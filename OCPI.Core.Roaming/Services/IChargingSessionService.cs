using OCPI.Contracts;

namespace OCPI.Core.Roaming.Services
{
    public interface IChargingSessionService
    {
        Task<List<OcpiSession>> GetActiveOcpiSessionsAsync();
        Task<OcpiSession?> GetOcpiSessionAsync(string sessionId);
        Task<bool> PushSessionUpdateToPartnersAsync(string sessionId);
    }
}