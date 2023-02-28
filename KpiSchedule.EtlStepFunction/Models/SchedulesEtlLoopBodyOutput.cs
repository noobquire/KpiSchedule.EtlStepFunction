namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlLoopBodyOutput
    {
        public int Index { get; set; }
        public int Count { get; set; }
        public IList<IList<string>> PrefixChunks { get; set; }
        public SchedulesEtlParserOutput GroupSchedules { get; set; }
        public SchedulesEtlParserOutput TeacherSchedules { get; set; }
        public SchedulesEtlIterationResult IterationOutput { get; set; }
    }
}
