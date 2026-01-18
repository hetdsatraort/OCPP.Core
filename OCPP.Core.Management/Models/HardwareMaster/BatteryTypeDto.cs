using System;

namespace OCPP.Core.Management.Models.HardwareMaster
{
    public class BatteryTypeDto
    {
        public string RecId { get; set; }
        public string BatteryType { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public class BatteryTypeRequestDto
    {
        public string BatteryType { get; set; }
    }

    public class BatteryTypeUpdateDto
    {
        public string RecId { get; set; }
        public string BatteryType { get; set; }
        public int Active { get; set; }
    }
}
