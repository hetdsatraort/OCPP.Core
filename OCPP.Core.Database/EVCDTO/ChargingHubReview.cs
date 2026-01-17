using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class ChargingHubReview
    {
        public string RecId { get; set; }
        public string ChargingHubId { get; set; }
        public string ChargingStationId { get; set; }
        public int Rating { get; set; }
        public string Description { get; set; }
        public DateTime ReviewTime { get; set; }
        public string ReviewImage1 { get; set; }
        public string ReviewImage2 { get; set; }
        public string ReviewImage3 { get; set; }
        public string ReviewImage4 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

    }
}
