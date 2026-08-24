using System.Text.Json.Serialization;
using OCPI.Contracts;
using OCPI.Contracts.ChargingProfiles;
using OCPI.Enums.SmartCharging;

namespace OCPI.Core.Roaming.Services
{
    // ── Version response DTOs ──────────────────────────────────────────────────

    /// <summary>Single item returned by GET /versions</summary>
    public class OcpiVersionInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>Endpoint entry within a version details response</summary>
    public class OcpiEndpointEntry
    {
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>Full response returned by GET /versions/{version}</summary>
    public class OcpiVersionDetails
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("endpoints")]
        public List<OcpiEndpointEntry> Endpoints { get; set; } = new();
    }

    // ── Version service interface ──────────────────────────────────────────────

    /// <summary>
    /// Our own OCPI version service — replaces the NuGet IOcpiVersionService.
    /// Scans the assembly for all [OcpiEndpoint]-decorated controllers and assembles
    /// the OCPI-compliant version and endpoint data at runtime.
    /// </summary>
    public interface IOcpiVersionService
    {
        /// <summary>Returns the list of supported OCPI versions.</summary>
        List<OcpiVersionInfo> GetVersions();

        /// <summary>Returns version details (endpoint list) for the requested version, or null if unsupported.</summary>
        OcpiVersionDetails? GetVersionDetails(string version);
    }

    // ── Existing service interfaces ────────────────────────────────────────────

    /// <summary>
    /// Service interfaces for OCPI operations
    /// </summary>
    public interface IOcpiCredentialsService
    {
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential?> GetPartnerByTokenAsync(string token);
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential?> GetPartnerByCountryAndPartyAsync(string countryCode, string partyId, string roleId);
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential> CreateOrUpdatePartnerAsync(string token, string url, string countryCode, string partyId, string businessName, string role, string version, string? outboundToken = null);
        Task DeletePartnerAsync(string token);

        // ── A-token (pending registration) management ──────────────────────────
        Task<OCPP.Core.Database.OCPIDTO.OcpiPendingRegistration> IssueATokenAsync(string label, int expiryHours = 72);
        Task<OCPP.Core.Database.OCPIDTO.OcpiPendingRegistration?> GetPendingRegistrationByTokenAsync(string aToken);
        Task<List<OCPP.Core.Database.OCPIDTO.OcpiPendingRegistration>> GetAllPendingRegistrationsAsync();
        Task MarkATokenUsedAsync(int pendingId, int partnerCredentialId);
    }

    public interface IOcpiLocationService
    {
        Task<IEnumerable<OcpiLocation>> GetOurLocationsAsync(int offset, int limit);
        Task<int> GetOurLocationCountAsync();
        Task<OcpiLocation> GetOurLocationAsync(string locationId);
        Task<OcpiEvse> GetOurEvseAsync(string locationId, string evseUid);
        Task<OcpiConnector> GetOurConnectorAsync(string locationId, string evseUid, string connectorId);

        Task StorePartnerLocationAsync(int partnerCredentialId, OcpiLocation location);
        Task StorePartnerEvseAsync(int partnerLocationId, OcpiEvse evse);
        Task StorePartnerConnectorAsync(int partnerEvseId, OcpiConnector connector);

        /// <summary>Returns the database PK of a stored partner location, or null if not found.</summary>
        Task<int?> GetPartnerLocationDbIdAsync(string countryCode, string partyId, string locationId);
        /// <summary>Returns the database PK of a stored partner EVSE, or null if not found.</summary>
        Task<int?> GetPartnerEvseDbIdAsync(int partnerLocationId, string evseUid);

        /// <summary>Returns a stored partner location as an OCPI object, or null if not found.</summary>
        Task<OcpiLocation?> GetStoredPartnerLocationAsync(string countryCode, string partyId, string locationId);
        /// <summary>Returns a stored partner EVSE as an OCPI object, or null if not found.</summary>
        Task<OcpiEvse?> GetStoredPartnerEvseAsync(string countryCode, string partyId, string locationId, string evseUid);
        /// <summary>Returns a stored partner connector as an OCPI object, or null if not found.</summary>
        Task<OcpiConnector?> GetStoredPartnerConnectorAsync(string countryCode, string partyId, string locationId, string evseUid, string connectorId);
    }

    public interface IOcpiSessionService
    {
        Task StorePartnerSessionAsync(int partnerCredentialId, OcpiSession session);
        Task UpdatePartnerSessionAsync(string sessionId, OcpiSession session);
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerSession> GetPartnerSessionAsync(string sessionId);
    }

    public interface IOcpiCdrService
    {
        Task<string> CreateCdrAsync(OcpiCdr cdr, int? partnerCredentialId = null);
        Task<OcpiCdr?> GetCdrAsync(string cdrId);
        Task<List<OcpiCdr>> GetCdrsAsync(DateTime? from = null, DateTime? to = null, int offset = 0, int limit = 100);
        /// <summary>Returns the total number of CDRs matching the given date filters, for pagination headers.</summary>
        Task<int> GetCdrCountAsync(DateTime? from = null, DateTime? to = null);
    }

    public interface IOcpiTariffService
    {
        Task<List<OcpiTariff>> GetTariffsAsync(int offset = 0, int limit = 100);
        Task<int> GetTariffCountAsync();
        Task<OcpiTariff> GetTariffAsync(string countryCode, string partyId, string tariffId);
        Task<string> CreateOrUpdateTariffAsync(OcpiTariff tariff);
        /// <summary>Soft-deletes a partner-pushed tariff. Returns false if no matching tariff was found.</summary>
        Task<bool> DeleteTariffAsync(string countryCode, string partyId, string tariffId);
    }

    public interface IOcpiTokenService
    {
        Task<OcpiAuthorizationInfo> AuthorizeTokenAsync(string tokenUid, OcpiLocationReferences? locationReferences = null);
        Task StorePartnerTokenAsync(int partnerCredentialId, OcpiToken token);
        Task UpdatePartnerTokenAsync(string tokenUid, OcpiToken token);
        Task<OCPP.Core.Database.OCPIDTO.OcpiToken> GetPartnerTokenAsync(string tokenUid);
    }

    public interface IOcpiCommandService
    {
        Task<(CommandResponseType Result, string? SessionId)> HandleStartSessionAsync(OcpiStartSessionCommand command);
        Task<CommandResponseType> HandleStopSessionAsync(OcpiStopSessionCommand command);
        Task<CommandResponseType> HandleReserveNowAsync(OcpiReserveNowCommand command);
        Task<CommandResponseType> HandleCancelReservationAsync(OcpiCancelReservationCommand command);
        Task<CommandResponseType> HandleUnlockConnectorAsync(OcpiUnlockConnectorCommand command);

        /// <summary>
        /// Handles an async CommandResult POSTed back to our response_url by a partner CPO we
        /// (as eMSP) issued a command to. <paramref name="correlationId"/> is the trailing path
        /// segment of the response_url we constructed (authorization_reference for START_SESSION,
        /// the resolved session_id for STOP_SESSION).
        /// </summary>
        Task HandleCommandResultAsync(string commandType, string correlationId, OcpiCommandResult result);
    }

    public interface IOcpiChargingProfileService
    {
        Task<ChargingProfileResponseType> SetChargingProfileAsync(string sessionId, OcpiSetChargingProfileRequest request);
        Task<OcpiChargingProfile?> GetActiveChargingProfileAsync(string sessionId);
        Task<ChargingProfileResponseType> ClearChargingProfileAsync(string sessionId, string? responseUrl);
    }
}
