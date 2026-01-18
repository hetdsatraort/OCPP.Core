# New User Controller APIs - Documentation

This document describes the new API endpoints added to the UserController for wallet management, vehicle management, and user/data listing.

## Wallet Management APIs

### 1. Add Wallet Credits
**Endpoint:** `POST /api/user/add-wallet-credits`

**Authorization:** Required

**Request Body:**
```json
{
  "userId": "guid",
  "amount": 100.50,
  "transactionType": "Credit",
  "paymentRecId": "optional-payment-id",
  "additionalInfo1": "Optional info",
  "additionalInfo2": "Optional info",
  "additionalInfo3": "Optional info"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Credits added successfully",
  "wallet": {
    "userId": "guid",
    "currentBalance": 250.50
  }
}
```

### 2. Get Wallet Details
**Endpoint:** `GET /api/user/wallet-details`

**Authorization:** Required

**Response:**
```json
{
  "success": true,
  "message": "Wallet details retrieved successfully",
  "wallet": {
    "userId": "guid",
    "currentBalance": 250.50,
    "recentTransactions": [
      {
        "recId": "guid",
        "previousCreditBalance": "150.00",
        "currentCreditBalance": "250.50",
        "transactionType": "Credit",
        "paymentRecId": "payment-id",
        "chargingSessionId": null,
        "createdOn": "2024-01-01T00:00:00Z"
      }
    ]
  }
}
```

## Vehicle Management APIs

### 3. Add User Vehicle
**Endpoint:** `POST /api/user/user-vehicle-add`

**Authorization:** Required

**Request Body:**
```json
{
  "evManufacturerID": "manufacturer-id",
  "carModelID": "model-id",
  "carModelVariant": "Variant Name",
  "carRegistrationNumber": "ABC-1234",
  "defaultConfig": 1,
  "batteryTypeId": "battery-type-id",
  "batteryCapacityId": "capacity-id"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Vehicle added successfully",
  "vehicle": {
    "recId": "guid",
    "userId": "user-guid",
    "evManufacturerID": "manufacturer-id",
    "carModelID": "model-id",
    "carModelVariant": "Variant Name",
    "carRegistrationNumber": "ABC-1234",
    "defaultConfig": 1,
    "batteryTypeId": "battery-type-id",
    "batteryCapacityId": "capacity-id",
    "createdOn": "2024-01-01T00:00:00Z",
    "updatedOn": "2024-01-01T00:00:00Z"
  }
}
```

### 4. Update User Vehicle
**Endpoint:** `PUT /api/user/user-vehicle-update`

**Authorization:** Required

**Request Body:**
```json
{
  "recId": "vehicle-guid",
  "evManufacturerID": "manufacturer-id",
  "carModelID": "model-id",
  "carModelVariant": "Updated Variant",
  "carRegistrationNumber": "ABC-1234",
  "defaultConfig": 1,
  "batteryTypeId": "battery-type-id",
  "batteryCapacityId": "capacity-id"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Vehicle updated successfully",
  "vehicle": {
    "recId": "guid",
    "userId": "user-guid",
    "evManufacturerID": "manufacturer-id",
    "carModelID": "model-id",
    "carModelVariant": "Updated Variant",
    "carRegistrationNumber": "ABC-1234",
    "defaultConfig": 1,
    "createdOn": "2024-01-01T00:00:00Z",
    "updatedOn": "2024-01-01T12:00:00Z"
  }
}
```

### 5. Delete User Vehicle
**Endpoint:** `DELETE /api/user/user-vehicle-delete/{vehicleId}`

**Authorization:** Required

**Path Parameter:**
- `vehicleId`: The GUID of the vehicle to delete

**Response:**
```json
{
  "success": true,
  "message": "Vehicle deleted successfully"
}
```

**Note:** This performs a soft delete (sets Active = 0)

### 6. Get User Vehicle List
**Endpoint:** `GET /api/user/user-vehicle-list`

**Authorization:** Required

**Response:**
```json
{
  "success": true,
  "message": "Vehicles retrieved successfully",
  "vehicles": [
    {
      "recId": "guid",
      "userId": "user-guid",
      "evManufacturerID": "manufacturer-id",
      "carModelID": "model-id",
      "carModelVariant": "Variant Name",
      "carRegistrationNumber": "ABC-1234",
      "defaultConfig": 1,
      "batteryTypeId": "battery-type-id",
      "batteryCapacityId": "capacity-id",
      "createdOn": "2024-01-01T00:00:00Z",
      "updatedOn": "2024-01-01T00:00:00Z"
    }
  ]
}
```

**Note:** Results are ordered by default configuration first, then by creation date

## User Management APIs

### 7. Get User List
**Endpoint:** `GET /api/user/user-list`

**Authorization:** Required

**Query Parameters:**
- `pageNumber` (optional, default: 1): Page number for pagination
- `pageSize` (optional, default: 10): Number of users per page

**Example:** `GET /api/user/user-list?pageNumber=1&pageSize=20`

**Response:**
```json
{
  "success": true,
  "message": "Users retrieved successfully",
  "users": [
    {
      "recId": "guid",
      "firstName": "John",
      "lastName": "Doe",
      "eMailID": "john@example.com",
      "phoneNumber": "1234567890",
      "countryCode": "+1",
      "profileImageID": "image-url",
      "addressLine1": "123 Main St",
      "state": "NY",
      "city": "New York",
      "pinCode": "10001",
      "profileCompleted": "Yes",
      "userRole": "User",
      "createdOn": "2024-01-01T00:00:00Z"
    }
  ],
  "totalCount": 150
}
```

**Note:** This is typically an admin endpoint. You may want to add role-based authorization.

### 8. Get User Details
**Endpoint:** `GET /api/user/user-details/{userId}`

**Authorization:** Required

**Path Parameter:**
- `userId`: The GUID of the user

**Response:**
```json
{
  "success": true,
  "message": "User details retrieved successfully",
  "user": {
    "recId": "guid",
    "firstName": "John",
    "lastName": "Doe",
    "eMailID": "john@example.com",
    "phoneNumber": "1234567890",
    "userRole": "User"
  },
  "wallet": {
    "userId": "guid",
    "currentBalance": 250.50,
    "recentTransactions": [...]
  },
  "vehicles": [
    {
      "recId": "vehicle-guid",
      "carRegistrationNumber": "ABC-1234",
      "defaultConfig": 1
    }
  ]
}
```

## cURL Examples

### Add Wallet Credits
```bash
curl -X POST http://localhost:8082/api/user/add-wallet-credits \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "your-user-id",
    "amount": 100.00,
    "transactionType": "Credit"
  }'
```

### Add Vehicle
```bash
curl -X POST http://localhost:8082/api/user/user-vehicle-add \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{
    "carRegistrationNumber": "ABC-1234",
    "carModelVariant": "Model X",
    "defaultConfig": 1
  }'
```

### Get Wallet Details
```bash
curl -X GET http://localhost:8082/api/user/wallet-details \
  -b cookies.txt
```

### Get User Vehicle List
```bash
curl -X GET http://localhost:8082/api/user/user-vehicle-list \
  -b cookies.txt
```

### Delete Vehicle
```bash
curl -X DELETE http://localhost:8082/api/user/user-vehicle-delete/vehicle-guid \
  -b cookies.txt
```

### Get User List (Paginated)
```bash
curl -X GET "http://localhost:8082/api/user/user-list?pageNumber=1&pageSize=10" \
  -b cookies.txt
```

## JavaScript/Fetch Examples

### Add Wallet Credits
```javascript
const response = await fetch('http://localhost:8082/api/user/add-wallet-credits', {
  method: 'POST',
  credentials: 'include',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    userId: 'user-guid',
    amount: 100.00,
    transactionType: 'Credit'
  })
});
```

### Add Vehicle
```javascript
const response = await fetch('http://localhost:8082/api/user/user-vehicle-add', {
  method: 'POST',
  credentials: 'include',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    carRegistrationNumber: 'ABC-1234',
    carModelVariant: 'Model X',
    defaultConfig: 1
  })
});
```

### Get Wallet Details
```javascript
const response = await fetch('http://localhost:8082/api/user/wallet-details', {
  method: 'GET',
  credentials: 'include'
});
```

## Security Notes

1. All endpoints require authentication via JWT token in HTTP-only cookie
2. Vehicle operations are restricted to the authenticated user's own vehicles
3. `add-wallet-credits` can be called by any authenticated user - consider adding admin role check
4. `user-list` and `user-details` endpoints may need role-based authorization for production use
5. All operations use soft delete to maintain data integrity
6. Vehicle registration numbers must be unique across the system

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

## Transaction Types

For wallet operations, common transaction types include:
- `Credit` - Adding funds to wallet
- `Debit` - Removing funds from wallet
- `Refund` - Refunding a transaction
- `ChargingFee` - Deducting charging fees
- `Bonus` - Promotional credits

## Default Vehicle Configuration

The `DefaultConfig` field allows users to mark one vehicle as their default:
- When setting a vehicle as default (`defaultConfig: 1`), all other vehicles are automatically unmarked
- Only one vehicle per user can be marked as default
- The vehicle list returns default vehicles first

## Wallet Balance Calculation

The wallet system:
1. Stores each transaction with previous and current balance
2. Current balance is calculated from the most recent transaction
3. Each new transaction references the previous balance
4. Transaction history is maintained for audit purposes
5. Supports pagination for transaction history (last 20 shown in wallet-details)
