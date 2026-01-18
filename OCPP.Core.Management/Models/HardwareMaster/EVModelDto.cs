using System;

namespace OCPP.Core.Management.Models.HardwareMaster
{
    public class EVModelDto
    {
        public string RecId { get; set; }
        public string ModelName { get; set; }
        public string ManufacturerId { get; set; }
        public string ManufacturerName { get; set; }
        public string Variant { get; set; }
        public string BatteryTypeId { get; set; }
        public string BatteryTypeName { get; set; }
        public string BatteryCapacityId { get; set; }
        public string BatteryCapacityValue { get; set; }
        public string CarModelImage { get; set; }
        public string TypeASupport { get; set; }
        public string TypeBSupport { get; set; }
        public string ChadeMOSupport { get; set; }
        public string CCSSupport { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public class EVModelRequestDto
    {
        public string ModelName { get; set; }
        public string ManufacturerId { get; set; }
        public string Variant { get; set; }
        public string BatteryTypeId { get; set; }
        public string BatteryCapacityId { get; set; }
        public string CarModelImage { get; set; }
        public string TypeASupport { get; set; }
        public string TypeBSupport { get; set; }
        public string ChadeMOSupport { get; set; }
        public string CCSSupport { get; set; }
    }

    public class EVModelUpdateDto
    {
        public string RecId { get; set; }
        public string ModelName { get; set; }
        public string ManufacturerId { get; set; }
        public string Variant { get; set; }
        public string BatteryTypeId { get; set; }
        public string BatteryCapacityId { get; set; }
        public string CarModelImage { get; set; }
        public string TypeASupport { get; set; }
        public string TypeBSupport { get; set; }
        public string ChadeMOSupport { get; set; }
        public string CCSSupport { get; set; }
        public int Active { get; set; }
    }
}
