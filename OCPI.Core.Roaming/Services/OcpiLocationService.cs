using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
using System.Text.Json.Serialization;

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

        public async Task<IEnumerable<OcpiLocation>> GetOurLocationsAsync(int offset, int limit)
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

            var stationChargePointIds = stations
                .Where(s => !string.IsNullOrEmpty(s.ChargingPointId))
                .Select(s => s.ChargingPointId!)
                .Distinct()
                .ToList();

            var activeOcpiChargePointIds = stationChargePointIds.Any()
                ? await _dbContext.OcpiHostedSessions
                    .Where(s => s.Status == "ACTIVE" && stationChargePointIds.Contains(s.ChargePointId))
                    .Select(s => s.ChargePointId!)
                    .Distinct()
                    .ToListAsync()
                : [];

            var activeOcpiSet = new HashSet<string>(activeOcpiChargePointIds, StringComparer.OrdinalIgnoreCase);

            return hubs.Select(h => MapToOcpiLocation(h, stations, guns, countryCode, partyId, activeOcpiSet));
        }

        public async Task<int> GetOurLocationCountAsync()
        {
            return await _dbContext.ChargingHubs.Where(h => h.Active == 1).CountAsync();
        }

        public async Task<OcpiLocation> GetOurLocationAsync(string locationId)
        {
            var countryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var partyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var hub = await _dbContext.ChargingHubs
                .FirstOrDefaultAsync(h => h.RecId == locationId && h.Active == 1);

            if (hub == null)
                return new OcpiLocation();

            var stations = await _dbContext.ChargingStations
                .Where(s => s.Active == 1 && s.ChargingHubId == locationId)
                .ToListAsync();

            var guns = await _dbContext.ChargingGuns
                .Where(g => g.Active == 1 && stations.Select(s => s.RecId).Contains(g.ChargingStationId))
                .ToListAsync();

            var stationChargePointIds = stations
                .Where(s => !string.IsNullOrEmpty(s.ChargingPointId))
                .Select(s => s.ChargingPointId!)
                .Distinct()
                .ToList();

            var activeOcpiChargePointIds = stationChargePointIds.Count > 0
                ? await _dbContext.OcpiHostedSessions
                    .Where(s => s.Status == "ACTIVE" && stationChargePointIds.Contains(s.ChargePointId))
                    .Select(s => s.ChargePointId!)
                    .Distinct()
                    .ToListAsync()
                : [];

            var activeOcpiSet = new HashSet<string>(activeOcpiChargePointIds, StringComparer.OrdinalIgnoreCase);

            return MapToOcpiLocation(hub, stations, guns, countryCode, partyId, activeOcpiSet);
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
                .FirstOrDefaultAsync(l => l.CountryCode == location.CountryCode!.ToString()
                    && l.PartyId == location.PartyId
                    && l.LocationId == location.Id);

            if (existing != null)
            {
                existing.Name          = Trunc(location.Name, 255);
                existing.Address       = Trunc(location.Address, 500);
                existing.City          = Trunc(location.City, 100);
                existing.PostalCode    = Trunc(location.PostalCode, 20);
                existing.Country       = Trunc(location.Country?.ToString(), 3);
                existing.Latitude      = Trunc(location.Coordinates?.Latitude, 20);
                existing.Longitude     = Trunc(location.Coordinates?.Longitude, 20);
                existing.LocationType  = Trunc(location.Type?.ToString(), 50);
                existing.LastUpdated   = location.LastUpdated ?? DateTime.UtcNow;

                _dbContext.OcpiPartnerLocations.Update(existing);
            }
            else
            {
                var newLocation = new OcpiPartnerLocation
                {
                    CountryCode         = Trunc(location.CountryCode?.ToString(), 2),
                    PartyId             = Trunc(location.PartyId, 3),
                    LocationId          = Trunc(location.Id, 36),
                    Name                = Trunc(location.Name, 255),
                    Address             = Trunc(location.Address, 500),
                    City                = Trunc(location.City, 100),
                    PostalCode          = Trunc(location.PostalCode, 20),
                    Country             = Trunc(location.Country?.ToString(), 3),
                    Latitude            = Trunc(location.Coordinates?.Latitude, 20),
                    Longitude           = Trunc(location.Coordinates?.Longitude, 20),
                    LocationType        = Trunc(location.Type?.ToString(), 50),
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated         = location.LastUpdated ?? DateTime.UtcNow
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
                existing.EvseId            = Trunc(evse.EvseId, 48);
                existing.Status            = Trunc(evse.Status.ToString(), 50);
                existing.StatusDateTime    = evse.LastUpdated ?? DateTime.UtcNow;
                existing.FloorLevel        = Trunc(evse.FloorLevel, 10);
                existing.PhysicalReference = Trunc(evse.PhysicalReference, 50);
                existing.LastUpdated       = evse.LastUpdated ?? DateTime.UtcNow;

                _dbContext.OcpiPartnerEvses.Update(existing);
            }
            else
            {
                var newEvse = new OcpiPartnerEvse
                {
                    EvseUid           = Trunc(evse.Uid, 36),
                    EvseId            = Trunc(evse.EvseId, 48),
                    Status            = Trunc(evse.Status.ToString(), 50),
                    StatusDateTime    = evse.LastUpdated ?? DateTime.UtcNow,
                    FloorLevel        = Trunc(evse.FloorLevel, 10),
                    PhysicalReference = Trunc(evse.PhysicalReference, 50),
                    PartnerLocationId = partnerLocationId,
                    LastUpdated       = evse.LastUpdated ?? DateTime.UtcNow
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
                existing.Standard       = Trunc(connector.Standard.ToString(), 50);
                existing.Format         = Trunc(connector.Format.ToString(), 20);
                existing.PowerType      = Trunc(connector.PowerType.ToString(), 50);
                existing.MaxVoltage     = connector.MaxVoltage;
                existing.MaxAmperage    = connector.MaxAmperage;
                existing.MaxElectricPower = connector.MaxElectricPower;
                existing.LastUpdated    = connector.LastUpdated ?? DateTime.UtcNow;

                _dbContext.OcpiPartnerConnectors.Update(existing);
            }
            else
            {
                var newConnector = new OcpiPartnerConnector
                {
                    ConnectorId       = Trunc(connector.Id, 36),
                    Standard          = Trunc(connector.Standard.ToString(), 50),
                    Format            = Trunc(connector.Format.ToString(), 20),
                    PowerType         = Trunc(connector.PowerType.ToString(), 50),
                    MaxVoltage        = connector.MaxVoltage,
                    MaxAmperage       = connector.MaxAmperage,
                    MaxElectricPower  = connector.MaxElectricPower,
                    PartnerEvseId     = partnerEvseId,
                    LastUpdated       = connector.LastUpdated ?? DateTime.UtcNow
                };

                await _dbContext.OcpiPartnerConnectors.AddAsync(newConnector);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<int?> GetPartnerLocationDbIdAsync(string countryCode, string partyId, string locationId)
        {
            var loc = await _dbContext.OcpiPartnerLocations
                .Where(l => l.CountryCode == countryCode && l.PartyId == partyId && l.LocationId == locationId)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();
            return loc;
        }

        public async Task<int?> GetPartnerEvseDbIdAsync(int partnerLocationId, string evseUid)
        {
            var evse = await _dbContext.OcpiPartnerEvses
                .Where(e => e.PartnerLocationId == partnerLocationId && e.EvseUid == evseUid)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();
            return evse;
        }

        private static string? Trunc(string? value, int maxLength) =>
            value is null ? null : value.Length <= maxLength ? value : value[..maxLength];

        #region Mapping Methods

        private OcpiLocation MapToOcpiLocation(
            OCPP.Core.Database.EVCDTO.ChargingHub hub,
            List<OCPP.Core.Database.EVCDTO.ChargingStation> stations,
            List<OCPP.Core.Database.EVCDTO.ChargingGuns> guns,
            string countryCode,
            string partyId,
            HashSet<string> activeOcpiSessions)
        {
            return new OcpiLocation
            {
                CountryCode = "IN",
                PartyId = partyId,
                Id = hub.RecId,
                Publish = true,
                Name = hub.ChargingHubName,
                Address = hub.AddressLine1 + (string.IsNullOrEmpty(hub.AddressLine2) ? "" : ", " + hub.AddressLine2) + (string.IsNullOrEmpty(hub.City) ? "" : ", " + hub.City),
                City = hub.City ?? "Unknown",
                PostalCode = hub.Pincode ?? "",
                Country = "India",
                Coordinates = new OcpiGeolocation
                {
                    Latitude = hub.Latitude?.ToString() ?? "0",
                    Longitude = hub.Longitude?.ToString() ?? "0"
                },
                Type = LocationType.OnStreet,
                Evses = stations.Where(s => s.ChargingHubId == hub.RecId)
                                .Select(s => MapToOcpiEvse(s, guns, activeOcpiSessions))
                                .ToList(),
                Operator = new OcpiBusinessDetails
                {
                    Name = _configuration["OCPI:BusinessName"] ?? "EV Charging Platform"
                },
                LastUpdated = hub.UpdatedOn
            };
        }

        private OcpiEvse MapToOcpiEvse(
            OCPP.Core.Database.EVCDTO.ChargingStation station,
            List<OCPP.Core.Database.EVCDTO.ChargingGuns> guns,
            HashSet<string>? activeOcpiSessions = null)
        {
            var chargePointId = station.ChargingPointId ?? station.RecId;
            var chargePoint = _dbContext.ChargePoints.FirstOrDefault(cp => cp.ChargePointId == chargePointId);

            EvseStatus status;
            if (activeOcpiSessions?.Contains(chargePointId) == true)
            {
                // There is an active OCPI partner session on this charger — it is definitively
                // in use, so override whatever GunStatusSyncService last wrote to ChargerStatus.
                status = EvseStatus.Charging;
            }
            else
            {
                status = MapStatus(guns.FirstOrDefault(g => g.ChargingStationId == station.RecId)?.ChargerStatus ?? "UNKNOWN");
            }

            return new OcpiEvse
            {
                Uid = station.RecId,
                EvseId = $"IN*CPO*E{station.RecId}",
                Status = status,
                StatusSchedule = null,
                Connectors = guns?.Where(g => g.ChargingStationId == station.RecId).Select(MapToOcpiConnector).ToList() ?? [],
                PhysicalReference = chargePoint?.Name ?? "Station " + station.ChargingPointId,
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
                "UNAVAILABLE" => EvseStatus.Blocked,
                "FAULTED" => EvseStatus.Blocked,
                "OFFLINE" => EvseStatus.OutOfOrder,
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

    public class OcpiLocation
    {
        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("party_id")]
        public string? PartyId { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("publish")]
        public bool? Publish { get; set; }

        [JsonPropertyName("publish_allowed_to")]
        public IEnumerable<OcpiPublishTokenType>? PublishAllowedTo { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("coordinates")]
        public OcpiGeolocation? Coordinates { get; set; }

        [JsonPropertyName("related_locations")]
        public IEnumerable<OcpiAdditionalGeolocation>? RelatedLocations { get; set; }

        [JsonPropertyName("parking_type")]
        public ParkingType? ParkingType { get; set; }

        [JsonPropertyName("evses")]
        public IEnumerable<OcpiEvse>? Evses { get; set; }

        [JsonPropertyName("directions")]
        public IEnumerable<OcpiDisplayText>? Directions { get; set; }

        [JsonPropertyName("operator")]
        public OcpiBusinessDetails? Operator { get; set; }

        [JsonPropertyName("suboperator")]
        public OcpiBusinessDetails? Suboperator { get; set; }

        [JsonPropertyName("owner")]
        public OcpiBusinessDetails? Owner { get; set; }

        [JsonPropertyName("facilities")]
        public IEnumerable<string>? Facilities { get; set; }

        [JsonPropertyName("time_zone")]
        public string? TimeZone { get; set; }

        [JsonPropertyName("opening_times")]
        public OcpiHours? OpeningTimes { get; set; }

        [JsonPropertyName("charging_when_closed")]
        public bool? ChargingWhenClosed { get; set; }

        [JsonPropertyName("images")]
        public IEnumerable<OcpiImage>? Images { get; set; }

        [JsonPropertyName("energy_mix")]
        public OcpiEnergyMix? EnergyMix { get; set; }

        [JsonPropertyName("last_updated")]
        public DateTime? LastUpdated { get; set; }

        [JsonPropertyName("type")]
        public LocationType? Type { get; set; }
    }
}
