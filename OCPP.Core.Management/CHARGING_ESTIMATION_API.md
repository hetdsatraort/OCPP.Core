# Charging Estimation API - Documentation

## Overview
The Charging Estimation API provides pre-charging estimates for energy consumption, cost, time, kilometres/range, and battery increase based on charger capacity and configurable car assumptions.

## API Endpoint

### POST `/api/ChargingSession/estimate-charging`
**Authorization:** Not required (can be called before authentication)

## Request Format

```json
{
  "chargingGunId": "gun-rec-id-123",
  "chargingStationId": "station-id-456",
  "connectorId": "1",
  "batteryCapacity": 50.0,           // Optional: User's battery in kWh
  "desiredEnergy": 25.0,             // Optional: Energy to charge in kWh
  "desiredDuration": 60,             // Optional: Duration in minutes
  "currentBatteryPercentage": 30.0   // Optional: Current SoC (0-100)
}
```

### Request Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `chargingGunId` | string | ✅ Yes | Charging gun/connector record ID |
| `chargingStationId` | string | ✅ Yes | Charging station ID |
| `connectorId` | string | ✅ Yes | Connector ID (e.g., "1", "2") |
| `batteryCapacity` | double | ❌ No | User's battery capacity in kWh (default: 40 kWh) |
| `desiredEnergy` | double | ❌ No | Desired energy to charge in kWh |
| `desiredDuration` | int | ❌ No | Desired charging duration in minutes |
| `currentBatteryPercentage` | double | ❌ No | Current battery % (0-100) |

### Estimation Logic

**Priority for Energy Calculation:**
1. If `desiredEnergy` provided → Use that value
2. If `desiredDuration` provided → Calculate: `Energy = Power × Time × Efficiency`
3. Otherwise → Default to 1 hour: `Energy = Power × 1.0 × Efficiency`

**Caps Applied:**
- If `currentBatteryPercentage` provided → Cap at available capacity
- Otherwise → Cap at 80% of battery capacity (realistic charging limit)

## Response Format

```json
{
  "success": true,
  "message": "Estimation calculated successfully",
  "estimatedEnergy": 20.25,
  "estimatedCost": 253.13,
  "estimatedCostWithTax": 298.69,
  "estimatedTimeMinutes": 96.3,
  "estimatedTimeHours": 1.61,
  "estimatedKilometres": 91.1,
  "estimatedBatteryIncrease": 50.6,
  "charger": {
    "powerOutput": 7.4,
    "tariff": 12.50,
    "chargerType": "AC Type 2",
    "connectorId": "1"
  },
  "car": {
    "batteryCapacity": 40.0,
    "efficiency": 4.5,
    "currentBatteryPercentage": 30.0,
    "chargingEfficiency": 0.90
  },
  "costDetails": {
    "energyCost": 253.13,
    "taxAmount": 45.56,
    "totalCost": 298.69,
    "costPerKm": 3.28,
    "tariffApplied": 12.50,
    "currency": "₹"
  }
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Whether estimation was successful |
| `message` | string | Status message or error description |
| `estimatedEnergy` | double | Energy consumption in kWh |
| `estimatedCost` | double | Base cost without tax (₹) |
| `estimatedCostWithTax` | double | Total cost with 18% GST (₹) |
| `estimatedTimeMinutes` | double | Charging time in minutes |
| `estimatedTimeHours` | double | Charging time in hours |
| `estimatedKilometres` | double | Estimated range added (km) |
| `estimatedBatteryIncrease` | double | Battery increase in percentage (%) |
| `charger` | object | Charger details used for estimation |
| `car` | object | Car assumptions used |
| `costDetails` | object | Detailed cost breakdown |

## Calculation Formulas

### 1. Energy Consumption
```
Energy (kWh) = Power (kW) × Time (hours) × Charging Efficiency
```

### 2. Charging Time
```
Time (hours) = Energy (kWh) / (Power (kW) × Charging Efficiency)
```

### 3. Cost Calculation
```
Energy Cost (₹) = Energy (kWh) × Tariff (₹/kWh)
Tax Amount (₹) = Energy Cost × 0.18 (18% GST)
Total Cost (₹) = Energy Cost + Tax Amount
```

### 4. Kilometres/Range
```
Kilometres = Energy (kWh) × Efficiency (km/kWh)
Default Efficiency = 4.5 km/kWh
```

### 5. Battery Increase
```
Battery Increase (%) = (Energy (kWh) / Battery Capacity (kWh)) × 100
```

### 6. Cost per Kilometre
```
Cost per km (₹/km) = Total Cost (₹) / Kilometres
```

## Default Car Assumptions

| Parameter | Default Value | Description |
|-----------|--------------|-------------|
| Battery Capacity | 40 kWh | Common for EVs in India (Tata Nexon EV, MG ZS EV) |
| Efficiency | 4.5 km/kWh | Realistic average for Indian EVs |
| Charging Efficiency | 90% (AC) / 87% (DC) | Accounts for energy losses |

### Popular EV Battery Capacities (India)

| Vehicle | Battery Capacity |
|---------|------------------|
| Tata Nexon EV | 40.5 kWh |
| MG ZS EV | 50.3 kWh |
| Hyundai Kona Electric | 39.2 kWh |
| Tata Tigor EV | 26 kWh |
| Mahindra e-Verito | 21.2 kWh |

## Usage Examples

### Example 1: Basic Estimation (1 hour default)
**Request:**
```json
{
  "chargingGunId": "gun-123",
  "chargingStationId": "station-456",
  "connectorId": "1"
}
```

**Scenario:**
- Charger: 7.4 kW AC Type 2
- Tariff: ₹12.50/kWh
- Battery: 40 kWh (default)
- Duration: 1 hour (default)

**Estimation:**
- Energy: 6.66 kWh (7.4 kW × 1h × 0.9)
- Cost: ₹98.36 (₹83.25 + ₹15.11 GST)
- Time: 60 minutes
- Range: 30 km
- Battery: +16.7%

### Example 2: Specific Duration
**Request:**
```json
{
  "chargingGunId": "gun-123",
  "chargingStationId": "station-456",
  "connectorId": "1",
  "desiredDuration": 120
}
```

**Estimation:**
- Energy: 13.32 kWh (7.4 kW × 2h × 0.9)
- Cost: ₹196.72
- Time: 120 minutes
- Range: 60 km
- Battery: +33.3%

### Example 3: Specific Energy with Current SoC
**Request:**
```json
{
  "chargingGunId": "gun-123",
  "chargingStationId": "station-456",
  "connectorId": "1",
  "batteryCapacity": 50.0,
  "desiredEnergy": 25.0,
  "currentBatteryPercentage": 20.0
}
```

**Estimation:**
- Energy: 25.0 kWh
- Cost: ₹368.75 (₹312.50 + ₹56.25 GST)
- Time: 225 minutes (3.75 hours)
- Range: 112.5 km
- Battery: +50%

### Example 4: Fast DC Charging
**Request:**
```json
{
  "chargingGunId": "dc-gun-789",
  "chargingStationId": "station-456",
  "connectorId": "1",
  "batteryCapacity": 40.0,
  "desiredEnergy": 32.0,
  "currentBatteryPercentage": 10.0
}
```

**Scenario:**
- Charger: 50 kW DC Fast Charger
- Tariff: ₹18.00/kWh
- Charging Efficiency: 87% (DC)

**Estimation:**
- Energy: 32.0 kWh
- Cost: ₹678.24 (₹576.00 + ₹102.24 GST)
- Time: 44.1 minutes
- Range: 144 km
- Battery: +80%

## Integration Notes

### Frontend Usage
```typescript
// Angular Service Call
estimateCharging(request: ChargingEstimationRequest): Observable<ChargingEstimationResponse> {
  return this.http.post<ChargingEstimationResponse>(
    `${this.apiUrl}/api/ChargingSession/estimate-charging`,
    request
  );
}

// Component Usage
this.chargingService.estimateCharging({
  chargingGunId: this.selectedGun.recId,
  chargingStationId: this.stationId,
  connectorId: this.connectorId,
  batteryCapacity: this.userCar.batteryCapacity,
  desiredEnergy: this.energySlider.value,
  currentBatteryPercentage: this.currentSoC
}).subscribe(response => {
  if (response.success) {
    this.displayEstimation(response);
  }
});
```

### Use Cases
1. **Pre-Charging Estimate:** Show users cost and time before starting session
2. **Charging Planner:** Help users plan charging based on their needs
3. **Cost Calculator:** Compare charging costs at different stations
4. **Range Planning:** Calculate how much range they can add
5. **Budget Planning:** Estimate costs for monthly charging needs

## Error Handling

### Common Error Responses

**Charging Gun Not Found:**
```json
{
  "success": false,
  "message": "Charging gun not found or inactive"
}
```

**Invalid Power Output:**
```json
{
  "success": false,
  "message": "Invalid charger power output"
}
```

**Invalid Request:**
```json
{
  "success": false,
  "message": "Invalid request data"
}
```

## Performance Considerations

- **No Authorization Required:** Can be called quickly before user logs in
- **Fast Calculation:** All calculations done in-memory
- **No External Dependencies:** Uses only charger details from database
- **Cached Data:** Charger details can be cached on frontend

## Future Enhancements

1. **Vehicle Database Integration:**
   - Lookup actual battery capacity from vehicle make/model
   - Use manufacturer's efficiency ratings

2. **Dynamic Pricing:**
   - Time-of-use tariffs
   - Peak/off-peak pricing

3. **Weather Adjustment:**
   - Temperature-based efficiency adjustments
   - Seasonal variations

4. **Charging Profiles:**
   - Tapered charging for DC fast chargers
   - More accurate time estimates based on SoC curve

5. **Historical Data:**
   - Average charging speeds at specific stations
   - User's historical efficiency patterns

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-15 | Initial implementation |

---

**API Location:** `ChargingSessionController.cs`  
**DTOs:** `ChargingEstimationRequestDto.cs`, `ChargingEstimationResponseDto.cs`  
**Endpoint:** `POST /api/ChargingSession/estimate-charging`
