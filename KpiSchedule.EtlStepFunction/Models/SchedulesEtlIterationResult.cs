namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlIterationResult
    {
        public SchedulesEtlParserOutput GroupSchedules { get; set; }
        public SchedulesEtlParserOutput TeacherSchedules { get; set; }

    }
}