using AutoMapper;
using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Entities.RozKpi;
using KpiSchedule.Common.Exceptions;
using KpiSchedule.Common.Models.RozKpiApi;
using KpiSchedule.Common.Repositories;
using ILogger = Serilog.ILogger;

namespace KpiSchedule.EtlStepFunction.Services
{
    public class TeacherSchedulesEtlService
    {
        private readonly IRozKpiApiTeachersClient rozKpiApiTeachersClient;
        private readonly RozKpiTeacherSchedulesRepository repository;
        private readonly ILogger logger;
        private readonly IMapper mapper;

        public TeacherSchedulesEtlService(
            IRozKpiApiTeachersClient rozKpiApiTeachersClient, 
            RozKpiTeacherSchedulesRepository repository,
            ILogger logger, 
            IMapper mapper)
        {
            this.rozKpiApiTeachersClient = rozKpiApiTeachersClient;
            this.repository = repository;
            this.logger = logger;
            this.mapper = mapper;
        }

        public async Task<IEnumerable<RozKpiApiTeacherSchedule>> ScrapeTeacherSchedules(IEnumerable<string> teacherPrefixesToParse)
        {
            var teacherNameTasks = teacherPrefixesToParse.Select(async c => await rozKpiApiTeachersClient.GetTeachers(c.ToString()));
            var teacherNames = new List<string>();
            foreach (var teacherNameTask in teacherNameTasks)
            {
                var teacherNamesForPrefix = await teacherNameTask;
                teacherNames.AddRange(teacherNamesForPrefix.Data);
            }

            var teacherScheduleIdTasks = teacherNames.Select(async teacherName =>
            {
                try
                {
                    return (await rozKpiApiTeachersClient.GetTeacherScheduleId(teacherName));
                }
                catch (Exception)
                {
                    logger.Error("Caught error when getting teacher scheduleId for {teacherName}", teacherName);
                    return Guid.Empty;
                }
            });

            var teacherScheduleIds = new List<Guid>();
            foreach (var teacherScheduleIdTask in teacherScheduleIdTasks)
            {
                var id = await teacherScheduleIdTask;
                if (id != Guid.Empty)
                {
                    teacherScheduleIds.Add(id);
                }
            }
            var teacherScheduleTasks = teacherScheduleIds.Select(async id =>
            {
                try
                {
                    var schedule = await rozKpiApiTeachersClient.GetTeacherSchedule(id);
                    return schedule;
                }
                catch (KpiScheduleParserException)
                {
                    return null;
                }
                catch (KpiScheduleClientException)
                {
                    return null;
                }
                catch (Exception)
                {
                    logger.Fatal("Caught an unhandled exception when trying to parse scheduleId {scheduleId}", id);
                    return null;
                }
            });

            var teacherSchedules = new List<RozKpiApiTeacherSchedule>();

            foreach (var teacherScheduleTask in teacherScheduleTasks)
            {
                var teacherSchedule = await teacherScheduleTask;
                if (teacherSchedule != null)
                {
                    teacherSchedules.Add(teacherSchedule);
                }
            }

            logger.Information("Parsed a total of {schedulesCount} teacher schedules", teacherSchedules.Count);

            return teacherSchedules;
        }

        public async Task WriteTeacherSchedulesToDynamoDb(IEnumerable<RozKpiApiTeacherSchedule> schedules)
        {
            var mappedSchedules = mapper!.Map<IEnumerable<TeacherScheduleEntity>>(schedules);

            logger.Information("Writing {schedulesCount} schedules to DynamoDb", schedules.Count());
            await repository!.BatchPutSchedules(mappedSchedules);
        }
    }
}
