# Charging Session Enhanced Calculations

## Overview
The charging session system now provides comprehensive calculations including energy consumption, State of Charge (SOC) changes, cost breakdown, and charging performance metrics using data from ChargingGuns, UserVehicle, and OCPP transactions.

## Data Sources

### 1. ChargingGuns Table
- **ChargerTypeId**: Type of charger (AC/DC, fast/slow)
- **ChargerTariff**: Price per kWh (₹/kWh)
- **PowerOutput**: Maximum power output (kW)
- **ChargerStatus**: Current availability status

### 2. UserVehicle Table
- **BatteryCapacityId**: Links to battery capacity master
- **ChargerTypeId**: Compatible charger type
- **EVManufacturerID** & **CarModelID**: Vehicle details

### 3. OCPP Transaction Table
- **MeterStart**: Starting meter reading (Wh)
- **MeterStop**: Ending meter reading (Wh)
- **StartTime** & **StopTime**: Actual charging timestamps

### 4. Master Tables
- **BatteryCapacityMaster**: Battery specifications
- **ChargerTypeMaster**: Charger type details
- **EVModelMaster**: Vehicle model information

## Calculations

### 1. Energy Consumption

```csharp
// From OCPP Transaction (most accurate)
energyConsumed = (MeterStop - MeterStart) / 1000  // Convert Wh to kWh
energyConsumed = Max(0, energyConsumed)  // Ensure non-negative

// Unit: kWh
// Precision: 3 decimal places (0.001 kWh = 1 Wh)
```

**Example:**
- MeterStart: 12,345 Wh
- MeterStop: 27,890 Wh
- Energy = (27,890 - 12,345) / 1000 = 15.545 kWh

### 2. State of Charge (SOC) Change

```csharp
// SOC Change in kWh
socChange = energyConsumed

// SOC Change as Percentage
socChangePercentage = (energyConsumed / batteryCapacity) × 100

// Unit: % and kWh
// Precision: 1 decimal place for percentage
```

**Example:**
- Energy Consumed: 15.545 kWh
- Battery Capacity: 40 kWh
- SOC Change = (15.545 / 40) × 100 = 38.9%

**Interpretation:**
- 0-20%: Partial charge
- 20-80%: Optimal charging range
- 80-100%: Final charge (slower for battery health)

### 3. Estimated Range Added

```csharp
// Using industry average efficiency
estimatedRange = energyConsumed × averageEfficiency
averageEfficiency = 4.5 km/kWh  // Typical EV efficiency

// Unit: km
// Precision: Whole numbers
```

**Example:**
- Energy Consumed: 15.545 kWh
- Range Added = 15.545 × 4.5 = 70 km

**Notes:**
- Actual range varies with:
  - Driving conditions (city/highway)
  - Weather (temperature affects battery)
  - Driving style (aggressive/conservative)
  - Vehicle load and accessories

### 4. Average Charging Speed

```csharp
// Power delivery rate
duration = endTime - startTime  // TimeSpan
averageChargingSpeed = energyConsumed / duration.TotalHours

// Unit: kW
// Precision: 2 decimal places
```

**Example:**
- Energy Consumed: 15.545 kWh
- Duration: 1 hour 15 minutes (1.25 hours)
- Average Speed = 15.545 / 1.25 = 12.44 kW

**Charging Speed Categories:**
- < 3 kW: Level 1 (Slow, home outlet)
- 3-7 kW: Level 2 (Home/workplace)
- 7-22 kW: AC Fast
- 22-50 kW: DC Fast
- 50-150 kW: DC Rapid
- > 150 kW: DC Ultra-Rapid

### 5. Peak Charging Speed

```csharp
// From charging gun specifications
peakChargingSpeed = double.Parse(chargingGun.PowerOutput)

// Fallback to average if not available
peakChargingSpeed = averageChargingSpeed

// Unit: kW
// Precision: 2 decimal places
```

**Example:**
- PowerOutput: "22 kW" (from charging gun)
- Peak Speed = 22.00 kW

### 6. Charging Efficiency

```csharp
// Theoretical vs actual energy
duration = endTime - startTime
theoreticalEnergy = peakChargingSpeed × duration.TotalHours
chargingEfficiency = (energyConsumed / theoreticalEnergy) × 100

// Cap at realistic maximum
chargingEfficiency = Min(chargingEfficiency, 100)

// Default if calculation unavailable
defaultEfficiency = 90.0%

// Unit: %
// Precision: 1 decimal place
```

**Example:**
- Energy Consumed: 15.545 kWh
- Peak Power: 22 kW
- Duration: 1.25 hours
- Theoretical = 22 × 1.25 = 27.5 kWh
- Efficiency = (15.545 / 27.5) × 100 = 56.5%

**Typical Efficiency Ranges:**
- 85-95%: Normal charging (good)
- 75-85%: Acceptable (some losses)
- 50-75%: Power throttling or battery near full
- < 50%: Potential issue or trickle charging

**Factors Affecting Efficiency:**
- Battery temperature
- State of Charge (slows near 80-100%)
- Battery age and condition
- Grid voltage stability
- Cable quality and length

### 7. Cost Calculations

#### a. Energy Cost
```csharp
// Get tariff from charging gun (most accurate)
tariff = double.Parse(chargingGun.ChargerTariff)  // ₹/kWh

// Calculate energy cost
energyCost = energyConsumed × tariff

// Unit: ₹ (Indian Rupees)
// Precision: 2 decimal places
```

**Example:**
- Energy: 15.545 kWh
- Tariff: ₹12.50/kWh
- Energy Cost = 15.545 × 12.50 = ₹194.31

#### b. Service Fee
```csharp
// Optional platform/service charge
serviceFee = 0.0  // Currently not implemented
// Could be flat rate or percentage based

// Unit: ₹
// Precision: 2 decimal places
```

#### c. Taxes (GST)
```csharp
// 18% GST on energy cost
taxRate = 0.18
taxes = energyCost × taxRate

// Unit: ₹
// Precision: 2 decimal places
```

**Example:**
- Energy Cost: ₹194.31
- GST = 194.31 × 0.18 = ₹34.98

#### d. Total Cost
```csharp
totalCost = energyCost + serviceFee + taxes

// Unit: ₹
// Precision: 2 decimal places
```

**Example:**
- Energy Cost: ₹194.31
- Service Fee: ₹0.00
- Taxes: ₹34.98
- **Total: ₹229.29**

### 8. Duration Calculations

```csharp
// Time span calculation
duration = (endTime ?? DateTime.UtcNow) - startTime

// Various formats
totalMinutes = Math.Round(duration.TotalMinutes, 0)
hours = duration.Hours
minutes = duration.Minutes
totalHours = Math.Round(duration.TotalHours, 2)

// Formatted string
formattedDuration = hours > 0 
    ? $"{hours}h {minutes}m" 
    : $"{minutes}m"
```

**Example:**
- Start: 2024-01-27 10:00:00
- End: 2024-01-27 11:15:00
- Duration: 1h 15m (75 minutes, 1.25 hours)

## API Response Structure

### GET /api/ChargingSession/charging-session-details/{sessionId}

```json
{
  "success": true,
  "message": "Charging session details retrieved successfully",
  "data": {
    "session": { /* Basic session info */ },
    "transactionId": 12345,
    "status": "Completed",
    "isActive": false,
    
    "meterReadings": {
      "startReading": 12.345,
      "currentReading": 27.890,
      "unit": "kWh"
    },
    
    "energyConsumption": {
      "totalEnergy": 15.545,
      "unit": "kWh",
      "description": "15.55 kWh delivered to vehicle"
    },
    
    "stateOfCharge": {
      "socChange": 15.55,
      "socChangePercentage": 38.9,
      "batteryCapacity": 40.0,
      "batteryCapacityUnit": "kWh",
      "estimatedRangeAdded": 70,
      "estimatedRangeUnit": "km",
      "description": "Battery charged by 38.9% (~70 km range added)"
    },
    
    "vehicle": {
      "manufacturer": "Tata Motors",
      "model": "Nexon EV",
      "variant": "Max",
      "registrationNumber": "KA01AB1234",
      "batteryCapacity": "40.5 kWh"
    },
    
    "chargingPerformance": {
      "averageChargingSpeed": 12.44,
      "peakChargingSpeed": 22.00,
      "unit": "kW",
      "chargingEfficiency": 56.5,
      "efficiencyUnit": "%",
      "description": "Average 12.4 kW charging at 56.5% efficiency"
    },
    
    "chargerDetails": {
      "chargerType": "Type 2 AC",
      "powerOutput": "22 kW",
      "chargerTariff": 12.50,
      "tariffUnit": "₹/kWh",
      "connectorId": "1",
      "chargerStatus": "Available"
    },
    
    "costDetails": {
      "energyCost": 194.31,
      "serviceFee": 0.00,
      "taxes": 34.98,
      "totalCost": 229.29,
      "currency": "₹",
      "tariffApplied": 12.50,
      "tariffUnit": "₹/kWh",
      "breakdown": "Energy: ₹194.31 + Tax: ₹34.98 = ₹229.29"
    },
    
    "timing": {
      "startTime": "2024-01-27T10:00:00Z",
      "endTime": "2024-01-27T11:15:00Z",
      "duration": {
        "totalMinutes": 75,
        "hours": 1,
        "minutes": 15,
        "totalHours": 1.25,
        "formattedDuration": "1h 15m"
      },
      "isActive": false,
      "lastUpdate": "2024-01-27T11:15:32Z"
    },
    
    "summary": {
      "energyDelivered": "15.55 kWh",
      "socGained": "38.9%",
      "rangeAdded": "~70 km",
      "totalCost": "₹229.29",
      "chargingTime": "1h 15m",
      "averageSpeed": "12.4 kW",
      "costPerKwh": "₹12.50"
    }
  }
}
```

## Real-Time Updates

For **active sessions** (not yet ended):
- `isActive`: true
- `meterCurrent`: Updated from OCPP transaction
- `energyConsumed`: Calculated in real-time
- `calculatedCost`: Current estimated cost
- `duration`: Time elapsed since start
- `endTime`: null

The calculations update each time the API is called, providing live charging progress.

## Data Flow

```
1. User starts charging
   ↓
2. ChargingGun tariff captured
   ↓
3. OCPP transaction created
   ↓
4. ChargingSession record created
   ↓
5. During charging:
   - OCPP updates meter readings
   - API calculates live metrics
   ↓
6. User stops charging
   ↓
7. Final calculations:
   - Energy consumed
   - SOC change (if vehicle data available)
   - Total cost
   - Wallet deduction
   ↓
8. Session completed
```

## Example Scenario

**User:** Charges Tata Nexon EV at a 22kW AC charger

**Vehicle Details:**
- Battery: 40.5 kWh
- Starting SOC: ~30%
- Target SOC: ~80%

**Charging Session:**
- Connector: Type 2 AC (22 kW max)
- Tariff: ₹12.50/kWh
- Duration: 1h 18m

**Results:**
- Energy Delivered: 18.2 kWh
- SOC Gained: 45% (from 30% to 75%)
- Range Added: ~82 km
- Average Speed: 14.0 kW
- Efficiency: 64%
- Cost: ₹270.15 (including 18% GST)

**Why 64% efficiency?**
- Battery nearly full (SOC > 70%)
- Charging power throttled for battery protection
- Normal for the final charging phase

## Error Handling

### Missing Data Scenarios

1. **No Battery Capacity**
   - SOC calculations skipped
   - Only show energy consumed
   - Still calculate cost and speed

2. **No ChargingGun Tariff**
   - Falls back to session tariff
   - Falls back to request tariff
   - Default: ₹0/kWh (with warning)

3. **No OCPP Transaction**
   - Use session meter readings
   - Status: "Pending"
   - Less accurate real-time data

4. **No Vehicle Data**
   - Skip vehicle-specific calculations
   - Still show energy and cost
   - Generic metrics only

## Best Practices

1. **Always set up ChargingGuns with accurate tariffs** before starting sessions
2. **Encourage users to add vehicle details** for SOC calculations
3. **Monitor charging efficiency** - values below 70% may indicate issues
4. **Use real-time updates** for active session monitoring
5. **Check battery capacity** matches vehicle specifications

## Testing Checklist

- [ ] Start session with correct tariff from ChargingGun
- [ ] Verify energy calculation from OCPP transaction
- [ ] Check SOC calculation with battery capacity
- [ ] Validate cost breakdown (energy + tax)
- [ ] Test real-time updates during charging
- [ ] Verify final calculations on session end
- [ ] Test missing data scenarios
- [ ] Validate range estimation
- [ ] Check efficiency calculations
- [ ] Test with different charger types and power levels

## Future Enhancements

1. **Dynamic Tariffs**: Time-of-use pricing
2. **Service Fees**: Platform charges
3. **Loyalty Programs**: Discounts and rewards
4. **Carbon Credits**: CO2 savings tracking
5. **Advanced Analytics**: Charging patterns, cost optimization
6. **Battery Health**: Degradation tracking
7. **Pre-authorization**: Reserve funds before charging
8. **Charging Predictions**: Estimate time and cost before starting
