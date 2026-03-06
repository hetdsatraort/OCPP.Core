# JWT Authentication System for OCPP.Core

This document explains the new JWT-based authentication system implemented in OCPP.Core Management API.

## Overview

The authentication system uses:
- **JWT (JSON Web Tokens)** for stateless authentication
- **HTTP-only cookies** for secure token storage
- **Refresh tokens** for extended sessions
- **SHA256 password hashing** for secure password storage

## API Endpoints

All endpoints are prefixed with `/api/user`

### 1. Register New User
**Endpoint:** `POST /api/user/register`

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "eMailID": "john.doe@example.com",
  "phoneNumber": "1234567890",
  "countryCode": "+1",
  "password": "SecurePassword123",
  "confirmPassword": "SecurePassword123",
  "addressLine1": "123 Main St",
  "city": "New York",
  "state": "NY",
  "pinCode": "10001"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Registration successful",
  "user": {
    "recId": "guid",
    "firstName": "John",
    "lastName": "Doe",
    "eMailID": "john.doe@example.com",
    "phoneNumber": "1234567890",
    "userRole": "User",
    "createdOn": "2024-01-01T00:00:00Z"
  }
}
```

### 2. Login
**Endpoint:** `POST /api/user/login`

**Request Body:**
```json
{
  "emailOrPhone": "john.doe@example.com",
  "password": "SecurePassword123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "user": {
    "recId": "guid",
    "firstName": "John",
    "lastName": "Doe",
    "eMailID": "john.doe@example.com"
  }
}
```

**Note:** Access and refresh tokens are automatically set as HTTP-only cookies.

### 3. Logout
**Endpoint:** `POST /api/user/logout`

**Headers:** Requires authentication

**Response:**
```json
{
  "success": true,
  "message": "Logout successful"
}
```

### 4. Reset Password
**Endpoint:** `POST /api/user/reset-password`

**Headers:** Requires authentication

**Request Body:**
```json
{
  "emailOrPhone": "john.doe@example.com",
  "oldPassword": "OldPassword123",
  "newPassword": "NewPassword123",
  "confirmPassword": "NewPassword123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Password reset successful. Please login again."
}
```

### 5. Update Profile
**Endpoint:** `PUT /api/user/profile-update`

**Headers:** Requires authentication

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "0987654321",
  "city": "Los Angeles",
  "state": "CA"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Profile updated successfully",
  "user": {
    "recId": "guid",
    "firstName": "John",
    "lastName": "Doe",
    "city": "Los Angeles",
    "state": "CA"
  }
}
```

### 6. Get User Profile
**Endpoint:** `GET /api/user/profile`

**Headers:** Requires authentication

**Response:**
```json
{
  "success": true,
  "message": "Profile retrieved successfully",
  "user": {
    "recId": "guid",
    "firstName": "John",
    "lastName": "Doe",
    "eMailID": "john.doe@example.com",
    "phoneNumber": "1234567890"
  }
}
```

### 7. Delete User Account
**Endpoint:** `DELETE /api/user/user-delete`

**Headers:** Requires authentication

**Response:**
```json
{
  "success": true,
  "message": "Account deleted successfully"
}
```

**Note:** This performs a soft delete (sets Active = 0)

### 8. Refresh Token
**Endpoint:** `POST /api/user/refresh-token`

**Note:** Automatically reads refresh token from HTTP-only cookie

**Response:**
```json
{
  "success": true,
  "message": "Token refreshed successfully",
  "user": {
    "recId": "guid",
    "firstName": "John",
    "lastName": "Doe"
  }
}
```

## Configuration

### appsettings.json

Add the following JWT settings to your `appsettings.json`:

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

**Important:** Change the `Secret` value in production!

## Security Features

1. **HTTP-only Cookies:** Tokens are stored in HTTP-only cookies, preventing XSS attacks
2. **Secure Flag:** Cookies are marked as secure (HTTPS only in production)
3. **SameSite:** Cookies use SameSite=Strict to prevent CSRF attacks
4. **Password Hashing:** Passwords are hashed using SHA256
5. **Token Expiration:** Access tokens expire after 15 minutes, refresh tokens after 7 days
6. **Token Revocation:** Refresh tokens can be revoked on logout or password reset
7. **Soft Delete:** User accounts are soft-deleted, preserving data integrity

## Database Migration

Run the following command to create the RefreshToken table:

```bash
dotnet ef migrations add AddRefreshTokenTable --project OCPP.Core.Database --startup-project OCPP.Core.Management
dotnet ef database update --project OCPP.Core.Database --startup-project OCPP.Core.Management
```

## Usage Examples

### cURL Examples

**Register:**
```bash
curl -X POST http://localhost:8082/api/user/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Doe",
    "eMailID": "john@example.com",
    "phoneNumber": "1234567890",
    "password": "Test123",
    "confirmPassword": "Test123"
  }'
```

**Login:**
```bash
curl -X POST http://localhost:8082/api/user/login \
  -H "Content-Type: application/json" \
  -c cookies.txt \
  -d '{
    "emailOrPhone": "john@example.com",
    "password": "Test123"
  }'
```

**Get Profile (requires cookies from login):**
```bash
curl -X GET http://localhost:8082/api/user/profile \
  -b cookies.txt
```

### JavaScript/Fetch Example

```javascript
// Register
const registerResponse = await fetch('http://localhost:8082/api/user/register', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    firstName: 'John',
    lastName: 'Doe',
    eMailID: 'john@example.com',
    phoneNumber: '1234567890',
    password: 'Test123',
    confirmPassword: 'Test123'
  })
});

// Login
const loginResponse = await fetch('http://localhost:8082/api/user/login', {
  method: 'POST',
  credentials: 'include', // Important for cookies
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    emailOrPhone: 'john@example.com',
    password: 'Test123'
  })
});

// Get Profile
const profileResponse = await fetch('http://localhost:8082/api/user/profile', {
  method: 'GET',
  credentials: 'include' // Important for cookies
});
```

## Token Refresh Flow

1. Access token expires after 15 minutes
2. Client receives 401 Unauthorized response
3. Client calls `/api/user/refresh-token` endpoint
4. Server validates refresh token from cookie
5. Server generates new access and refresh tokens
6. Server sets new tokens as HTTP-only cookies
7. Client retries original request with new tokens

## Error Responses

All endpoints return standardized error responses:

```json
{
  "success": false,
  "message": "Error description"
}
```

Common HTTP status codes:
- `200 OK` - Success
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Authentication required or invalid credentials
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

## Multi-Platform Support

This API can be consumed by:
- Web applications (JavaScript/TypeScript)
- Mobile apps (iOS/Android)
- Desktop applications
- Other backend services

Simply include cookies in requests using appropriate HTTP client libraries.

## Notes

1. The system supports login with either email or phone number
2. All timestamps are stored in UTC
3. User deletion is soft delete (Active = 0) to maintain referential integrity
4. On password reset, all existing refresh tokens are revoked
5. The system logs all authentication events for audit purposes
