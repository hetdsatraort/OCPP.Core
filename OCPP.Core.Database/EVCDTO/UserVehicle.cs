using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class UserVehicle
    {
        public string RecId { get; set; }
        public string UserId { get; set; }
        public string EVManufacturerID { get; set; }
        public string CarModelID { get; set; }
        public string CarModelVariant { get; set; }
        public string CarRegistrationNumber { get; set; }
        public int DefaultConfig { get; set; }
        public string BatteryTypeId { get; set; }
        public string BatteryCapacityId { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
