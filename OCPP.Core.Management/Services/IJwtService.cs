using OCPP.Core.Database.EVCDTO;
using System.Security.Claims;

namespace OCPP.Core.Management.Services
{
    public interface IJwtService
    {
        string GenerateAccessToken(Users user);
        RefreshToken GenerateRefreshToken(string ipAddress);
        ClaimsPrincipal ValidateToken(string token);
        string GetUserIdFromToken(string token);
    }
}
