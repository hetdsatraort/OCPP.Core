# JWT Authentication Migration - Complete Guide

## Overview

The OCPP.Core Management application has been successfully migrated from cookie-based authentication to JWT (JSON Web Token) authentication. This migration provides a unified authentication system for both the admin panel (Razor Pages) and the REST API.

## What Was Changed

### 1. Startup.cs - Complete Authentication Overhaul

**Before:** Cookie-based authentication with `AddCookie()`
**After:** JWT Bearer authentication with full token validation

Key changes:
- Replaced `CookieAuthenticationDefaults` with `JwtBearerDefaults`
- Added JWT token validation from both Authorization headers AND cookies
- Configured CORS to support API calls with credentials
- Added automatic redirect to login for unauthorized web requests
- Added token expiration headers for better error handling

```csharp
// JWT authentication now supports BOTH:
// 1. Authorization: Bearer <token> (for API calls)
// 2. accessToken cookie (for admin panel pages)
```

### 2. AccountController.cs - JWT-based Login

**Before:** Used `UserManager.SignIn()` with cookie authentication
**After:** Generates JWT tokens and stores them in HTTP-only cookies

Key features:
- Supports both database users (Users table) and config-based users (backward compatibility)
- Uses SHA256 password hashing
- Generates both access tokens (15 min) and refresh tokens (7 days)
- Stores refresh tokens in database for security
- Logs authentication events

### 3. UserController.cs - Cookie Settings Update

**Before:** Strict cookie settings that could block API calls
**After:** Relaxed cookie settings for development

Changes:
- `SameSite = Lax` (instead of Strict) - allows POST API calls to work
- `Secure = false` for development (set to true in production with HTTPS)

## How It Works

### For Admin Panel (Razor Pages)

1. User navigates to `/Account/Login`
2. User enters username/password
3. System validates credentials (database or config)
4. JWT tokens generated and stored in HTTP-only cookies
5. User redirected to admin panel
6. All subsequent requests automatically include cookies
7. JWT middleware validates token on every request

### For API Calls

1. Client calls `/api/user/login` with credentials
2. Server returns JWT tokens in cookies AND response body
3. Client can use either:
   - Cookies (automatic in browsers)
   - Authorization header: `Bearer <token>`
4. Protected endpoints verify JWT token
5. On token expiry, call `/api/user/refresh-token`

## Authentication Flow

```
???????????????
?   Login     ?
???????????????
       ?
       ?
???????????????????????
? Validate Credentials?
???????????????????????
       ?
       ?
???????????????????????
? Generate JWT Tokens ?
? - Access (15 min)   ?
? - Refresh (7 days)  ?
???????????????????????
       ?
       ?
???????????????????????
? Store in Cookies    ?
? & Database          ?
???????????????????????
       ?
       ?
???????????????????????
?  Authenticate User  ?
???????????????????????
```

## Token Validation Priority

1. **Authorization Header** (for API calls)
   - `Authorization: Bearer eyJhbGc...`
   
2. **Cookie** (for admin panel)
   - `accessToken` cookie

3. **Redirect** (for web pages)
   - Unauthorized ? `/Account/Login`

## Configuration

### appsettings.json

```json
{
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "OCPPCore",
    "Audience": "OCPPCoreUsers",
    "AccessTokenExpirationMinutes": "15",
    "RefreshTokenExpirationDays": "7"
  }
}
```

### Database Tables Required

1. **Users** - User accounts
2. **RefreshTokens** - Refresh token storage

## Why POST APIs Now Work

### The Problem
With `SameSite=Strict` and strict authentication, POST requests from external sources (like Postman, Angular apps) were being blocked or not receiving cookies.

### The Solution
1. **CORS Configuration** - Allows credentials from specified origins
2. **SameSite=Lax** - Allows cookies on cross-site POST requests
3. **Dual Token Support** - Can use either Authorization header OR cookies
4. **OnMessageReceived Event** - Reads tokens from multiple sources

## Testing the Authentication

### Admin Panel Login
```
URL: http://localhost:8082/Account/Login
Method: GET/POST
Credentials: admin / t3st (or any user from appsettings.json or Users table)
```

### API Login
```bash
curl -X POST http://localhost:8082/api/user/login \
  -H "Content-Type: application/json" \
  -d '{
    "emailOrPhone": "admin",
    "password": "t3st"
  }'
```

### API with Authorization Header
```bash
curl -X GET http://localhost:8082/api/user/profile \
  -H "Authorization: Bearer <your-jwt-token>"
```

### API with Cookies
```bash
curl -X GET http://localhost:8082/api/user/profile \
  -b "accessToken=<token-value>"
```

## Security Features

1. **HTTP-Only Cookies** - Prevents XSS attacks
2. **Token Expiration** - Access tokens expire in 15 minutes
3. **Refresh Tokens** - Long-lived tokens (7 days) for token renewal
4. **Token Revocation** - Refresh tokens can be revoked on logout/password change
5. **SHA256 Password Hashing** - Secure password storage
6. **IP Tracking** - Logs IP addresses for token generation/revocation
7. **Database Audit Trail** - All auth events logged in MessageLog

## Backward Compatibility

The system maintains backward compatibility with:
- Config-based users (from appsettings.json)
- Existing Users table structure
- All existing API endpoints

## Migration Checklist

? Startup.cs - JWT authentication configured
? AccountController.cs - JWT-based login
? UserController.cs - Cookie settings updated
? Token validation from headers and cookies
? CORS configuration for API access
? Redirect logic for unauthorized web requests
? Build successful - no compilation errors

## Common Issues & Solutions

### Issue: POST APIs return 401 Unauthorized
**Solution:** Ensure token is sent in Authorization header or cookies

### Issue: Admin panel redirects to login repeatedly
**Solution:** Check that accessToken cookie is being set and not blocked by browser

### Issue: CORS errors in browser console
**Solution:** Verify CORS policy includes your origin and AllowCredentials()

### Issue: Token expired errors
**Solution:** Call /api/user/refresh-token to get new tokens

## Next Steps

1. **Production Deployment:**
   - Set `Secure = true` in cookie options
   - Set `RequireHttpsMetadata = true` in JWT options
   - Use strong JWT secret key
   - Enable HTTPS

2. **Enhanced Security:**
   - Implement rate limiting on login endpoints
   - Add account lockout after failed attempts
   - Use BCrypt instead of SHA256 for passwords
   - Add two-factor authentication

3. **Monitoring:**
   - Monitor token refresh patterns
   - Track authentication failures
   - Set up alerts for suspicious activity

## API Documentation

Access Swagger documentation at:
```
http://localhost:8082/swagger
```

The Swagger UI now supports JWT authentication via the "Authorize" button.

---

**Status:** ? Complete and Working
**Build Status:** ? Successful
**Tested:** Admin Panel Login, API Endpoints (GET/POST)
**Date:** January 2025
