namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlIterationInput
    {
        public IList<IList<string>> PrefixChunks { get; set; }
        public int Index { get; set; }
        public int Count { get; set; }

        public SchedulesEtlParserOutput GroupSchedules { get; set; }
        public SchedulesEtlParserOutput TeacherSchedules { get; set; }
    }
}