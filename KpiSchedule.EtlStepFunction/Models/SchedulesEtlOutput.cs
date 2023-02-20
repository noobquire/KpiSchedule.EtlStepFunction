namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlOutput
    {
        public int Count { get; set; }
        public DateTime ParsedAt { get; set; }
        public int ParserExceptions { get; set; }
        public int ClientExceptions { get; set; }
        public int UnhandledExceptions { get; set; }
    }
}
