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
    public class GroupSchedulesEtlService
    {
        private readonly EtlServiceOptions options;
        private readonly IRozKpiApiGroupsClient rozKpiApiGroupsClient;
        private readonly GroupSchedulesRepository repository;
        private readonly ILogger logger;

        public GroupSchedulesEtlService(
            IRozKpiApiGroupsClient rozKpiApiGroupsClient,
            ILogger logger,
            GroupSchedulesRepository repository,
            IOptions<EtlServiceOptions> options)
        {
            this.rozKpiApiGroupsClient = rozKpiApiGroupsClient;
            this.logger = logger;
            this.repository = repository;
            this.options = options.Value;
        }

        public async Task<(SchedulesEtlParserOutput output, IEnumerable<RozKpiApiGroupSchedule>)> ScrapeGroupSchedules(IEnumerable<string> groupPrefixesToScrape)
        {
            int clientExceptions = 0, parserExceptions = 0, unhandledExceptions = 0;
            var groupNames = new ConcurrentBag<string>();
            await Parallel.ForEachAsync(groupPrefixesToScrape, new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism,
            }, async (prefix, token) =>
            {
                var groupNamesForPrefix = await rozKpiApiGroupsClient.GetGroups(prefix);
                foreach (var groupName in groupNamesForPrefix.Data)
                {
                    groupNames.Add(groupName);
                }
            });

            var groupScheduleIds = new ConcurrentBag<Guid>();
            await Parallel.ForEachAsync(groupNames, new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism,
            }, async (groupName, token) =>
            {
                try
                {
                    var groupScheduleIdsForName = await rozKpiApiGroupsClient.GetGroupScheduleIds(groupName);
                    foreach (var groupScheduleId in groupScheduleIdsForName)
                    {
                        groupScheduleIds.Add(groupScheduleId);
                    }
                }
                catch (KpiScheduleClientGroupNotFoundException)
                {
                    Interlocked.Increment(ref clientExceptions);
                    logger.Error("ScheduleId for group {groupName} not found", groupName);
                }
                catch
                {
                    Interlocked.Increment(ref unhandledExceptions);
                    logger.Fatal("Unhandled error when getting scheduleId for {groupName}", groupName);
                }
            });

            var groupSchedules = new ConcurrentBag<RozKpiApiGroupSchedule>();
            await Parallel.ForEachAsync(groupScheduleIds, new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism
            }, async (groupScheduleId, token) =>
            {
                try
                {
                    var schedule = await rozKpiApiGroupsClient.GetGroupSchedule(groupScheduleId);
                    groupSchedules.Add(schedule);
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
                    logger.Fatal("Caught an unhandled exception when trying to parse scheduleId {scheduleId}", groupScheduleId);
                }
            });

            var output = new SchedulesEtlParserOutput
            {
                Count = groupSchedules.Count,
                ClientExceptions = clientExceptions,
                ParserExceptions = parserExceptions,
                UnhandledExceptions = unhandledExceptions
            };

            return (output, groupSchedules);
        }

        public async Task WriteGroupSchedulesToDynamoDb(IEnumerable<RozKpiApiGroupSchedule> schedules)
        {
            var mappedSchedules = schedules.Select(s => s.MapToEntity()).ToList();

            logger.Information("Writing {schedulesCount} schedules to DynamoDb", schedules.Count());
            await repository!.BatchPutSchedules(mappedSchedules);
        }
    }
}
