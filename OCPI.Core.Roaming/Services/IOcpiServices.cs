using OCPI.Contracts;
using OCPI.Contracts.ChargingProfiles;
using OCPI.Enums.SmartCharging;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// Service interfaces for OCPI operations
    /// </summary>
    public interface IOcpiCredentialsService
    {
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential> GetPartnerByTokenAsync(string token);
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential> GetPartnerByCountryAndPartyAsync(string countryCode, string partyId);
        Task<OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential> CreateOrUpdatePartnerAsync(string token, string url, string countryCode, string partyId, string businessName, string role, string version);
        Task DeletePartnerAsync(string token);
    }

    public interface IOcpiLocationService
    {
        Task<List<OcpiLocation>> GetOurLocationsAsync(int offset, int limit);
        Task<OcpiLocation> GetOurLocationAsync(string locationId);
        Task<OcpiEvse> GetOurEvseAsync(string locationId, string evseUid);
        Task<OcpiConnector> GetOurConnectorAsync(string locationId, string evseUid, string connectorId);
        
        Task StorePartnerLocationAsync(int partnerCredentialId, OcpiLocation location);
        Task StorePartnerEvseAsync(int partnerLocationId, OcpiEvse evse);
        Task StorePartnerConnectorAsync(int partnerEvseId, OcpiConnector connector);
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
    }

    public interface IOcpiTariffService
    {
        Task<List<OcpiTariff>> GetTariffsAsync(int offset = 0, int limit = 100);
        Task<OcpiTariff> GetTariffAsync(string tariffId);
        Task<string> CreateOrUpdateTariffAsync(OcpiTariff tariff);
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
        Task<CommandResponseType> HandleStartSessionAsync(OcpiStartSessionCommand command);
        Task<CommandResponseType> HandleStopSessionAsync(OcpiStopSessionCommand command);
        Task<CommandResponseType> HandleReserveNowAsync(OcpiReserveNowCommand command);
        Task<CommandResponseType> HandleCancelReservationAsync(OcpiCancelReservationCommand command);
        Task<CommandResponseType> HandleUnlockConnectorAsync(OcpiUnlockConnectorCommand command);
    }

    public interface IOcpiChargingProfileService
    {
        Task<ChargingProfileResponseType> SetChargingProfileAsync(string sessionId, OcpiSetChargingProfileRequest request);
        Task<OcpiChargingProfile?> GetActiveChargingProfileAsync(string sessionId);
        Task<ChargingProfileResponseType> ClearChargingProfileAsync(string sessionId, string? responseUrl);
    }
}
