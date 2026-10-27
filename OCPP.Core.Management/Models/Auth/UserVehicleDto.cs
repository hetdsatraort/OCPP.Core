using System;

namespace OCPP.Core.Management.Models.Auth
{
    public class UserVehicleDto
    {
        public string RecId { get; set; }
        public string UserId { get; set; }
        public string EVManufacturerID { get; set; }
        public string CarModelID { get; set; }
        public string CarRegistrationNumber { get; set; }
        public int DefaultConfig { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

        // Resolved master-data names; only populated by endpoints that look them up.
        public string ManufacturerName { get; set; }
        public string ManufacturerLogoImage { get; set; }
        public string ModelName { get; set; }
        public string Variant { get; set; }
        public string CarModelImage { get; set; }
        public string BatteryType { get; set; }
        public string BatteryCapacity { get; set; }
        public string ChargerType { get; set; }
    }

    public class SessionVehicleResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string SessionId { get; set; }
        public UserVehicleDto Vehicle { get; set; }
    }
}
