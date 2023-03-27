using Amazon.Lambda.Core;
using KpiSchedule.Common.Clients;
using KpiSchedule.Common.Mappers;
using KpiSchedule.Common.ServiceCollectionExtensions;
using KpiSchedule.EtlStepFunction;
using KpiSchedule.EtlStepFunction.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Entities;
using KpiSchedule.Common.Repositories;
using KpiSchedule.EtlStepFunction.Services;
using KpiSchedule.EtlStepFunction.Options;
using ILogger = Serilog.ILogger;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(SchedulesJsonSerializer))]

namespace KpiSchedule.EtlStepFunction;

public class SchedulesEtlTasks
{
    private readonly GroupSchedulesEtlService groupSchedulesEtlService;
    private readonly TeacherSchedulesEtlService teacherSchedulesEtlService;
    private readonly ILogger logger;

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
            .AddDynamoDbSchedulesRepository<GroupSchedulesRepository, GroupScheduleEntity, GroupScheduleDayEntity, GroupSchedulePairEntity>(config)
            .AddDynamoDbSchedulesRepository<TeacherSchedulesRepository, TeacherScheduleEntity, TeacherScheduleDayEntity, TeacherSchedulePairEntity>(config)
            .AddScoped<GroupSchedulesEtlService>()
            .AddScoped<TeacherSchedulesEtlService>()
            .BuildServiceProvider();

        groupSchedulesEtlService = serviceProvider.GetRequiredService<GroupSchedulesEtlService>();
        teacherSchedulesEtlService = serviceProvider.GetRequiredService<TeacherSchedulesEtlService>();
        logger = serviceProvider.GetRequiredService<ILogger>();
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlParserOutput> RozKpiGroupSchedulesEtlTask(SchedulesEtlInput input)
    {
        var (output, groupSchedules) = await groupSchedulesEtlService.ScrapeGroupSchedules(input.Prefixes);
        if(groupSchedules.Any())
        {
            await groupSchedulesEtlService.WriteGroupSchedulesToDynamoDb(groupSchedules);
        }
        return output;
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlParserOutput> RozKpiTeacherSchedulesEtlTask(SchedulesEtlInput input)
    {
        var (output, teacherSchedules) = await teacherSchedulesEtlService.ScrapeTeacherSchedules(input.Prefixes);
        if(teacherSchedules.Any())
        {
            await teacherSchedulesEtlService.WriteTeacherSchedulesToDynamoDb(teacherSchedules);
        }
        return output;
    }

    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public SchedulesEtlIterationInput IteratePrefixesAndSumResults(SchedulesEtlLoopBodyOutput output)
    {
        var input = new SchedulesEtlIterationInput()
        {
            GroupSchedules = output.GroupSchedules + output.IterationOutput.GroupSchedules,
            TeacherSchedules = output.TeacherSchedules + output.IterationOutput.TeacherSchedules,
            PrefixChunks = output.PrefixChunks,
            Count = output.Count,
            Index = output.Index + 1
        };
        return input;
    }
}
