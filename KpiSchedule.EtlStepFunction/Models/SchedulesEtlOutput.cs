﻿namespace KpiSchedule.EtlStepFunction.Models
{
    public class SchedulesEtlOutput
    {
        public SchedulesEtlParserOutput GroupSchedules { get; set; }
        public SchedulesEtlParserOutput TeacherSchedules { get; set; }
    }
}
