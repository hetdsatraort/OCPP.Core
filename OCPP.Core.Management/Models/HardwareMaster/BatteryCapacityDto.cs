using System;

namespace OCPP.Core.Management.Models.HardwareMaster
{
    public class BatteryCapacityDto
    {
        public string RecId { get; set; }
        public string BatteryCapacity { get; set; }
        public string BatteryCapacityUnit { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public class BatteryCapacityRequestDto
    {
        public string BatteryCapacity { get; set; }
        public string BatteryCapacityUnit { get; set; }
    }

    public class BatteryCapacityUpdateDto
    {
        public string RecId { get; set; }
        public string BatteryCapacity { get; set; }
        public string BatteryCapacityUnit { get; set; }
        public int Active { get; set; }
    }
}
