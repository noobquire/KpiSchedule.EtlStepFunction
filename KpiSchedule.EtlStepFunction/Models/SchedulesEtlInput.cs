namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlInput
    {
        public IList<string> Prefixes { get; set; }
        public int BatchSize { get; set; }
    }
}
