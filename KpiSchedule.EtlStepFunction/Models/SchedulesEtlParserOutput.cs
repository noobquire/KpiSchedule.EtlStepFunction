namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlParserOutput
    {
        public int Count { get; set; }
        public int ParserExceptions { get; set; }
        public int ClientExceptions { get; set; }
        public int UnhandledExceptions { get; set; }

        public static SchedulesEtlParserOutput operator +(SchedulesEtlParserOutput first, SchedulesEtlParserOutput second)
        {
            var result = new SchedulesEtlParserOutput()
            {
                Count = first.Count + second.Count,
                ParserExceptions = first.ParserExceptions + second.ParserExceptions,
                ClientExceptions = first.ClientExceptions + second.ClientExceptions,
                UnhandledExceptions = first.UnhandledExceptions + second.UnhandledExceptions
            };
            return result;
        }
    }
}
