using AutoMapper;
using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Entities.RozKpi;
using KpiSchedule.Common.Exceptions;
using KpiSchedule.Common.Models.RozKpiApi;
using KpiSchedule.Common.Repositories;
using KpiSchedule.EtlStepFunction.Models;
using ILogger = Serilog.ILogger;

namespace KpiSchedule.EtlStepFunction.Services
{
    public class GroupSchedulesEtlService
    {
        private readonly IRozKpiApiGroupsClient rozKpiApiGroupsClient;
        private readonly RozKpiGroupSchedulesRepository repository;
        private readonly ILogger logger;
        private readonly IMapper mapper;

        public GroupSchedulesEtlService(
            IRozKpiApiGroupsClient rozKpiApiGroupsClient,
            ILogger logger,
            IMapper mapper,
            RozKpiGroupSchedulesRepository repository)
        {
            this.rozKpiApiGroupsClient = rozKpiApiGroupsClient;
            this.logger = logger;
            this.mapper = mapper;
            this.repository = repository;
        }

        public async Task<IEnumerable<RozKpiApiGroupSchedule>> ScrapeGroupSchedules(IEnumerable<string> groupPrefixesToScrape)
        {
            var groupNameTasks = groupPrefixesToScrape.Select(async c => await rozKpiApiGroupsClient.GetGroups(c.ToString()));
            var groupNames = new List<string>();
            foreach (var groupNameTask in groupNameTasks)
            {
                var groupNamesForPrefix = await groupNameTask;
                groupNames.AddRange(groupNamesForPrefix.Data);
            }

            var groupScheduleIdTasks = groupNames.Select(async groupName =>
            {
                try
                {
                    return (await rozKpiApiGroupsClient.GetGroupScheduleIds(groupName)).First();
                }
                catch (KpiScheduleClientGroupNotFoundException)
                {
                    return Guid.Empty;
                }
            });

            var groupScheduleIds = new List<Guid>();
            foreach (var groupScheduleIdTask in groupScheduleIdTasks)
            {
                var id = await groupScheduleIdTask;
                if (id != Guid.Empty)
                {
                    groupScheduleIds.Add(id);
                }
            }
            var groupScheduleTasks = groupScheduleIds.Select(async id =>
            {
                try
                {
                    var schedule = await rozKpiApiGroupsClient.GetGroupSchedule(id);
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

            var groupSchedules = new List<RozKpiApiGroupSchedule>();

            foreach (var groupScheduleTask in groupScheduleTasks)
            {
                var groupSchedule = await groupScheduleTask;
                if (groupSchedule != null)
                {
                    groupSchedules.Add(groupSchedule);
                }
            }

            logger.Information("Parsed a total of {schedulesCount} group schedules", groupSchedules.Count);

            return groupSchedules;
        }

        public async Task WriteGroupSchedulesToDynamoDb(IEnumerable<RozKpiApiGroupSchedule> schedules)
        {
            var mappedSchedules = mapper!.Map<IEnumerable<GroupScheduleEntity>>(schedules);

            logger.Information("Writing {schedulesCount} schedules to DynamoDb", schedules.Count());
            await repository!.BatchPutSchedules(mappedSchedules);
        }
    }
}
