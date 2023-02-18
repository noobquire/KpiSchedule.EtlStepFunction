using Amazon.Lambda.Core;
using KpiSchedule.Common.Clients;
using KpiSchedule.Common.Mappers;
using KpiSchedule.Common.Models.RozKpiApi;
using KpiSchedule.Common.ServiceCollectionExtensions;
using KpiSchedule.EtlStepFunction;
using KpiSchedule.EtlStepFunction.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Entities.RozKpi;
using KpiSchedule.Common.Repositories;
using KpiSchedule.EtlStepFunction.Services;
using AutoMapper;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(SchedulesJsonSerializer))]

namespace KpiSchedule.EtlStepFunction;

public class SchedulesEtlTasks
{
    private readonly GroupSchedulesEtlService groupSchedulesEtlService;
    private readonly TeacherSchedulesEtlService teacherSchedulesEtlService;
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
            .AddKpiClient<IRozKpiApiTeachersClient, RozKpiApiTeachersClient>(config)
            .AddKpiClient<IRozKpiApiGroupsClient, RozKpiApiGroupsClient>(config)
            .AddAutoMapper(
                typeof(RozKpiApiGroupSchedule_GroupScheduleEntity_MapperProfile),
                typeof(RozKpiApiTeacherSchedule_TeacherScheduleEntity_MapperProfile))
            .AddDynamoDbSchedulesRepository<RozKpiGroupSchedulesRepository, GroupScheduleEntity>(config)
            .AddDynamoDbSchedulesRepository<RozKpiTeacherSchedulesRepository, TeacherScheduleEntity>(config)
            .AddScoped<GroupSchedulesEtlService>()
            .AddScoped<TeacherSchedulesEtlService>()
            .BuildServiceProvider();

        groupPrefixesToParse = config.GetRequiredSection("GroupPrefixesToParse").Get<IList<string>>()!;
        teacherPrefixesToParse = config.GetRequiredSection("TeacherPrefixesToParse").Get<IList<string>>()!;
        groupSchedulesEtlService = serviceProvider.GetRequiredService<GroupSchedulesEtlService>();
        teacherSchedulesEtlService = serviceProvider.GetRequiredService<TeacherSchedulesEtlService>();
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlOutput<RozKpiApiGroupSchedule>> ParseRozKpiGroupSchedulesTask()
    {
        var groupSchedules = await groupSchedulesEtlService.ScrapeGroupSchedules(groupPrefixesToParse);

        var output = new SchedulesEtlOutput<RozKpiApiGroupSchedule>()
        {
            Schedules = groupSchedules.ToList(),
            ParsedAt = DateTime.UtcNow,
            Count = groupSchedules.Count()
        };

        return output;
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlOutput<RozKpiApiTeacherSchedule>> ParseRozKpiTeacherSchedulesTask()
    {
        var teacherSchedules = await teacherSchedulesEtlService.ScrapeTeacherSchedules(teacherPrefixesToParse);

        var output = new SchedulesEtlOutput<RozKpiApiTeacherSchedule>()
        {
            Schedules = teacherSchedules.ToList(),
            ParsedAt = DateTime.UtcNow,
            Count = teacherSchedules.Count()
        };

        return output;
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task PutGroupSchedulesToDynamoDbTask(SchedulesEtlOutput<RozKpiApiGroupSchedule> schedules)
    {
        var groupSchedules = schedules.Schedules;

        await groupSchedulesEtlService.WriteGroupSchedulesToDynamoDb(groupSchedules);
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task PutTeacherSchedulesToDynamoDbTask(SchedulesEtlOutput<RozKpiApiTeacherSchedule> schedules)
    {
        var teacherSchedules = schedules.Schedules;

        await teacherSchedulesEtlService.WriteTeacherSchedulesToDynamoDb(teacherSchedules);
    }
}
