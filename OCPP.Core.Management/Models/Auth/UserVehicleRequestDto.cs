using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class UserVehicleRequestDto
    {
        public string EVManufacturerID { get; set; }

        public string CarModelID { get; set; }

        [Required]
        public string CarRegistrationNumber { get; set; }
    }

    public class UserVehicleUpdateDto
    {
        [Required]
        public string RecId { get; set; }

        public string EVManufacturerID { get; set; }

        public string CarModelID { get; set; }

        public string CarRegistrationNumber { get; set; }
    }

    public class SessionVehicleLinkDto
    {
        [Required]
        public string SessionId { get; set; }

        [Required]
        public string VehicleId { get; set; }
    }
}
