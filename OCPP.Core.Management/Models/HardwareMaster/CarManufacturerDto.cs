using System;

namespace OCPP.Core.Management.Models.HardwareMaster
{
    public class CarManufacturerDto
    {
        public string RecId { get; set; }
        public string ManufacturerName { get; set; }
        public string ManufacturerLogoImage { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public class CarManufacturerRequestDto
    {
        public string ManufacturerName { get; set; }
        public string ManufacturerLogoImage { get; set; }
    }

    public class CarManufacturerUpdateDto
    {
        public string RecId { get; set; }
        public string ManufacturerName { get; set; }
        public string ManufacturerLogoImage { get; set; }
        public int Active { get; set; }
    }
}
