using OCPI.Core.Roaming.Models.OCPI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCPI.Core.Roaming.Services.Interfaces
{
    /// <summary>
    /// Service interface for OCPI Location operations
    /// </summary>
    public interface IOcpiLocationService
    {
        /// <summary>
        /// Get all locations with optional country code and party ID filters
        /// </summary>
        Task<List<OcpiLocationDto>> GetLocationsAsync(string? countryCode = null, string? partyId = null);

        /// <summary>
        /// Get a specific location by ID
        /// </summary>
        Task<OcpiLocationDto?> GetLocationByIdAsync(string locationId);

        /// <summary>
        /// Create or update a location
        /// </summary>
        Task<OcpiLocationDto> CreateOrUpdateLocationAsync(OcpiLocationDto location);

        /// <summary>
        /// Delete a location by ID
        /// </summary>
        Task<bool> DeleteLocationAsync(string locationId);

        /// <summary>
        /// Get a specific EVSE within a location
        /// </summary>
        Task<OcpiEvseDto?> GetEvseAsync(string locationId, string evseUid);

        /// <summary>
        /// Get a specific connector within an EVSE
        /// </summary>
        Task<OcpiConnectorDto?> GetConnectorAsync(string locationId, string evseUid, string connectorId);
    }

    /// <summary>
    /// Service interface for OCPI Session operations
    /// </summary>
    public interface IOcpiSessionService
    {
        /// <summary>
        /// Get all sessions with optional country code and party ID filters
        /// </summary>
        Task<List<OcpiSessionDto>> GetSessionsAsync(string? countryCode = null, string? partyId = null);

        /// <summary>
        /// Get a specific session by ID
        /// </summary>
        Task<OcpiSessionDto?> GetSessionByIdAsync(string sessionId);

        /// <summary>
        /// Create or update a session
        /// </summary>
        Task<OcpiSessionDto> CreateOrUpdateSessionAsync(OcpiSessionDto session);

        /// <summary>
        /// Start a new charging session
        /// </summary>
        Task<OcpiSessionDto> StartSessionAsync(StartSessionRequestDto request);

        /// <summary>
        /// Stop an active charging session
        /// </summary>
        Task<OcpiSessionDto> StopSessionAsync(StopSessionRequestDto request);
    }

    /// <summary>
    /// Service interface for OCPI CDR operations
    /// </summary>
    public interface IOcpiCdrService
    {
        /// <summary>
        /// Get all CDRs with optional country code and party ID filters
        /// </summary>
        Task<List<OcpiCdrDto>> GetCdrsAsync(string? countryCode = null, string? partyId = null);

        /// <summary>
        /// Get a specific CDR by ID
        /// </summary>
        Task<OcpiCdrDto?> GetCdrByIdAsync(string cdrId);

        /// <summary>
        /// Create a new CDR
        /// </summary>
        Task<OcpiCdrDto> CreateCdrAsync(OcpiCdrDto cdr);
    }

    /// <summary>
    /// Service interface for OCPI Token operations
    /// </summary>
    public interface IOcpiTokenService
    {
        /// <summary>
        /// Get all tokens with optional country code and party ID filters
        /// </summary>
        Task<List<OcpiTokenDto>> GetTokensAsync(string? countryCode = null, string? partyId = null);

        /// <summary>
        /// Get a specific token by UID
        /// </summary>
        Task<OcpiTokenDto?> GetTokenByUidAsync(string tokenUid);

        /// <summary>
        /// Create or update a token
        /// </summary>
        Task<OcpiTokenDto> CreateOrUpdateTokenAsync(OcpiTokenDto token);

        /// <summary>
        /// Authorize a token for charging at a specific location
        /// </summary>
        Task<AuthorizationInfo> AuthorizeTokenAsync(TokenAuthorizationRequestDto request);
    }

    /// <summary>
    /// Service interface for OCPI Tariff operations
    /// </summary>
    public interface IOcpiTariffService
    {
        /// <summary>
        /// Get all tariffs with optional country code and party ID filters
        /// </summary>
        Task<List<OcpiTariffDto>> GetTariffsAsync(string? countryCode = null, string? partyId = null);

        /// <summary>
        /// Get a specific tariff by ID
        /// </summary>
        Task<OcpiTariffDto?> GetTariffByIdAsync(string tariffId);

        /// <summary>
        /// Create or update a tariff
        /// </summary>
        Task<OcpiTariffDto> CreateOrUpdateTariffAsync(OcpiTariffDto tariff);

        /// <summary>
        /// Delete a tariff by ID
        /// </summary>
        Task<bool> DeleteTariffAsync(string tariffId);
    }

    /// <summary>
    /// Service interface for OCPI Credentials operations
    /// </summary>
    public interface IOcpiCredentialsService
    {
        /// <summary>
        /// Get current credentials
        /// </summary>
        Task<OcpiCredentialsResponseDto> GetCredentialsAsync();

        /// <summary>
        /// Register new partner credentials
        /// </summary>
        Task<OcpiCredentialsResponseDto> RegisterCredentialsAsync(OcpiCredentialsRequestDto request);

        /// <summary>
        /// Update existing credentials
        /// </summary>
        Task<OcpiCredentialsResponseDto> UpdateCredentialsAsync(OcpiCredentialsRequestDto request);

        /// <summary>
        /// Delete credentials (unregister partner)
        /// </summary>
        Task<bool> DeleteCredentialsAsync();
    }

    /// <summary>
    /// Service interface for OCPI Version operations
    /// </summary>
    public interface IOcpiVersionService
    {
        /// <summary>
        /// Get list of supported OCPI versions
        /// </summary>
        Task<List<OcpiVersionDto>> GetVersionsAsync();

        /// <summary>
        /// Get detailed endpoint information for a specific version
        /// </summary>
        Task<OcpiVersionDetailsDto> GetVersionDetailsAsync(string version);
    }
}
