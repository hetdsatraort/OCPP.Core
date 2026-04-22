using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiLocationService : IOcpiLocationService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiLocationService> _logger;
        private readonly IConfiguration _configuration;

        public OcpiLocationService(
            OCPPCoreContext dbContext, 
            ILogger<OcpiLocationService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<OcpiLocation>> GetOurLocationsAsync(int offset, int limit)
        {
            var countryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var partyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var hubs = await _dbContext.ChargingHubs
                .Where(h => h.Active == 1)
                .OrderBy(h => h.ChargingHubName)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            var stations = await _dbContext.ChargingStations
                .Where(s => s.Active == 1 && hubs.Select(h => h.RecId).Contains(s.ChargingHubId))
                .ToListAsync();

            var guns = await _dbContext.ChargingGuns
                .Where(g => g.Active == 1 && stations.Select(s => s.RecId).Contains(g.ChargingStationId))
                .ToListAsync();

            return hubs.Select(h => MapToOcpiLocation(h, stations, guns, countryCode, partyId)).ToList();
        }

        public async Task<OcpiLocation> GetOurLocationAsync(string locationId)
        {
            var countryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var partyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var hub = await _dbContext.ChargingHubs
                .FirstOrDefaultAsync(h => h.RecId == locationId && h.Active == 1);

            var stations = await _dbContext.ChargingStations
                .Where(s => s.Active == 1 && s.ChargingHubId == locationId)
                .ToListAsync();

            var guns = await _dbContext.ChargingGuns
                .Where(g => g.Active == 1 && stations.Select(s => s.RecId).Contains(g.ChargingStationId))
                .ToListAsync();
            if (hub == null)
                return new OcpiLocation();

            return MapToOcpiLocation(hub, stations, guns, countryCode, partyId);
        }

        public async Task<OcpiEvse> GetOurEvseAsync(string locationId, string evseUid)
        {
            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == evseUid && s.ChargingHubId == locationId);

            if (station == null)
                return new OcpiEvse();

            var guns = await _dbContext.ChargingGuns
                .Where(g => g.ChargingStationId == evseUid)
                .ToListAsync();

            return MapToOcpiEvse(station, guns);
        }

        public async Task<OcpiConnector> GetOurConnectorAsync(string locationId, string evseUid, string connectorId)
        {

            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == evseUid && s.ChargingHubId == locationId);

            if (station == null)
                return new OcpiConnector();

            var gun = await _dbContext.ChargingGuns
                .FirstOrDefaultAsync(g => g.RecId == connectorId 
                    && g.ChargingStationId == evseUid 
                    && station.ChargingHubId == locationId);

            if (gun == null)
                return new OcpiConnector();

            return MapToOcpiConnector(gun);
        }

        public async Task StorePartnerLocationAsync(int partnerCredentialId, OcpiLocation location)
        {
            var existing = await _dbContext.OcpiPartnerLocations
                .FirstOrDefaultAsync(l => l.CountryCode == location.CountryCode.ToString() 
                    && l.PartyId == location.PartyId 
                    && l.LocationId == location.Id);

            if (existing != null)
            {
                // Update existing
                existing.Name = location.Name;
                existing.Address = location.Address;
                existing.City = location.City;
                existing.PostalCode = location.PostalCode;
                existing.Country = location.Country!.ToString();
                existing.Latitude = location.Coordinates?.Latitude;
                existing.Longitude = location.Coordinates?.Longitude;
                existing.LocationType = location.Type?.ToString();
                existing.LastUpdated = location.LastUpdated ?? DateTime.UtcNow;
                
                _dbContext.OcpiPartnerLocations.Update(existing);
            }
            else
            {
                // Create new
                var newLocation = new OcpiPartnerLocation
                {
                    CountryCode = location.CountryCode.ToString(),
                    PartyId = location.PartyId,
                    LocationId = location.Id,
                    Name = location.Name,
                    Address = location.Address,
                    City = location.City,
                    PostalCode = location.PostalCode,
                    Country = location.Country!.ToString(),
                    Latitude = location.Coordinates?.Latitude,
                    Longitude = location.Coordinates?.Longitude,
                    LocationType = location.Type?.ToString(),
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = location.LastUpdated ?? DateTime.UtcNow
                };
                
                await _dbContext.OcpiPartnerLocations.AddAsync(newLocation);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task StorePartnerEvseAsync(int partnerLocationId, OcpiEvse evse)
        {
            var existing = await _dbContext.OcpiPartnerEvses
                .FirstOrDefaultAsync(e => e.EvseUid == evse.Uid && e.PartnerLocationId == partnerLocationId);

            if (existing != null)
            {
                // Update existing
                existing.EvseId = evse.EvseId;
                existing.Status = evse.Status.ToString();
                existing.StatusDateTime = evse.LastUpdated ?? DateTime.UtcNow;
                existing.FloorLevel = evse.FloorLevel;
                existing.PhysicalReference = evse.PhysicalReference;
                existing.LastUpdated = evse.LastUpdated ?? DateTime.UtcNow;
                
                _dbContext.OcpiPartnerEvses.Update(existing);
            }
            else
            {
                // Create new
                var newEvse = new OcpiPartnerEvse
                {
                    EvseUid = evse.Uid,
                    EvseId = evse.EvseId,
                    Status = evse.Status.ToString(),
                    StatusDateTime = evse.LastUpdated ?? DateTime.UtcNow,
                    FloorLevel = evse.FloorLevel,
                    PhysicalReference = evse.PhysicalReference,
                    PartnerLocationId = partnerLocationId,
                    LastUpdated = evse.LastUpdated ?? DateTime.UtcNow
                };
                
                await _dbContext.OcpiPartnerEvses.AddAsync(newEvse);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task StorePartnerConnectorAsync(int partnerEvseId, OcpiConnector connector)
        {
            var existing = await _dbContext.OcpiPartnerConnectors
                .FirstOrDefaultAsync(c => c.ConnectorId == connector.Id && c.PartnerEvseId == partnerEvseId);

            if (existing != null)
            {
                // Update existing
                existing.Standard = connector.Standard.ToString();
                existing.Format = connector.Format.ToString();
                existing.PowerType = connector.PowerType.ToString();
                existing.MaxVoltage = connector.MaxVoltage;
                existing.MaxAmperage = connector.MaxAmperage;
                existing.MaxElectricPower = connector.MaxElectricPower;
                existing.LastUpdated = connector.LastUpdated ?? DateTime.UtcNow;
                
                _dbContext.OcpiPartnerConnectors.Update(existing);
            }
            else
            {
                // Create new
                var newConnector = new OcpiPartnerConnector
                {
                    ConnectorId = connector.Id,
                    Standard = connector.Standard.ToString(),
                    Format = connector.Format.ToString(),
                    PowerType = connector.PowerType.ToString(),
                    MaxVoltage = connector.MaxVoltage,
                    MaxAmperage = connector.MaxAmperage,
                    MaxElectricPower = connector.MaxElectricPower,
                    PartnerEvseId = partnerEvseId,
                    LastUpdated = connector.LastUpdated ?? DateTime.UtcNow
                };
                
                await _dbContext.OcpiPartnerConnectors.AddAsync(newConnector);
            }

            await _dbContext.SaveChangesAsync();
        }

        #region Mapping Methods

        private OcpiLocation MapToOcpiLocation(OCPP.Core.Database.EVCDTO.ChargingHub hub, List<OCPP.Core.Database.EVCDTO.ChargingStation> stations, List<OCPP.Core.Database.EVCDTO.ChargingGuns> guns, string countryCode, string partyId)
        {
            var cc = Enum.Parse<CountryCode>("356", true);

            return new OcpiLocation
            {
                CountryCode = CountryCode.India,
                PartyId = partyId,
                Id = hub.RecId,
                Publish = true,
                Name = hub.ChargingHubName,
                Address = hub.AddressLine1 + (string.IsNullOrEmpty(hub.AddressLine2) ? "" : ", " + hub.AddressLine2) + (string.IsNullOrEmpty(hub.City) ? "" : ", " + hub.City),
                City = hub.City ?? "Unknown",
                PostalCode = hub.Pincode ?? "",
                Country = CountryCode.India.ToString(),
                Coordinates = new OcpiGeolocation
                {
                    Latitude = hub.Latitude?.ToString() ?? "0",
                    Longitude = hub.Longitude?.ToString() ?? "0"
                },
                Type = LocationType.OnStreet,
                Evses = stations.Select(s => MapToOcpiEvse(s, guns)).ToList() ?? new List<OcpiEvse>(),
                Operator = new OcpiBusinessDetails
                {
                    Name = _configuration["OCPI:BusinessName"] ?? "EV Charging Platform"
                },
                LastUpdated = hub.UpdatedOn
            };
        }

        private OcpiEvse MapToOcpiEvse(OCPP.Core.Database.EVCDTO.ChargingStation station, List<OCPP.Core.Database.EVCDTO.ChargingGuns> guns)
        {
            var chargePointId = station.ChargingPointId ?? station.RecId;
            var chargePoint = _dbContext.ChargePoints.FirstOrDefault(cp => cp.ChargePointId == chargePointId);


            var status = MapStatus(guns.FirstOrDefault(g => g.ChargingStationId == station.RecId)?.ChargerStatus ?? "UNKNOWN");
            
            return new OcpiEvse
            {
                Uid = station.RecId,
                EvseId = $"IN*CPO*E{station.RecId}",
                Status = status,
                StatusSchedule = null, // For simplicity, not implementing schedule in this example
                Connectors = guns?.Where(g => g.ChargingStationId == station.RecId).Select(MapToOcpiConnector).ToList() ?? new List<OcpiConnector>(),
                PhysicalReference = "Station " + station.ChargingPointId,
                LastUpdated = station.UpdatedOn
            };
        }

        private OcpiConnector MapToOcpiConnector(OCPP.Core.Database.EVCDTO.ChargingGuns gun)
        {
            var connectorType = _dbContext.ChargerTypeMasters.FirstOrDefault(ct => ct.RecId == gun.ChargerTypeId)?.ChargerType ?? "Unknown";
            return new OcpiConnector
            {
                Id = gun.RecId,
                Standard = MapConnectorType(connectorType),
                Format = ConnectorFormat.Socket,
                PowerType = connectorType?.ToUpper().Contains("AC") == true ? PowerType.Ac3Phase : PowerType.Dc,
                MaxVoltage = ParseVoltage(gun.PowerOutput),
                MaxAmperage = ParseAmperage(gun.PowerOutput),
                MaxElectricPower = ParsePower(gun.PowerOutput),
                LastUpdated = gun.UpdatedOn
            };
        }

        private EvseStatus MapStatus(string ocppStatus)
        {
            return ocppStatus?.ToUpper() switch
            {
                "AVAILABLE" => EvseStatus.Available,
                "OCCUPIED" => EvseStatus.Charging,
                "CHARGING" => EvseStatus.Charging,
                "UNAVAILABLE" => EvseStatus.OutOfOrder,
                "FAULTED" => EvseStatus.OutOfOrder,
                _ => EvseStatus.Unknown
            };
        }

        private ConnectorType MapConnectorType(string chargerType)
        {
            return chargerType?.ToUpper() switch
            {
                "CCS" => ConnectorType.IEC_62196_T2_Combo,
                "CHADEMO" => ConnectorType.Chademo,
                "TYPE2" => ConnectorType.IEC_62196_T2,
                _ => ConnectorType.IEC_62196_T2
            };
        }

        private int? ParsePower(string powerOutput)
        {
            if (string.IsNullOrEmpty(powerOutput))
                return null;

            var numericPart = new string(powerOutput.Where(char.IsDigit).ToArray());
            if (int.TryParse(numericPart, out int power))
            {
                // If in kW, convert to W
                if (powerOutput.ToLower().Contains("kw"))
                    return power * 1000;
                return power;
            }
            return null;
        }

        private int? ParseVoltage(string powerOutput)
        {
            // Extract voltage if available, otherwise return default
            if (string.IsNullOrEmpty(powerOutput))
                return 400; // Default for 3-phase AC

            if (powerOutput.ToLower().Contains("dc"))
                return 400; // Typical DC voltage

            return 400; // Default voltage
        }

        private int? ParseAmperage(string powerOutput)
        {
            // Calculate amperage based on power
            var power = ParsePower(powerOutput);
            var voltage = ParseVoltage(powerOutput);
            
            if (power.HasValue && voltage.HasValue && voltage.Value > 0)
                return power.Value / voltage.Value;

            return 32; // Default amperage
        }

        #endregion
    }
}
