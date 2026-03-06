# JWT Authentication - Quick Reference

## ?? Quick Start

### Admin Panel Login
- **URL:** `http://localhost:8082/Account/Login`
- **Credentials:** `admin` / `t3st` (from appsettings.json)

### API Login
```bash
POST /api/user/login
{
  "emailOrPhone": "admin",
  "password": "t3st"
}
```

## ?? Token Usage

### Option 1: Cookies (Automatic)
Tokens are automatically stored in HTTP-only cookies after login.
No additional headers needed!

### Option 2: Authorization Header
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

## ?? API Endpoints

### Authentication
- `POST /api/user/login` - Login and get tokens
- `POST /api/user/logout` - Logout and revoke tokens
- `POST /api/user/refresh-token` - Refresh expired access token
- `POST /api/user/register` - Register new user

### User Profile
- `GET /api/user/profile` - Get current user profile
- `PUT /api/user/profile-update` - Update user profile
- `DELETE /api/user/user-delete` - Delete user account

### Password Management
- `POST /api/user/reset-password` - Reset password (requires old password)

## ?? Configuration

### appsettings.json
```json
{
  "JwtSettings": {
    "Secret": "Your-Secret-Key-32-Chars-Min",
    "Issuer": "OCPPCore",
    "Audience": "OCPPCoreUsers",
    "AccessTokenExpirationMinutes": "15",
    "RefreshTokenExpirationDays": "7"
  }
}
```

## ?? Token Lifetimes

| Token Type | Lifetime | Purpose |
|------------|----------|---------|
| Access Token | 15 minutes | API authentication |
| Refresh Token | 7 days | Token renewal |

## ??? Security Features

- ? HTTP-Only cookies (XSS protection)
- ? Token expiration
- ? Refresh token rotation
- ? Token revocation on logout
- ? SHA256 password hashing
- ? IP address logging
- ? Audit trail

## ?? Example: Using the API

### JavaScript/Fetch
```javascript
// Login
const response = await fetch('http://localhost:8082/api/user/login', {
  method: 'POST',
  credentials: 'include', // Important!
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    emailOrPhone: 'admin',
    password: 't3st'
  })
});

// Get Profile (cookies sent automatically)
const profile = await fetch('http://localhost:8082/api/user/profile', {
  credentials: 'include'
});
```

### cURL
```bash
# Login and save cookies
curl -X POST http://localhost:8082/api/user/login \
  -H "Content-Type: application/json" \
  -c cookies.txt \
  -d '{"emailOrPhone":"admin","password":"t3st"}'

# Use saved cookies
curl -X GET http://localhost:8082/api/user/profile \
  -b cookies.txt
```

### Postman
1. **Login:** POST to `/api/user/login`
2. **Enable cookies:** Settings ? Cookies ? Enable cookie capturing
3. **Automatic:** Cookies are automatically included in subsequent requests

## ?? Troubleshooting

### POST APIs return 401
- ? Check Authorization header or cookies are sent
- ? Verify token hasn't expired
- ? Check CORS configuration

### Cookies not being set
- ? Check `credentials: 'include'` in fetch
- ? Verify browser allows third-party cookies
- ? Check SameSite cookie settings

### Token expired
- ? Call `/api/user/refresh-token` endpoint
- ? Or re-login to get new tokens

## ?? Token Refresh Flow

```
1. Access token expires (15 min)
2. API returns 401 with Token-Expired header
3. Call /api/user/refresh-token
4. Get new access + refresh tokens
5. Retry original request
```

## ?? CORS Configuration

Allowed Origins (development):
- `http://localhost:4200` (Angular)
- `http://localhost:8082` (Self)

For production, update `Startup.cs` with your domain.

## ?? User Roles

| Role | Access Level |
|------|--------------|
| Administrator | Full access |
| Admin | Full access |
| User | Standard access |

## ?? Database Tables

### Users
Stores user accounts and credentials

### RefreshTokens
Stores active refresh tokens for security

## ?? Swagger UI

Access at: `http://localhost:8082/swagger`

**To test with Swagger:**
1. Click "Authorize" button
2. Enter: `Bearer <your-token>`
3. Click "Authorize"
4. Make API calls

## ? Migration Status

- ? Cookie authentication ? JWT authentication
- ? Admin panel login working
- ? API authentication working
- ? GET endpoints working
- ? POST endpoints working
- ? Token refresh working
- ? Build successful

---

**Need Help?** Check `JWT_MIGRATION_GUIDE.md` for detailed information
