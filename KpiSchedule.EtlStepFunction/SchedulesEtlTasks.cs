using Amazon.Lambda.Core;
using KpiSchedule.Common.Clients;
using KpiSchedule.Common.Mappers;
using KpiSchedule.Common.ServiceCollectionExtensions;
using KpiSchedule.EtlStepFunction;
using KpiSchedule.EtlStepFunction.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Entities.RozKpi;
using KpiSchedule.Common.Repositories;
using KpiSchedule.EtlStepFunction.Services;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(SchedulesJsonSerializer))]

namespace KpiSchedule.EtlStepFunction;

public class SchedulesEtlTasks
{
    private readonly GroupSchedulesEtlService groupSchedulesEtlService;
    private readonly TeacherSchedulesEtlService teacherSchedulesEtlService;

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
            .Configure<EtlServiceOptions>(config.GetSection("EtlService"))
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

        groupSchedulesEtlService = serviceProvider.GetRequiredService<GroupSchedulesEtlService>();
        teacherSchedulesEtlService = serviceProvider.GetRequiredService<TeacherSchedulesEtlService>();
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlOutput> RozKpiGroupSchedulesEtlTask(IList<string> groupPrefixesToParse)
    {
        var (output, groupSchedules) = await groupSchedulesEtlService.ScrapeGroupSchedules(groupPrefixesToParse);

        await groupSchedulesEtlService.WriteGroupSchedulesToDynamoDb(groupSchedules);

        return output;
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlOutput> RozKpiTeacherSchedulesEtlTask(IList<string> teacherPrefixesToParse)
    {
        var (output, teacherSchedules) = await teacherSchedulesEtlService.ScrapeTeacherSchedules(teacherPrefixesToParse);
        await teacherSchedulesEtlService.WriteTeacherSchedulesToDynamoDb(teacherSchedules);

        return output;
    }
}
