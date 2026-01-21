# Dual Authentication Configuration Guide

## Overview
The OCPP.Core Management application now supports **dual authentication**:
- **Cookie Authentication** - For the management panel (web interface)
- **JWT Bearer Authentication** - For API endpoints (mobile/web external calls)

## Authentication Schemes

### 1. Cookie Authentication (Management Panel)
- **Purpose**: Traditional web application login for admin panel
- **Scheme**: `CookieAuthenticationDefaults.AuthenticationScheme`
- **Used by**: MVC Controllers, Razor views, admin interface
- **Login Flow**: 
  1. User navigates to `/Account/Login`
  2. Submits credentials
  3. Cookie is set on successful authentication
  4. Subsequent requests include cookie automatically
- **Expiration**: 8 hours with sliding expiration

### 2. JWT Bearer Authentication (API)
- **Purpose**: Token-based auth for external mobile/web applications
- **Scheme**: `JwtBearerDefaults.AuthenticationScheme`
- **Used by**: API Controllers (endpoints under `/api/*`)
- **Login Flow**:
  1. Client calls `/api/auth/login` with credentials
  2. Server returns JWT access token + refresh token
  3. Client includes token in `Authorization: Bearer {token}` header
  4. Server validates token on each request
- **Expiration**: 
  - Access Token: 15 minutes
  - Refresh Token: 7 days

## Authorization Policies

Three authorization policies are configured:

### Default Policy (Cookie)
```csharp
[Authorize] // Uses cookie authentication by default
public class HomeController : Controller
{
    // Management panel actions
}
```

### ApiPolicy (JWT Bearer)
```csharp
[Authorize(Policy = "ApiPolicy")] // Uses JWT authentication
public class ChargingHubsController : ControllerBase
{
    // API endpoints for external clients
}
```

### CombinedPolicy (Both)
```csharp
[Authorize(Policy = "CombinedPolicy")] // Accepts both cookie AND JWT
public class SharedController : ControllerBase
{
    // Accessible from both management panel and external API
}
```

## How to Apply Authentication to Controllers

### For API Controllers (External Clients)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OCPP.Core.Management.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")] // ← JWT Bearer only
    public class ChargingHubsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAll()
        {
            // Requires valid JWT token in Authorization header
            return Ok(hubs);
        }
        
        [AllowAnonymous] // ← Public endpoint, no auth required
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // Login endpoint - no auth needed
            return Ok(new { token = jwtToken });
        }
    }
}
```

### For Management Panel Controllers

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OCPP.Core.Management.Controllers
{
    [Authorize] // ← Uses default cookie authentication
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Requires logged-in admin with valid cookie
            return View();
        }
    }
}
```

## API Client Implementation

### Angular/Web Client

```typescript
// Login
const response = await fetch('https://localhost:7161/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'user', password: 'pass' })
});
const { accessToken, refreshToken } = await response.json();

// Store tokens
localStorage.setItem('accessToken', accessToken);
localStorage.setItem('refreshToken', refreshToken);

// API calls with JWT
const data = await fetch('https://localhost:7161/api/charginghubs', {
  headers: {
    'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
  }
});
```

### Mobile Client (Ionic/Capacitor)

```typescript
import { HttpClient, HttpHeaders } from '@angular/common/http';

export class ApiService {
  private baseUrl = 'https://localhost:7161/api';
  
  constructor(private http: HttpClient) {}
  
  login(username: string, password: string) {
    return this.http.post(`${this.baseUrl}/auth/login`, 
      { username, password });
  }
  
  getChargingHubs() {
    const token = localStorage.getItem('accessToken');
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`
    });
    return this.http.get(`${this.baseUrl}/charginghubs`, { headers });
  }
}
```

## CORS Configuration

CORS is configured to allow external API calls from:
- `http://localhost:4200` (Angular dev)
- `https://localhost:4200`
- `http://localhost:8100` (Ionic dev)
- `https://localhost:8100`

**Add your production domains** to the CORS policy in `Startup.cs`:

```csharp
services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "https://your-production-domain.com" // ← Add here
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

## JWT Configuration

JWT settings are in `appsettings.json`:

```json
{
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!ChangeThisInProduction",
    "Issuer": "OCPPCore",
    "Audience": "OCPPCoreUsers",
    "AccessTokenExpirationMinutes": "15",
    "RefreshTokenExpirationDays": "7"
  }
}
```

**Important**: Change the `Secret` in production to a strong, unique value.

## Error Handling

### 401 Unauthorized
- **Cookie Auth**: Redirects to `/Account/Login` (for web pages)
- **Cookie Auth + API**: Returns `401` status code (for `/api/*` paths)
- **JWT Auth**: Returns `401` with `Token-Expired: true` header if token expired

### 403 Forbidden
- **Cookie Auth**: Redirects to `/Account/AccessDenied` (for web pages)
- **Cookie Auth + API**: Returns `403` status code (for `/api/*` paths)
- **JWT Auth**: Returns `403` if user lacks required permissions

## Testing Authentication

### Test Management Panel (Cookie)
1. Navigate to `https://localhost:7161`
2. Login with admin credentials
3. Access admin pages - cookie is sent automatically

### Test API (JWT)
1. Use Postman/cURL to login:
   ```bash
   curl -X POST https://localhost:7161/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"your-password"}'
   ```
2. Copy the `accessToken` from response
3. Make API call with token:
   ```bash
   curl https://localhost:7161/api/charginghubs \
     -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
   ```

## Swagger/OpenAPI

Swagger UI is configured with JWT Bearer authentication:
- Access: `https://localhost:7161/swagger`
- Click **Authorize** button
- Enter: `Bearer YOUR_TOKEN`
- Test API endpoints directly from Swagger

## Migration Checklist

- [ ] Update all API controllers to use `[Authorize(Policy = "ApiPolicy")]`
- [ ] Ensure Auth endpoints (login/register) use `[AllowAnonymous]`
- [ ] Update CORS origins with production domains
- [ ] Change JWT secret in production `appsettings.json`
- [ ] Test both authentication flows (cookie + JWT)
- [ ] Update client applications to use JWT tokens
- [ ] Implement token refresh logic in clients

## Security Best Practices

1. **HTTPS Only**: Always use HTTPS in production
2. **Strong Secrets**: Use cryptographically strong JWT secrets (32+ chars)
3. **Token Storage**: 
   - Web: Use httpOnly cookies or secure localStorage
   - Mobile: Use secure storage (Keychain/Keystore)
4. **Token Rotation**: Implement refresh token rotation
5. **CORS**: Restrict origins to known domains only
6. **Rate Limiting**: Add rate limiting to auth endpoints
7. **Audit Logging**: Log all authentication attempts

## Troubleshooting

### Issue: API returns 401 despite valid cookie
**Solution**: API endpoints should use JWT, not cookies. Apply `[Authorize(Policy = "ApiPolicy")]` to controller.

### Issue: Management panel redirects to login constantly
**Solution**: Ensure management controllers use default authorization (cookie). Don't apply `ApiPolicy`.

### Issue: CORS errors from mobile app
**Solution**: Add mobile app origin to CORS policy and ensure `AllowCredentials()` is set.

### Issue: Token expired errors
**Solution**: Implement token refresh flow in client application using refresh token.

### Issue: 302 redirects on API calls
**Solution**: This is fixed - API paths now return 401/403 instead of redirecting.
