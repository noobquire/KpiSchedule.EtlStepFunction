using Amazon.Lambda.Core;
using KpiSchedule.Common.Clients;
using KpiSchedule.Common.Exceptions;
using KpiSchedule.Common.Models.RozKpiApi;
using KpiSchedule.Common.ServiceCollectionExtensions;
using KpiSchedule.EtlStepFunction;
using KpiSchedule.EtlStepFunction.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(SchedulesJsonSerializer))]

namespace KpiSchedule.EtlStepFunction
{
    public class SchedulesEtlTasks
    {
        private readonly ILogger logger;
        private readonly RozKpiApiGroupsClient rozKpiApiGroupsClient;
        private readonly RozKpiApiTeachersClient rozKpiApiTeachersClient;
        private readonly IList<string> groupPrefixesToParse;
        private readonly IList<string> teacherPrefixesToParse;

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public SchedulesEtlTasks()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json")
                .Build();
            var serviceProvider = new ServiceCollection()
                .AddRozKpiParsers()
                .AddSerilogConsoleLogger()
                //.AddSerilogCloudWatchLogger(config)
                .AddKpiClient<RozKpiApiTeachersClient>(config)
                .AddKpiClient<RozKpiApiGroupsClient>(config)
                .BuildServiceProvider();
            logger = serviceProvider.GetService<ILogger>()!;
            rozKpiApiGroupsClient = serviceProvider.GetService<RozKpiApiGroupsClient>()!;
            rozKpiApiTeachersClient = serviceProvider.GetService<RozKpiApiTeachersClient>()!;
            groupPrefixesToParse = config.GetRequiredSection("GroupPrefixesToParse").Get<IList<string>>()!;
            teacherPrefixesToParse = config.GetRequiredSection("TeacherPrefixesToParse").Get<IList<string>>()!;
        }

        [LambdaSerializer(typeof(SchedulesJsonSerializer))]
        public async Task<SchedulesEtlOutput<RozKpiApiGroupSchedule>> ParseRozKpiGroupSchedulesTask()
        {
            var groupNameTasks = groupPrefixesToParse.Select(async c => await rozKpiApiGroupsClient.GetGroups(c.ToString()));
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

            var output = new SchedulesEtlOutput<RozKpiApiGroupSchedule>()
            {
                Schedules = groupSchedules,
                ParsedAt = DateTime.UtcNow,
                Count = groupSchedules.Count
            };

            return output;
        }

        [LambdaSerializer(typeof(JsonSerializer))]
        public async Task<SchedulesEtlOutput<RozKpiApiTeacherSchedule>> ParseRozKpiTeacherSchedulesTask()
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

            var output = new SchedulesEtlOutput<RozKpiApiTeacherSchedule>()
            {
                Schedules = teacherSchedules,
                ParsedAt = DateTime.UtcNow,
                Count = teacherSchedules.Count
            };

            return output;
        }
    }
}