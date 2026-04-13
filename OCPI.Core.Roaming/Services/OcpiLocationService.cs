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
                .Include(h => h.ChargingStations)
                .ThenInclude(s => s.ChargingGuns)
                .Where(h => h.Active == 1)
                .OrderBy(h => h.ChargingHubId)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return hubs.Select(h => MapToOcpiLocation(h, countryCode, partyId)).ToList();
        }

        public async Task<OcpiLocation> GetOurLocationAsync(string locationId)
        {
            var countryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var partyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var hub = await _dbContext.ChargingHubs
                .Include(h => h.ChargingStations)
                .ThenInclude(s => s.ChargingGuns)
                .FirstOrDefaultAsync(h => h.ChargingHubId == locationId && h.Active == 1);

            if (hub == null)
                return null;

            return MapToOcpiLocation(hub, countryCode, partyId);
        }

        public async Task<OcpiEvse> GetOurEvseAsync(string locationId, string evseUid)
        {
            var station = await _dbContext.ChargingStations
                .Include(s => s.ChargingGuns)
                .FirstOrDefaultAsync(s => s.ChargingStationId == evseUid && s.ChargingHubId == locationId);

            if (station == null)
                return null;

            return MapToOcpiEvse(station);
        }

        public async Task<OcpiConnector> GetOurConnectorAsync(string locationId, string evseUid, string connectorId)
        {
            var gun = await _dbContext.ChargingGuns
                .Include(g => g.ChargingStation)
                .FirstOrDefaultAsync(g => g.ChargingGunId == connectorId 
                    && g.ChargingStationId == evseUid 
                    && g.ChargingStation.ChargingHubId == locationId);

            if (gun == null)
                return null;

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
                existing.Country = location.Country.ToString();
                existing.Latitude = location.Coordinates?.Latitude;
                existing.Longitude = location.Coordinates?.Longitude;
                existing.LocationType = location.Type?.ToString();
                existing.LastUpdated = location.LastUpdated;
                
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
                    Country = location.Country.ToString(),
                    Latitude = location.Coordinates?.Latitude,
                    Longitude = location.Coordinates?.Longitude,
                    LocationType = location.Type?.ToString(),
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = location.LastUpdated
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
                existing.StatusDateTime = evse.StatusDateTime;
                existing.FloorLevel = evse.FloorLevel;
                existing.PhysicalReference = evse.PhysicalReference;
                existing.LastUpdated = evse.LastUpdated;
                
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
                    StatusDateTime = evse.StatusDateTime,
                    FloorLevel = evse.FloorLevel,
                    PhysicalReference = evse.PhysicalReference,
                    PartnerLocationId = partnerLocationId,
                    LastUpdated = evse.LastUpdated
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
                existing.LastUpdated = connector.LastUpdated;
                
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
                    LastUpdated = connector.LastUpdated
                };
                
                await _dbContext.OcpiPartnerConnectors.AddAsync(newConnector);
            }

            await _dbContext.SaveChangesAsync();
        }

        #region Mapping Methods

        private OcpiLocation MapToOcpiLocation(Database.EVCDTO.ChargingHub hub, string countryCode, string partyId)
        {
            return new OcpiLocation
            {
                CountryCode = Enum.Parse<CountryCode>(countryCode, true),
                PartyId = partyId,
                Id = hub.ChargingHubId,
                Publish = true,
                Name = hub.ChargingHubName,
                Address = hub.Address,
                City = hub.City ?? "Unknown",
                PostalCode = hub.PostalCode ?? "",
                Country = Enum.Parse<CountryCode>(countryCode, true),
                Coordinates = new OcpiGeolocation
                {
                    Latitude = hub.Latitude?.ToString() ?? "0",
                    Longitude = hub.Longitude?.ToString() ?? "0"
                },
                Type = LocationType.OnStreet,
                Evses = hub.ChargingStations?.Select(MapToOcpiEvse).ToList() ?? new List<OcpiEvse>(),
                Operator = new OcpiBusinessDetails
                {
                    Name = _configuration["OCPI:BusinessName"] ?? "EV Charging Platform"
                },
                LastUpdated = hub.UpdatedOn ?? hub.CreatedOn ?? DateTime.UtcNow
            };
        }

        private OcpiEvse MapToOcpiEvse(Database.EVCDTO.ChargingStation station)
        {
            var status = MapStatus(station.Status);
            
            return new OcpiEvse
            {
                Uid = station.ChargingStationId,
                EvseId = $"IN*CPO*E{station.ChargingStationId}",
                Status = status,
                StatusDateTime = station.UpdatedOn ?? DateTime.UtcNow,
                Connectors = station.ChargingGuns?.Select(MapToOcpiConnector).ToList() ?? new List<OcpiConnector>(),
                PhysicalReference = station.ChargingStationName,
                LastUpdated = station.UpdatedOn ?? station.CreatedOn ?? DateTime.UtcNow
            };
        }

        private OcpiConnector MapToOcpiConnector(Database.EVCDTO.ChargingGuns gun)
        {
            return new OcpiConnector
            {
                Id = gun.ChargingGunId,
                Standard = MapConnectorType(gun.ChargerType),
                Format = ConnectorFormat.Socket,
                PowerType = gun.ChargerType?.ToUpper().Contains("DC") == true ? PowerType.Dc : PowerType.Ac3Phase,
                MaxVoltage = ParseVoltage(gun.PowerOutput),
                MaxAmperage = ParseAmperage(gun.PowerOutput),
                MaxElectricPower = ParsePower(gun.PowerOutput),
                LastUpdated = gun.UpdatedOn ?? gun.CreatedOn ?? DateTime.UtcNow
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
