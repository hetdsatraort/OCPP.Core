using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class ChargingGuns
    {
        public string RecId { get; set; }
        public string ChargingStationId { get; set; }
        public string ConnectorId { get; set; }
        public string ChargingHubId { get; set; }
        public string ChargerTypeId { get; set; }
        public string ChargerTariff { get; set; }
        public string PowerOutput { get; set; }
        public string ChargerStatus { get; set; }
        public string ChargerMeterReading { get; set; }
        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
