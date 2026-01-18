using System;

namespace OCPP.Core.Management.Models.HardwareMaster
{
    public class ChargerTypeDto
    {
        public string RecId { get; set; }
        public string ChargerType { get; set; }
        public string ChargerTypeImage { get; set; }
        public string AdditionalInfo1 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public class ChargerTypeRequestDto
    {
        public string ChargerType { get; set; }
        public string ChargerTypeImage { get; set; }
        public string AdditionalInfo1 { get; set; }
    }

    public class ChargerTypeUpdateDto
    {
        public string RecId { get; set; }
        public string ChargerType { get; set; }
        public string ChargerTypeImage { get; set; }
        public string AdditionalInfo1 { get; set; }
        public int Active { get; set; }
    }
}
