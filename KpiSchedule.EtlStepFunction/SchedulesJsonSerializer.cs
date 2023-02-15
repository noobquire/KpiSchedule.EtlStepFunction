using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace KpiSchedule.EtlStepFunction
{
    public class SchedulesJsonSerializer : Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer
    {
        public SchedulesJsonSerializer() : base(CustomizeSerializerSettings)
        {
        }

        private static void CustomizeSerializerSettings(JsonSerializerOptions options)
        {
            options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            options.WriteIndented = true;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }
    }
}
