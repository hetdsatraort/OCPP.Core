# OTP-Based Authentication System - Documentation

## Overview
This document describes the SMS-based OTP (One-Time Password) authentication system implemented for the EV Charging platform. The system provides secure, passwordless authentication using phone numbers.

## Architecture

### Components

1. **Database Entity:** `OtpValidation` - Stores OTP sessions with AuthID tracking
2. **DTOs:** Request/Response models for OTP operations
3. **API Endpoints:** Send, Verify, and Resend OTP
4. **SMS Integration:** Placeholder for SMS gateway (to be implemented)

### Flow Diagram

```
┌─────────────┐         ┌──────────────┐         ┌─────────────┐
│   Mobile    │         │  API Server  │         │  Database   │
│     App     │         │              │         │             │
└──────┬──────┘         └──────┬───────┘         └──────┬──────┘
       │                       │                        │
       │  1. Send OTP          │                        │
       │──────────────────────>│                        │
       │   (Phone Number)      │                        │
       │                       │  2. Generate OTP       │
       │                       │     Create AuthID      │
       │                       │──────────────────────>│
       │                       │                        │
       │                       │  3. Store OTP Record   │
       │                       │<──────────────────────│
       │                       │                        │
       │                       │  4. Send SMS           │
       │                       │────> [SMS Gateway]     │
       │                       │                        │
       │  5. Return AuthID     │                        │
       │<──────────────────────│                        │
       │                       │                        │
       │  6. Verify OTP        │                        │
       │──────────────────────>│                        │
       │  (AuthID + OTP Code)  │                        │
       │                       │  7. Validate OTP       │
       │                       │──────────────────────>│
       │                       │                        │
       │                       │  8. Check User Exists  │
       │                       │──────────────────────>│
       │                       │                        │
       │                       │  9. Generate JWT       │
       │                       │     (if existing user) │
       │                       │                        │
       │ 10. Return Token/     │                        │
       │     Registration Flag │                        │
       │<──────────────────────│                        │
       │                       │                        │
```

## API Endpoints

### Base URL
```
https://your-domain.com/api/User
```

---

### 1. Send OTP

**Endpoint:** `POST /api/User/send-otp`

**Authorization:** None required (AllowAnonymous)

**Request Body:**
```json
{
  "phoneNumber": "9876543210",
  "countryCode": "+91",
  "purpose": "Login"
}
```

**Request Parameters:**

| Field | Type | Required | Description | Validation |
|-------|------|----------|-------------|------------|
| `phoneNumber` | string | ✅ Yes | Phone number without country code | 10 digits, regex: `^\d{10}$` |
| `countryCode` | string | ❌ No | Country code (default: +91) | - |
| `purpose` | string | ❌ No | Purpose: Login, Registration, PasswordReset (default: Login) | - |

**Response:**
```json
{
  "success": true,
  "message": "OTP sent successfully",
  "authId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "maskedPhoneNumber": "+91-98765*****",
  "expiresInSeconds": 300
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Whether OTP was sent successfully |
| `message` | string | Status message |
| `authId` | string | Unique Auth ID for this OTP session (required for verification) |
| `maskedPhoneNumber` | string | Masked phone number for display |
| `expiresInSeconds` | int | OTP validity duration (300 = 5 minutes) |

**Rate Limiting:**
- Maximum 3 OTP requests per phone number per 10 minutes
- Returns error: "Too many OTP requests. Please try again later."

**Error Responses:**

```json
{
  "success": false,
  "message": "Invalid request data"
}
```

```json
{
  "success": false,
  "message": "Too many OTP requests. Please try again later."
}
```

---

### 2. Verify OTP

**Endpoint:** `POST /api/User/verify-otp`

**Authorization:** None required (AllowAnonymous)

**Request Body:**
```json
{
  "authId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "otpCode": "123456",
  "phoneNumber": "9876543210"
}
```

**Request Parameters:**

| Field | Type | Required | Description | Validation |
|-------|------|----------|-------------|------------|
| `authId` | string | ✅ Yes | Auth ID from send-otp response | - |
| `otpCode` | string | ✅ Yes | 6-digit OTP code | Regex: `^\d{6}$` |
| `phoneNumber` | string | ✅ Yes | Phone number (for validation) | - |

**Response (Existing User):**
```json
{
  "success": true,
  "message": "Login successful",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "a1b2c3d4e5f6g7h8i9j0...",
  "isNewUser": false,
  "user": {
    "userId": "user-rec-id-123",
    "phoneNumber": "9876543210",
    "firstName": "John",
    "lastName": "Doe",
    "email": "john@example.com",
    "userRole": "User",
    "profileCompleted": true
  }
}
```

**Response (New User - Registration Required):**
```json
{
  "success": true,
  "message": "OTP verified. Please complete registration.",
  "isNewUser": true,
  "accessToken": null,
  "refreshToken": null,
  "user": null
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Whether OTP verification was successful |
| `message` | string | Status message |
| `isNewUser` | boolean | true = registration required, false = login successful |
| `accessToken` | string | JWT access token (null for new users) |
| `refreshToken` | string | Refresh token (null for new users) |
| `user` | object | User information (null for new users) |

**Verification Rules:**
- ✅ OTP must not be expired (5 minute validity)
- ✅ Maximum 5 verification attempts per OTP
- ✅ OTP must match exactly
- ✅ OTP must not be already verified
- ✅ AuthID must be valid

**Error Responses:**

```json
{
  "success": false,
  "message": "Invalid or expired OTP session"
}
```

```json
{
  "success": false,
  "message": "OTP has expired. Please request a new one."
}
```

```json
{
  "success": false,
  "message": "Maximum verification attempts exceeded. Please request a new OTP."
}
```

```json
{
  "success": false,
  "message": "Invalid OTP code. 3 attempts remaining."
}
```

---

### 3. Resend OTP

**Endpoint:** `POST /api/User/resend-otp`

**Authorization:** None required (AllowAnonymous)

**Request Body:**
```json
{
  "phoneNumber": "9876543210",
  "countryCode": "+91",
  "purpose": "Login"
}
```

**Behavior:**
- Invalidates all previous non-verified OTPs for the phone number
- Generates and sends a new OTP with a new AuthID
- Same rate limiting as Send OTP

**Response:** Same as Send OTP endpoint

---

## Database Schema

### OtpValidation Table

```sql
CREATE TABLE OtpValidations (
    RecId VARCHAR(50) PRIMARY KEY,
    AuthId VARCHAR(50) NOT NULL,
    PhoneNumber VARCHAR(20) NOT NULL,
    CountryCode VARCHAR(10),
    OtpCode VARCHAR(10) NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    IsVerified BIT NOT NULL DEFAULT 0,
    VerifiedAt DATETIME NULL,
    AttemptCount INT NOT NULL DEFAULT 0,
    RequestIp VARCHAR(50),
    VerifyIp VARCHAR(50),
    UserId VARCHAR(50),
    Purpose VARCHAR(50),
    
    INDEX IX_OtpValidations_AuthId (AuthId),
    INDEX IX_OtpValidations_PhoneNumber (PhoneNumber),
    INDEX IX_OtpValidations_PhoneNumber_CreatedAt (PhoneNumber, CreatedAt)
);
```

### Entity Properties

| Column | Type | Description |
|--------|------|-------------|
| `RecId` | string (50) | Unique record ID (GUID) |
| `AuthId` | string (50) | Unique Auth ID for OTP session (GUID) |
| `PhoneNumber` | string (20) | Phone number without country code |
| `CountryCode` | string (10) | Country code (e.g., +91) |
| `OtpCode` | string (10) | 6-digit OTP code |
| `CreatedAt` | DateTime | OTP creation timestamp (UTC) |
| `ExpiresAt` | DateTime | OTP expiration timestamp (UTC) |
| `IsVerified` | bool | Whether OTP has been verified |
| `VerifiedAt` | DateTime? | Verification timestamp |
| `AttemptCount` | int | Number of verification attempts |
| `RequestIp` | string (50) | IP address that requested OTP |
| `VerifyIp` | string (50) | IP address that verified OTP |
| `UserId` | string (50) | Associated user ID (if exists) |
| `Purpose` | string (50) | OTP purpose (Login, Registration, etc.) |

---

## Security Features

### 1. Rate Limiting
- **Limit:** 3 OTP requests per phone number per 10 minutes
- **Purpose:** Prevent SMS spam and abuse
- **Implementation:** Query-based check before generating OTP

### 2. OTP Expiration
- **Validity:** 5 minutes (300 seconds)
- **Purpose:** Minimize window for brute force attacks
- **Implementation:** ExpiresAt timestamp checked during verification

### 3. Attempt Limiting
- **Limit:** 5 verification attempts per OTP
- **Purpose:** Prevent brute force OTP guessing
- **Implementation:** AttemptCount incremented on each attempt

### 4. AuthID-Based Validation
- **Purpose:** Prevents OTP verification without proper request context
- **Implementation:** Each OTP session has unique AuthID that must be provided during verification

### 5. IP Tracking
- **Tracks:** Request IP and Verify IP
- **Purpose:** Audit trail and fraud detection
- **Implementation:** GetIpAddress() from HTTP context

### 6. Single-Use OTPs
- **Purpose:** OTP can only be verified once
- **Implementation:** IsVerified flag prevents reuse

### 7. Soft Invalidation on Resend
- **Purpose:** Previous OTPs become invalid when resend is requested
- **Implementation:** ExpiresAt set to past time

---

## Integration Guide

### Frontend Integration (Angular/TypeScript)

#### 1. Send OTP
```typescript
// Service method
sendOtp(phoneNumber: string, countryCode: string = '+91'): Observable<SendOtpResponse> {
  return this.http.post<SendOtpResponse>(
    `${this.apiUrl}/api/User/send-otp`,
    { phoneNumber, countryCode, purpose: 'Login' }
  );
}

// Component usage
onSendOtp() {
  this.authService.sendOtp(this.phoneNumber).subscribe({
    next: (response) => {
      if (response.success) {
        this.authId = response.authId;
        this.showOtpInput = true;
        this.startTimer(response.expiresInSeconds);
        this.showSuccessMessage(response.message);
      } else {
        this.showErrorMessage(response.message);
      }
    },
    error: (error) => {
      this.showErrorMessage('Failed to send OTP');
    }
  });
}
```

#### 2. Verify OTP
```typescript
// Service method
verifyOtp(authId: string, otpCode: string, phoneNumber: string): Observable<VerifyOtpResponse> {
  return this.http.post<VerifyOtpResponse>(
    `${this.apiUrl}/api/User/verify-otp`,
    { authId, otpCode, phoneNumber }
  );
}

// Component usage
onVerifyOtp() {
  this.authService.verifyOtp(this.authId, this.otpCode, this.phoneNumber).subscribe({
    next: (response) => {
      if (response.success) {
        if (response.isNewUser) {
          // Navigate to registration page
          this.router.navigate(['/register'], { 
            queryParams: { phoneNumber: this.phoneNumber } 
          });
        } else {
          // Store tokens and navigate to dashboard
          localStorage.setItem('accessToken', response.accessToken);
          localStorage.setItem('user', JSON.stringify(response.user));
          this.router.navigate(['/dashboard']);
        }
      } else {
        this.showErrorMessage(response.message);
      }
    },
    error: (error) => {
      this.showErrorMessage('Failed to verify OTP');
    }
  });
}
```

#### 3. Resend OTP
```typescript
onResendOtp() {
  this.authService.resendOtp(this.phoneNumber).subscribe({
    next: (response) => {
      if (response.success) {
        this.authId = response.authId; // Update with new AuthID
        this.resetTimer();
        this.startTimer(response.expiresInSeconds);
        this.showSuccessMessage('OTP resent successfully');
      } else {
        this.showErrorMessage(response.message);
      }
    }
  });
}
```

---

## SMS Gateway Integration

### Placeholder Implementation
The current implementation includes a placeholder method for SMS sending:

```csharp
private async Task SendOtpSms(string phoneNumber, string otpCode, string purpose)
{
    // TODO: Implement SMS gateway integration
    _logger.LogInformation($"[SMS Gateway Placeholder] Sending OTP {otpCode} to {phoneNumber} for {purpose}");
    await Task.CompletedTask;
}
```

### Recommended SMS Providers (India)

#### 1. MSG91
```csharp
private async Task SendOtpSms(string phoneNumber, string otpCode, string purpose)
{
    using var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.msg91.com/api/v5/otp");
    
    request.Headers.Add("authkey", _config["MSG91:AuthKey"]);
    
    var payload = new
    {
        template_id = _config["MSG91:TemplateId"],
        mobile = phoneNumber,
        otp = otpCode
    };
    
    request.Content = new StringContent(
        JsonSerializer.Serialize(payload),
        Encoding.UTF8,
        "application/json"
    );
    
    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();
}
```

#### 2. Fast2SMS
```csharp
private async Task SendOtpSms(string phoneNumber, string otpCode, string purpose)
{
    using var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.fast2sms.com/dev/bulkV2");
    
    request.Headers.Add("authorization", _config["Fast2SMS:ApiKey"]);
    
    var payload = new Dictionary<string, string>
    {
        { "route", "otp" },
        { "variables_values", otpCode },
        { "numbers", phoneNumber }
    };
    
    request.Content = new FormUrlEncodedContent(payload);
    
    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();
}
```

#### 3. Twilio (International)
```csharp
private async Task SendOtpSms(string phoneNumber, string otpCode, string purpose)
{
    var accountSid = _config["Twilio:AccountSid"];
    var authToken = _config["Twilio:AuthToken"];
    
    TwilioClient.Init(accountSid, authToken);
    
    var message = await MessageResource.CreateAsync(
        body: $"Your OTP for {purpose} is: {otpCode}. Valid for 5 minutes.",
        from: new PhoneNumber(_config["Twilio:PhoneNumber"]),
        to: new PhoneNumber(phoneNumber)
    );
    
    _logger.LogInformation($"SMS sent: {message.Sid}");
}
```

---

## Testing

### Development/Testing Mode
In development, the OTP code is logged for testing purposes:

```csharp
_logger.LogInformation($"[DEV] OTP Code: {otpCode}");
```

**⚠️ IMPORTANT:** Remove this logging in production!

### Test Scenarios

#### 1. Successful Login Flow
```
1. Send OTP → Get AuthID
2. Verify OTP with correct code → Get tokens
3. Store tokens and navigate to dashboard
```

#### 2. New User Registration
```
1. Send OTP → Get AuthID
2. Verify OTP → Get isNewUser: true
3. Navigate to registration page with phone number
4. Complete registration
```

#### 3. Rate Limiting
```
1. Send OTP (1st) → Success
2. Send OTP (2nd) → Success
3. Send OTP (3rd) → Success
4. Send OTP (4th) → Error: "Too many OTP requests"
```

#### 4. OTP Expiration
```
1. Send OTP → Get AuthID
2. Wait 6 minutes
3. Verify OTP → Error: "OTP has expired"
```

#### 5. Invalid Attempts
```
1. Send OTP → Get AuthID
2. Verify with wrong code (1st) → "4 attempts remaining"
3. Verify with wrong code (2nd) → "3 attempts remaining"
...
6. Verify with wrong code (5th) → "Maximum attempts exceeded"
```

#### 6. Resend OTP
```
1. Send OTP → Get AuthID #1
2. Resend OTP → Get AuthID #2
3. Verify with OTP #1 → Error (invalidated)
4. Verify with OTP #2 → Success
```

---

## Postman Collection

### Send OTP Request
```json
POST {{baseUrl}}/api/User/send-otp
Content-Type: application/json

{
  "phoneNumber": "9876543210",
  "countryCode": "+91",
  "purpose": "Login"
}
```

### Verify OTP Request
```json
POST {{baseUrl}}/api/User/verify-otp
Content-Type: application/json

{
  "authId": "{{authId}}",
  "otpCode": "123456",
  "phoneNumber": "9876543210"
}
```

### Resend OTP Request
```json
POST {{baseUrl}}/api/User/resend-otp
Content-Type: application/json

{
  "phoneNumber": "9876543210",
  "countryCode": "+91",
  "purpose": "Login"
}
```

---

## Database Migration

After implementing the OTP system, run the following to create the database table:

```bash
# Add migration
dotnet ef migrations add AddOtpValidation --project OCPP.Core.Database --startup-project OCPP.Core.Management

# Update database
dotnet ef database update --project OCPP.Core.Database --startup-project OCPP.Core.Management
```

---

## Configuration

Add SMS gateway configuration to `appsettings.json`:

```json
{
  "SMS": {
    "Provider": "MSG91",
    "MSG91": {
      "AuthKey": "your-auth-key",
      "TemplateId": "your-template-id"
    },
    "Fast2SMS": {
      "ApiKey": "your-api-key"
    },
    "Twilio": {
      "AccountSid": "your-account-sid",
      "AuthToken": "your-auth-token",
      "PhoneNumber": "+1234567890"
    }
  }
}
```

---

## Best Practices

### Security
1. ✅ Never log OTP codes in production
2. ✅ Use HTTPS for all API calls
3. ✅ Implement rate limiting at API gateway level too
4. ✅ Monitor for suspicious patterns (same IP, multiple phones)
5. ✅ Consider adding CAPTCHA for send-otp endpoint

### User Experience
1. ✅ Show countdown timer for OTP expiration
2. ✅ Auto-focus OTP input field
3. ✅ Enable resend after 30 seconds
4. ✅ Show masked phone number for confirmation
5. ✅ Auto-submit when 6 digits entered
6. ✅ Show remaining attempts on error

### Performance
1. ✅ Clean up expired OTP records (scheduled job)
2. ✅ Index on AuthId and PhoneNumber for fast lookups
3. ✅ Cache SMS provider configuration
4. ✅ Use async/await for SMS sending

---

## Troubleshooting

### Issue: OTP not received
**Check:**
- SMS gateway credentials configured correctly
- Phone number format is correct
- SMS provider has sufficient balance
- Check spam/blocked messages

### Issue: "Invalid or expired OTP session"
**Check:**
- AuthId matches the one from send-otp response
- Phone number is exactly the same
- OTP hasn't been verified already

### Issue: "Too many OTP requests"
**Solution:**
- Wait 10 minutes before trying again
- Or manually clean up OtpValidations table for testing

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-15 | Initial implementation |

---

## Files Changed/Created

### Database
- `OCPP.Core.Database/EVCDTO/OtpValidation.cs` - Entity
- `OCPP.Core.Database/OCPPCoreContext.cs` - DbSet and configuration

### Models
- `OCPP.Core.Management/Models/Auth/SendOtpRequestDto.cs`
- `OCPP.Core.Management/Models/Auth/VerifyOtpRequestDto.cs`

### Controllers
- `OCPP.Core.Management/Controllers/UserController.cs` - OTP endpoints

### Documentation
- `OCPP.Core.Management/OTP_AUTHENTICATION_README.md` - This file

---

**Implementation Status:** ✅ Complete (SMS gateway integration pending)

**Next Steps:**
1. Integrate chosen SMS provider
2. Run database migration
3. Test OTP flow end-to-end
4. Remove development logging
5. Configure production SMS credentials
