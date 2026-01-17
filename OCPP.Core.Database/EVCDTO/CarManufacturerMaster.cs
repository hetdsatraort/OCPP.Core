using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class CarManufacturerMaster
    {
        public string RecId { get; set; }
        public string ManufacturerName { get; set; }
        public string ManufacturerLogoImage { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

    }
}
