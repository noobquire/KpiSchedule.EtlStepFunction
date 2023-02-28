namespace KpiSchedule.EtlStepFunction.Options
{
    public class EtlProducerOptions
    {
        public IList<string> Prefixes { get; set; }
        public int SchedulesPerSlice { get; set; }
        public string QueueUrl { get; set; }
    }
}
