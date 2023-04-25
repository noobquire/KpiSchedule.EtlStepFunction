using Amazon.Lambda.Core;
using KpiSchedule.Common.Clients;
using KpiSchedule.Common.ServiceCollectionExtensions;
using KpiSchedule.EtlStepFunction;
using KpiSchedule.EtlStepFunction.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KpiSchedule.Common.Clients.Interfaces;
using KpiSchedule.Common.Repositories;
using KpiSchedule.EtlStepFunction.Services;
using KpiSchedule.EtlStepFunction.Options;
using ILogger = Serilog.ILogger;
using KpiSchedule.Common.Entities.Group;
using KpiSchedule.Common.Entities.Teacher;

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

    /// <summary>
    /// 1. Get group schedule IDs from roz.kpi.ua for each prefix.
    /// 2. Get schedule HTML page for each schedule ID.
    /// 3. Parse group schedule model from HTML page (use teacher schedules to fix data inconsistencies, exceptions are caught).
    /// 4. Map schedule model to DB entity.
    /// 5. Save schedule entity to DynamoDB repository.
    /// 6. Return amount of schedules parsed and exceptions caught by type.
    /// </summary>
    /// <param name="input">Parser input.</param>
    /// <returns>Parser output.</returns>
    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlParserOutput> RozKpiGroupSchedulesEtlTask(SchedulesEtlInput input)
    {
        logger.Information("Parsing group schedules for prefixes {groupPrefixes}", string.Join(", ", input.Prefixes));
        var (output, groupSchedules) = await groupSchedulesEtlService.ScrapeGroupSchedules(input.Prefixes);
        if(groupSchedules.Any())
        {
            await groupSchedulesEtlService.WriteGroupSchedulesToDynamoDb(groupSchedules);
        }
        return output;
    }

    /// <summary>
    /// 1. Get teacher schedule IDs from roz.kpi.ua for each prefix.
    /// 2. Get schedule HTML page for each schedule ID.
    /// 3. Parse teacher schedule model from HTML page.
    /// 4. Map schedule model to DB entity.
    /// 5. Save schedule entity to DynamoDB repository.
    /// 6. Return amount of schedules parsed and exceptions caught by type.
    /// </summary>
    /// <param name="input">Parser input.</param>
    /// <returns>Parser output.</returns>
    [LambdaSerializer(typeof(SchedulesJsonSerializer))]
    public async Task<SchedulesEtlParserOutput> RozKpiTeacherSchedulesEtlTask(SchedulesEtlInput input)
    {
        logger.Information("Parsing teacher schedules for prefixes {teacherPrefixes}", string.Join(", ", input.Prefixes));
        var (output, teacherSchedules) = await teacherSchedulesEtlService.ScrapeTeacherSchedules(input.Prefixes);
        if(teacherSchedules.Any())
        {
            await teacherSchedulesEtlService.WriteTeacherSchedulesToDynamoDb(teacherSchedules);
        }
        return output;
    }

    /// <summary>
    /// Sums amount of schedules parsed and exceptions caught on this iteration of step function.
    /// </summary>
    /// <param name="output">Parser output.</param>
    /// <returns>Parser input.</returns>
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
