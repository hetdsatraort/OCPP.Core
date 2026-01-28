using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class UserVehicleRequestDto
    {
        public string EVManufacturerID { get; set; }

        public string CarModelID { get; set; }

        public string CarModelVariant { get; set; }

        [Required]
        public string CarRegistrationNumber { get; set; }

        public int DefaultConfig { get; set; }

        public string BatteryTypeId { get; set; }

        public string BatteryCapacityId { get; set; }

        public string ChargerTypeId { get; set; }
    }

    public class UserVehicleUpdateDto : UserVehicleRequestDto
    {
        [Required]
        public string RecId { get; set; }
    }
}
