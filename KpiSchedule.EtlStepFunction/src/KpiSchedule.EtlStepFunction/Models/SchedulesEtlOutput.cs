namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlOutput<TSchedule>
    {
        public int Count { get; set; }
        public DateTime ParsedAt { get; set; }
        public IList<TSchedule> Schedules { get; set; }
    }
}
