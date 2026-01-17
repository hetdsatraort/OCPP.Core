using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class ChargingSession
    {
        public string RecId { get; set; }
        public string ChargingGunId { get; set; }
        public string ChargingStationID { get; set; }
        public string StartMeterReading { get; set; }
        public string EndMeterReading { get; set; }
        public string EnergyTransmitted { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ChargingSpeed { get; set; }
        public string ChargingTariff { get; set; }
        public string ChargingTotalFee { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
