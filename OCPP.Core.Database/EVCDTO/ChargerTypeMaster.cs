using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class ChargerTypeMaster
    {
        public string RecId { get; set; }
        public string ChargerType { get; set; }
        public string ChargerTypeImage { get; set; }
        public string Additional_Info_1 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
