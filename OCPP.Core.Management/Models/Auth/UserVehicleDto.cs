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
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public class SessionVehicleResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string SessionId { get; set; }
        public UserVehicleDto Vehicle { get; set; }
    }
}
