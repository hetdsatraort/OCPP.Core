# Backend Configuration Checklist

## ✅ CORS Configuration

Ensure your backend allows the Angular app to make requests with credentials.

### In `Startup.cs` or `Program.cs`:

```csharp
// Add CORS service
services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", builder =>
    {
        builder.WithOrigins("http://localhost:4200") // Angular dev server
               .AllowCredentials()                    // Important for cookies
               .AllowAnyHeader()
               .AllowAnyMethod()
               .SetIsOriginAllowed(origin => true);   // For development
    });
});

// In Configure method (before UseRouting)
app.UseCors("AllowAngularApp");
```

### For Production:

```csharp
services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", builder =>
    {
        builder.WithOrigins(
                   "http://localhost:4200",           // Development
                   "https://your-production-domain.com" // Production
               )
               .AllowCredentials()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});
```

## ✅ Cookie Configuration

### In `UserController.cs`:

The `SetTokenCookies` method should set cookies like this:

```csharp
private void SetTokenCookies(string accessToken, string refreshToken)
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Secure = false,        // Set to true in production with HTTPS
        SameSite = SameSiteMode.Lax,  // Or None if cross-domain
        Expires = DateTimeOffset.UtcNow.AddMinutes(15)
    };

    Response.Cookies.Append("accessToken", accessToken, cookieOptions);

    cookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(7);
    Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
}
```

### For Production (HTTPS):

```csharp
private void SetTokenCookies(string accessToken, string refreshToken)
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Secure = true,         // HTTPS only
        SameSite = SameSiteMode.None, // For cross-domain
        Expires = DateTimeOffset.UtcNow.AddMinutes(15)
    };

    Response.Cookies.Append("accessToken", accessToken, cookieOptions);

    cookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(7);
    Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
}
```

## ✅ JWT Configuration

### In `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "your-super-secret-key-min-32-characters",
    "Issuer": "OCPP.Core.Management",
    "Audience": "OCPP.Core.Users",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### In `Startup.cs`:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Configuration["Jwt:Issuer"],
            ValidAudience = Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Configuration["Jwt:SecretKey"])
            )
        };

        // For cookie-based auth
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["accessToken"];
                return Task.CompletedTask;
            }
        };
    });
```

## ✅ Database Models

Ensure these DTOs exist in your backend:

### LoginRequestDto.cs
```csharp
public class LoginRequestDto
{
    [Required]
    public string EmailOrPhone { get; set; }

    [Required]
    public string Password { get; set; }
}
```

### RegisterRequestDto.cs
```csharp
public class RegisterRequestDto
{
    [Required]
    public string FirstName { get; set; }

    [Required]
    public string LastName { get; set; }

    [Required]
    [EmailAddress]
    public string EMailID { get; set; }

    [Required]
    [Phone]
    public string PhoneNumber { get; set; }

    public string CountryCode { get; set; } = "+1";

    [Required]
    [MinLength(8)]
    public string Password { get; set; }

    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public string AddressLine3 { get; set; }
    public string State { get; set; }
    public string City { get; set; }
    public string PinCode { get; set; }
}
```

### AuthResponseDto.cs
```csharp
public class AuthResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public UserDto User { get; set; }
}

public class UserDto
{
    public string RecId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string EMailID { get; set; }
    public string PhoneNumber { get; set; }
    public string CountryCode { get; set; }
    public string UserRole { get; set; }
    public string ProfileCompleted { get; set; }
}
```

## ✅ API Endpoints Verification

Your UserController should have these endpoints:

1. **POST** `/api/user/register`
   - Request: `RegisterRequestDto`
   - Response: `AuthResponseDto`
   - Status: 200 (OK) or 400 (Bad Request)

2. **POST** `/api/user/login`
   - Request: `LoginRequestDto`
   - Response: `AuthResponseDto` + Cookies
   - Status: 200 (OK) or 401 (Unauthorized)

3. **POST** `/api/user/logout`
   - Requires: Authentication
   - Response: `AuthResponseDto`
   - Status: 200 (OK)

## ✅ HTTPS Configuration (Production)

### In `Program.cs`:

```csharp
// For production
app.UseHttpsRedirection();

// Enforce HTTPS
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps)
    {
        var httpsUrl = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(httpsUrl, permanent: true);
        return;
    }
    await next();
});
```

### SSL Certificate

```bash
# For development (if needed)
dotnet dev-certs https --trust
```

## ✅ Testing Backend Endpoints

Use Postman or curl to test:

### Register:
```bash
curl -X POST https://localhost:7161/api/user/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Test",
    "lastName": "User",
    "eMailID": "test@example.com",
    "phoneNumber": "1234567890",
    "countryCode": "+1",
    "password": "Test@123"
  }'
```

### Login:
```bash
curl -X POST https://localhost:7161/api/user/login \
  -H "Content-Type: application/json" \
  -c cookies.txt \
  -d '{
    "emailOrPhone": "test@example.com",
    "password": "Test@123"
  }'
```

### Logout:
```bash
curl -X POST https://localhost:7161/api/user/logout \
  -H "Content-Type: application/json" \
  -b cookies.txt
```

## ✅ Logging Configuration

Add logging to track authentication attempts:

```csharp
// In UserController
_logger.LogInformation($"Login attempt for: {request.EmailOrPhone}");
_logger.LogInformation($"User {user.EMailID} logged in successfully");
_logger.LogWarning($"Failed login attempt for: {request.EmailOrPhone}");
_logger.LogError(ex, "Error during user registration");
```

## ✅ Security Best Practices

1. **Password Hashing**
   ```csharp
   private string HashPassword(string password)
   {
       using var sha256 = SHA256.Create();
       var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
       return Convert.ToBase64String(hashedBytes);
   }
   
   private bool VerifyPassword(string password, string hashedPassword)
   {
       return HashPassword(password) == hashedPassword;
   }
   ```
   
   **Recommendation:** Use `BCrypt.Net` or `AspNetCore.Identity` for better password hashing

2. **Rate Limiting**
   ```csharp
   // Add rate limiting for login attempts
   services.AddRateLimiter(options => {
       options.AddFixedWindowLimiter("login", opt => {
           opt.Window = TimeSpan.FromMinutes(1);
           opt.PermitLimit = 5;
       });
   });
   ```

3. **Input Validation**
   - Always validate and sanitize input
   - Use `[Required]`, `[EmailAddress]`, `[Phone]` attributes
   - Implement custom validators for complex rules

## ✅ Error Responses

Ensure consistent error response format:

```csharp
// Bad Request
return BadRequest(new AuthResponseDto
{
    Success = false,
    Message = "Invalid request data"
});

// Unauthorized
return Unauthorized(new AuthResponseDto
{
    Success = false,
    Message = "Invalid credentials"
});

// Internal Server Error
return StatusCode(500, new AuthResponseDto
{
    Success = false,
    Message = "An error occurred during login"
});
```

## ✅ Database Connection

Verify your connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=OCPPCore;Trusted_Connection=True;"
  }
}
```

## 🧪 Quick Backend Test

### Checklist:
- [ ] Backend is running (usually https://localhost:7161)
- [ ] CORS is configured to allow http://localhost:4200
- [ ] Cookies are set with proper options
- [ ] JWT authentication is configured
- [ ] Database is accessible
- [ ] All DTOs are defined
- [ ] Endpoints return correct status codes
- [ ] Logging is enabled
- [ ] Password hashing is working
- [ ] Error handling is in place

## 📞 Troubleshooting

### Issue: "Access-Control-Allow-Origin" error
**Fix:** Check CORS configuration, ensure `AllowCredentials()` is set

### Issue: Cookies not received in browser
**Fix:** Check `SameSite` and `Secure` settings, verify CORS allows credentials

### Issue: 401 Unauthorized for protected routes
**Fix:** Verify JWT token is being read from cookie in `OnMessageReceived` event

### Issue: Database connection failed
**Fix:** Check connection string, ensure database is running

## ✅ Ready for Frontend Integration

Once all these are configured, your backend will work perfectly with the Angular frontend!

---

**Last Updated:** January 2026
**Backend Framework:** ASP.NET Core (.NET 6/7/8)
