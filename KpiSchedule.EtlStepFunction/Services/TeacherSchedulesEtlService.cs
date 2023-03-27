using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Entities;
using KpiSchedule.Common.Exceptions;
using KpiSchedule.Common.Mappers;
using KpiSchedule.Common.Models.RozKpiApi;
using KpiSchedule.Common.Repositories;
using KpiSchedule.EtlStepFunction.Models;
using KpiSchedule.EtlStepFunction.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using ILogger = Serilog.ILogger;

namespace KpiSchedule.EtlStepFunction.Services
{
    public class TeacherSchedulesEtlService
    {
        private readonly IRozKpiApiTeachersClient rozKpiApiTeachersClient;
        private readonly TeacherSchedulesRepository repository;
        private readonly ILogger logger;
        private readonly EtlServiceOptions options;

        public TeacherSchedulesEtlService(
            IRozKpiApiTeachersClient rozKpiApiTeachersClient, 
            TeacherSchedulesRepository repository,
            ILogger logger,
            IOptions<EtlServiceOptions> options)
        {
            this.rozKpiApiTeachersClient = rozKpiApiTeachersClient;
            this.repository = repository;
            this.logger = logger;
            this.options = options.Value;
        }

        public async Task<(SchedulesEtlParserOutput, IEnumerable<RozKpiApiTeacherSchedule>)> ScrapeTeacherSchedules(IEnumerable<string> teacherPrefixesToParse)
        {
            int clientExceptions = 0, parserExceptions = 0, unhandledExceptions = 0;
            var teacherNames = new ConcurrentBag<string>();
            await Parallel.ForEachAsync(teacherPrefixesToParse, new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism,
            }, async (prefix, token) =>
            {
                var teacherNamesForPrefix = await rozKpiApiTeachersClient.GetTeachers(prefix);
                foreach (var teacherName in teacherNamesForPrefix.Data)
                {
                    teacherNames.Add(teacherName);
                }
            });

            var teacherScheduleIds = new ConcurrentBag<Guid>();
            await Parallel.ForEachAsync(teacherNames, new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism,
            }, async (teacherName, token) =>
            {
                try
                {
                    var teacherScheduleId = await rozKpiApiTeachersClient.GetTeacherScheduleId(teacherName);
                    teacherScheduleIds.Add(teacherScheduleId);
                }
                catch (KpiScheduleClientGroupNotFoundException) // BUG: create a separate exception for teachers
                {
                    Interlocked.Increment(ref clientExceptions);
                    logger.Error("ScheduleId for teacher {teacherName} not found", teacherName);
                }
                catch
                {
                    Interlocked.Increment(ref unhandledExceptions);
                    logger.Fatal("Unhandled error when getting scheduleId for {teacherName}", teacherName);
                }
            });

            var teacherSchedules = new ConcurrentBag<RozKpiApiTeacherSchedule>();
            await Parallel.ForEachAsync(teacherScheduleIds, new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism
            }, async (teacherScheduleId, token) =>
            {
                try
                {
                    var schedule = await rozKpiApiTeachersClient.GetTeacherSchedule(teacherScheduleId);
                    teacherSchedules.Add(schedule);
                }
                catch (KpiScheduleParserException)
                {
                    Interlocked.Increment(ref parserExceptions);
                }
                catch (KpiScheduleClientException)
                {
                    Interlocked.Increment(ref clientExceptions);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref unhandledExceptions);
                    logger.Fatal("Caught an unhandled exception when trying to parse scheduleId {scheduleId}", teacherScheduleId);
                }
            });

            var output = new SchedulesEtlParserOutput
            {
                Count = teacherSchedules.Count,
                ClientExceptions = clientExceptions,
                ParserExceptions = parserExceptions,
                UnhandledExceptions = unhandledExceptions
            };

            return (output, teacherSchedules);
        }

        public async Task WriteTeacherSchedulesToDynamoDb(IEnumerable<RozKpiApiTeacherSchedule> schedules)
        {
            var mappedSchedules = schedules.Select(s => s.MapToEntity()).ToList();

            logger.Information("Writing {schedulesCount} schedules to DynamoDb", schedules.Count());
            await repository!.BatchPutSchedules(mappedSchedules);
        }
    }
}
