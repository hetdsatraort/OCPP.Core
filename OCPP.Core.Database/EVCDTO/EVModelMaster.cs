using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class EVModelMaster
    {        
        public string RecId { get; set; }
        public string ModelName { get; set; }
        public string ManufacturerId { get; set; }
        public string Variant { get; set; }
        public string BatterytypeId { get; set; }
        public string BatteryCapacityId { get; set; }
        public string CarModelImage { get; set; }
        public string TypeASupport { get; set; }
        public string TypeBSupport { get; set; }
        public string ChadeMOSupport { get; set; }
        public string CCSSupport { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

    }
}
