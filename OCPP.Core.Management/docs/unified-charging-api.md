# Unified Charging API — Android Integration Guide

## 1. Why this API exists

HyCharge actually has **two separate, fully-working charging backends**:

| | Local (OCPP) | Partner (OCPI roaming) |
|---|---|---|
| Controller | `ChargingHubController` + `ChargingSessionController` | `OcpiPartnerHubController` |
| Covers | Our own hubs/stations/guns, our own hardware | Partner CPOs' synced locations, proxied to the `OCPI.Core.Roaming` microservice |
| Id scheme | GUID `RecId` | numeric DB id (locations/EVSEs/connectors) or OCPI session id string |

These were never merged — different tables, different id types, two API surfaces to branch between.

**`UnifiedChargingController`** sits on top of both as a facade. It's one set of endpoints, one JSON shape, so the app never needs to know or care which network a given charger belongs to. It doesn't reimplement wallet billing, session limits, SoC polling, etc. — it calls the two existing controllers in-process and reshapes their responses.

Base route for everything below: **`api/UnifiedCharging`**

## 2. The one concept you need: composite IDs

Every hub, station, connector, and session is addressed by a **string id with a prefix**:

- `L:{recId}` — Local entity, e.g. `L:3f2a1c9e-4b7d-4e21-9a3f-7c1d2e5f8a90`
- `P:{numericId}` — Partner location/EVSE/connector, e.g. `P:42`
- `P:{sessionId}` — Partner session (the OCPI session id string), e.g. `P:OCPI-SESSION-998877`

The Android app should treat this as an **opaque string** — store it, echo it back on subsequent calls — never parse or construct it. Every response you get back already has the correct prefix baked in.

`providerType` in the JSON is `0` for Local, `1` for Partner (plain int, no string enum — mirror `ChargingProviderType` from the web frontend's `services/http.ts`).

## 3. Typical flow

```
search-locations  or  comprehensive-list
        │
        ▼
location-details/{id}          (optional drill-down)
        │
        ▼
connector-status/{id}          (optional live check right before starting)
        │
        ▼
start-session      ──────────────►  POST body has ConnectorId
        │
        ▼
session-details/{id}  (poll)   or   sessions  (list all active)
        │
        ▼
stop-session
```

Endpoints under **Locations** are `[AllowAnonymous]`. Endpoints under **Sessions** (start/stop/details/list/unlock/vehicle-link) require `[Authorize]` — send the JWT bearer token, same as every other authenticated call in the app.

---

## 4. Endpoints, with sample JSON for both flows

### 4.1 `POST search-locations` — anonymous

Merged geo-radius search, sorted by distance. Flat list only — no nested stations here (use `comprehensive-list` for that).

**Request**
```json
{
  "latitude": 12.9716,
  "longitude": 77.5946,
  "radiusKm": 25
}
```

**Response** (one Local hub + one Partner hub, merged and distance-sorted)
```json
{
  "success": true,
  "message": "Found 2 location(s) within 25km",
  "locations": [
    {
      "id": "L:3f2a1c9e-4b7d-4e21-9a3f-7c1d2e5f8a90",
      "providerType": 0,
      "name": "MG Road Hub",
      "addressLine1": "12 MG Road",
      "city": "Bengaluru",
      "state": "Karnataka",
      "pincode": "560001",
      "latitude": "12.9750",
      "longitude": "77.6050",
      "distanceKm": 1.2,
      "averageRating": 4.5,
      "totalStations": 4,
      "availableStations": 4,
      "totalConnectors": 0,
      "availableConnectors": 0,
      "partnerName": null,
      "stations": []
    },
    {
      "id": "P:42",
      "providerType": 1,
      "name": "Indiranagar Fast Charge",
      "addressLine1": "100 Ft Road",
      "city": "Bengaluru",
      "state": "Karnataka",
      "pincode": "560038",
      "latitude": "12.9784",
      "longitude": "77.6408",
      "distanceKm": 3.8,
      "averageRating": null,
      "totalStations": 6,
      "availableStations": 4,
      "totalConnectors": 0,
      "availableConnectors": 0,
      "partnerName": "ChargeNet India",
      "stations": []
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 2
}
```
> Note `totalConnectors` / `availableConnectors` / `stations` are left empty on this endpoint by design — it's a pin-drop map search, not a detail view.

---

### 4.2 `POST comprehensive-list` — anonymous

Merged, paginated, **nested** hub → station → connector listing. This is what powers a "browse chargers" screen.

**Request**
```json
{
  "page": 1,
  "pageSize": 10,
  "latitude": 12.9716,
  "longitude": 77.5946,
  "radiusKm": 25,
  "searchTerm": null,
  "city": null,
  "state": null
}
```

**Response**
```json
{
  "success": true,
  "message": "Found 2 location(s) matching criteria",
  "locations": [
    {
      "id": "L:3f2a1c9e-4b7d-4e21-9a3f-7c1d2e5f8a90",
      "providerType": 0,
      "name": "MG Road Hub",
      "addressLine1": "12 MG Road",
      "city": "Bengaluru",
      "distanceKm": 1.2,
      "totalStations": 1,
      "availableStations": 1,
      "totalConnectors": 2,
      "availableConnectors": 2,
      "partnerName": null,
      "stations": [
        {
          "id": "L:9c7e2b41-1234-4a11-8b2c-1234567890ab",
          "providerType": 0,
          "name": "Station A",
          "totalConnectors": 2,
          "availableConnectors": 2,
          "connectors": [
            {
              "id": "L:1a2b3c4d-5678-4e21-9a3f-abcdef123456",
              "providerType": 0,
              "connectorId": "1",
              "chargerTypeName": "CCS2",
              "powerOutput": "60",
              "tariff": "18.50",
              "status": "Available",
              "lastUpdated": "2026-07-23T05:40:00Z"
            }
          ]
        }
      ]
    },
    {
      "id": "P:42",
      "providerType": 1,
      "name": "Indiranagar Fast Charge",
      "partnerName": "ChargeNet India",
      "distanceKm": 3.8,
      "totalStations": 1,
      "availableStations": 1,
      "totalConnectors": 1,
      "availableConnectors": 1,
      "stations": [
        {
          "id": "P:501",
          "providerType": 1,
          "name": "EVSE-501",
          "totalConnectors": 1,
          "availableConnectors": 1,
          "connectors": [
            {
              "id": "P:9001",
              "providerType": 1,
              "connectorId": "1",
              "chargerTypeName": "CCS2",
              "powerOutput": "50.00",
              "tariff": null,
              "status": "AVAILABLE",
              "lastUpdated": "2026-07-23T05:35:00Z"
            }
          ]
        }
      ]
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```
> `tariff` is always `null` for Partner connectors — the partner CPO sets pricing and it's only known after the fact via the CDR. Don't show a per-kWh rate for Partner chargers up front.
>
> Local `status` values come from OCPP (`Available`, `Occupied`, `Faulted`, `Offline`, ...); Partner `status` values are OCPI's own vocabulary (`AVAILABLE`, `CHARGING`, ...). The casing differs by design — match case-insensitively if you're branching UI on status text.

---

### 4.3 `GET location-details/{id}` — anonymous

Single location, same nested shape as one entry above, plus full station/connector detail.

```
GET /api/UnifiedCharging/location-details/L:3f2a1c9e-4b7d-4e21-9a3f-7c1d2e5f8a90
GET /api/UnifiedCharging/location-details/P:42
```

**Response** (either flow — identical envelope, `data` is a single `UnifiedLocationDto` as shown in 4.2)
```json
{
  "success": true,
  "message": "Charging hub details retrieved successfully",
  "data": {
    "id": "L:3f2a1c9e-4b7d-4e21-9a3f-7c1d2e5f8a90",
    "providerType": 0,
    "name": "MG Road Hub",
    "stations": [ "... same shape as 4.2 ..." ]
  }
}
```

---

### 4.4 `GET connector-status/{id}` — anonymous

Live poll for Local; last-synced snapshot for Partner (there is no live-poll endpoint on the OCPI side today — don't build a "refresh" spinner expecting Partner status to change on demand).

**Local**
```
GET /api/UnifiedCharging/connector-status/L:1a2b3c4d-5678-4e21-9a3f-abcdef123456
```
```json
{
  "success": true,
  "message": "Gun status retrieved successfully",
  "data": {
    "providerType": 0,
    "live": true,
    "status": { "...": "raw live status payload from the OCPP server" }
  }
}
```

**Partner**
```
GET /api/UnifiedCharging/connector-status/P:9001
```
```json
{
  "success": true,
  "message": "Partner connector status is a last-synced snapshot, not a live poll",
  "data": {
    "providerType": 1,
    "live": false,
    "status": "AVAILABLE",
    "lastUpdated": "2026-07-23T05:35:00Z"
  }
}
```
> Check `data.live` before you trust the status as real-time.

---

### 4.5 `POST start-session` — requires `Authorize`

**Local request** (`connectorId` resolves to a `ChargingGuns` row)
```json
{
  "connectorId": "L:1a2b3c4d-5678-4e21-9a3f-abcdef123456",
  "chargeTagId": "AAA63CDF",
  "energyLimit": 25,
  "costLimit": 500,
  "timeLimit": 120,
  "batteryIncreaseLimit": 80
}
```

**Local response** — a fully-populated `UnifiedSessionDto`, ready to render:
```json
{
  "success": true,
  "message": "Charging session started successfully",
  "data": {
    "id": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
    "providerType": 0,
    "status": "Active",
    "isActive": true,
    "startTime": "2026-07-23T05:45:00Z",
    "endTime": null,
    "meterStart": 0,
    "energyDelivered": 0,
    "cost": 0,
    "currency": "INR",
    "stationId": "L:9c7e2b41-1234-4a11-8b2c-1234567890ab",
    "connectorId": "L:1a2b3c4d-5678-4e21-9a3f-abcdef123456",
    "energyLimit": 25,
    "costLimit": 500,
    "timeLimit": 120,
    "batteryIncreaseLimit": 80,
    "batteryStateOfCharge": {
      "startSoC": 42.0,
      "currentSoC": 42.0,
      "lastUpdate": "2026-07-23T05:45:00Z",
      "unit": "%",
      "isRealtime": true,
      "dataSource": "OCPP Server Cache (Live)"
    },
    "walletTransaction": null,
    "raw": { "...": "untouched response from ChargingSessionController" }
  }
}
```

**Partner request** — `tokenUid` is used instead of/alongside `chargeTagId`:
```json
{
  "connectorId": "P:9001",
  "chargeTagId": "AAA63CDF",
  "tokenUid": "AAA63CDF",
  "energyLimit": 25,
  "costLimit": 500,
  "timeLimit": 120,
  "batteryIncreaseLimit": 80
}
```

**Partner response** — ⚠️ **this is the one place the two flows genuinely diverge.** OCPI assigns the real session id *asynchronously*, so this response is **not** a `UnifiedSessionDto** — it's a thin envelope with the raw roaming-service reply under `data.raw`:
```json
{
  "success": true,
  "message": "Session start request accepted",
  "data": {
    "providerType": 1,
    "connectorId": "P:9001",
    "raw": {
      "...": "untouched OCPI roaming service response — no usable session id yet"
    }
  }
}
```
**Android implication:** after a Partner start, don't try to read a session id out of this response. Instead, poll `GET /sessions?status=Active` (4.8) a few seconds later and pick up the newly-appeared `P:{sessionId}` entry — same pattern the web frontend (`chargev3`) uses.

---

### 4.6 `POST stop-session` — requires `Authorize`

**Local**
```json
{
  "sessionId": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
  "endMeterReading": "6300"
}
```
```json
{
  "success": true,
  "message": "Charging session ended successfully",
  "data": {
    "id": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
    "providerType": 0,
    "status": "Completed",
    "isActive": false,
    "startTime": "2026-07-23T05:45:00Z",
    "endTime": "2026-07-23T06:15:00Z",
    "meterStart": 0,
    "meterCurrent": 6300,
    "energyDelivered": 6.3,
    "cost": 94.5,
    "currency": "INR",
    "batteryStateOfCharge": {
      "startSoC": 42.0,
      "endSoC": 47.5,
      "soCGain": 5.5,
      "lastUpdate": "2026-07-23T06:15:00Z",
      "unit": "%",
      "isRealtime": true,
      "dataSource": "OCPP Server Cache (Live)"
    },
    "walletTransaction": {
      "transactionId": "TXN123456",
      "previousBalance": 500.0,
      "amountDebited": 94.5,
      "newBalance": 405.5
    },
    "raw": { "...": "" }
  }
}
```

**Partner** — `endMeterReading` is ignored (OCPI has no equivalent field):
```json
{
  "sessionId": "P:OCPI-SESSION-998877"
}
```
```json
{
  "success": true,
  "message": "Stop request sent to partner",
  "data": {
    "providerType": 1,
    "sessionId": "P:OCPI-SESSION-998877",
    "raw": { "...": "untouched OCPI stop response" }
  }
}
```

---

### 4.7 `GET session-details/{id}` — requires `Authorize`

Rich detail, same shape for both networks — this is the endpoint to poll for a live "charging in progress" screen.

**Local**
```
GET /api/UnifiedCharging/session-details/L:9f8e7d6c-2222-4a11-8b2c-abcdef654321
```
```json
{
  "success": true,
  "message": "Session details retrieved successfully",
  "data": {
    "id": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
    "providerType": 0,
    "status": "Active",
    "isActive": true,
    "startTime": "2026-07-23T05:45:00Z",
    "meterStart": 0,
    "meterCurrent": 3150,
    "energyDelivered": 3.15,
    "cost": 47.25,
    "currency": "INR",
    "locationName": "MG Road Hub",
    "stationId": "L:9c7e2b41-1234-4a11-8b2c-1234567890ab",
    "connectorId": "L:1a2b3c4d-5678-4e21-9a3f-abcdef123456",
    "energyLimit": 25,
    "costLimit": 500,
    "timeLimit": 120,
    "limitProgress": { "energyPct": 12.6, "costPct": 9.5, "timePct": 15.0 },
    "batteryStateOfCharge": {
      "startSoC": 42.0,
      "currentSoC": 44.7,
      "soCGain": 2.7,
      "lastUpdate": "2026-07-23T06:03:00Z",
      "unit": "%",
      "isRealtime": true,
      "dataSource": "OCPP Server Cache (Live)"
    },
    "walletTransaction": null,
    "raw": { "...": "" }
  }
}
```

**Partner**
```
GET /api/UnifiedCharging/session-details/P:OCPI-SESSION-998877
```
```json
{
  "success": true,
  "message": "Partner session details retrieved successfully",
  "data": {
    "id": "P:OCPI-SESSION-998877",
    "providerType": 1,
    "status": "ACTIVE",
    "isActive": true,
    "startTime": "2026-07-23T05:50:00Z",
    "energyDelivered": 2.1,
    "cost": 33.6,
    "currency": "INR",
    "locationName": "Indiranagar Fast Charge",
    "partnerName": "ChargeNet India",
    "connectorId": "9001",
    "energyLimit": 25,
    "costLimit": 500,
    "timeLimit": 120,
    "limitProgress": { "energyPct": 8.4, "costPct": 6.7, "timePct": 10.0 },
    "batteryStateOfCharge": {
      "currentSoC": 55.0,
      "lastUpdate": "2026-07-23T06:00:00Z",
      "unit": "%",
      "isRealtime": false,
      "dataSource": "Partner CPO Report"
    },
    "raw": { "...": "" }
  }
}
```
> ⚠️ Field quirk: on a Partner session, `connectorId` is the *raw* OCPI EVSE uid (`"9001"`), **not** a composite `P:9001` id — unlike everywhere else in this API. Don't feed it straight back into `connector-status/{id}` without adding the `P:` prefix yourself.
>
> `batteryStateOfCharge.isRealtime` is always `false` for Partner — it's a snapshot from the partner's CDR/report, not a live cache. Show it as "last reported" rather than "live" in the UI.

---

### 4.8 `GET sessions?status=&page=&pageSize=` — requires `Authorize`

Merged, paginated list of the caller's own sessions, across both networks, newest-first. This is also how you discover the session id assigned to a just-started Partner session (see 4.5).

```
GET /api/UnifiedCharging/sessions?status=Active&page=1&pageSize=20
```
```json
{
  "success": true,
  "message": "Sessions retrieved successfully",
  "sessions": [
    {
      "id": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
      "providerType": 0,
      "status": "Active",
      "isActive": true,
      "startTime": "2026-07-23T05:45:00Z",
      "energyDelivered": 3.15,
      "cost": 47.25,
      "locationName": "MG Road Hub"
    },
    {
      "id": "P:OCPI-SESSION-998877",
      "providerType": 1,
      "status": "ACTIVE",
      "isActive": true,
      "startTime": "2026-07-23T05:50:00Z",
      "energyDelivered": 2.1,
      "cost": 33.6,
      "locationName": "Indiranagar Fast Charge",
      "partnerName": "ChargeNet India"
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```
`status` filter values differ by convention (`Active`/`Completed` for Local vs. OCPI's own `ACTIVE`/`COMPLETED`) — the controller upper-cases whatever you pass before forwarding it to the Partner side, so just send the human-readable value and both sides get handled correctly.

---

### 4.9 `POST unlock-connector` — requires `Authorize`, **Local only**

```
{ "connectorId": "L:1a2b3c4d-5678-4e21-9a3f-abcdef123456" }
```
```json
{
  "success": true,
  "message": "Connector unlocked successfully",
  "data": { "...": "" }
}
```

Partner connectors return a clean rejection rather than silently no-op-ing — **check `success` and show the message, don't assume it worked:**
```
{ "connectorId": "P:9001" }
```
```json
{
  "success": false,
  "message": "Unlock is not supported for partner (OCPI) connectors in this deployment."
}
```

---

### 4.10 `POST link-session-vehicle` / `GET session-vehicle/{id}` — requires `Authorize`, **Local only**

Same pattern — Partner sessions have nowhere to persist a vehicle link, so they get a clear rejection instead of a silent failure.

**Local — link**
```json
{ "sessionId": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321", "vehicleId": "VEH-001" }
```
```json
{
  "success": true,
  "message": "Vehicle linked to session successfully",
  "data": {
    "sessionId": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
    "vehicle": {
      "recId": "VEH-001",
      "make": "Tata",
      "model": "Nexon EV",
      "registrationNumber": "KA-01-AB-1234"
    }
  }
}
```

**Local — read**
```
GET /api/UnifiedCharging/session-vehicle/L:9f8e7d6c-2222-4a11-8b2c-abcdef654321
```
```json
{
  "success": true,
  "message": "Vehicle retrieved successfully",
  "data": {
    "sessionId": "L:9f8e7d6c-2222-4a11-8b2c-abcdef654321",
    "vehicle": { "recId": "VEH-001", "make": "Tata", "model": "Nexon EV", "registrationNumber": "KA-01-AB-1234" }
  }
}
```

**Partner — either call**
```json
{
  "success": false,
  "message": "Linking a vehicle is not supported for partner (OCPI) sessions in this deployment."
}
```

---

## 5. Things that will bite an Android dev if missed

1. **`providerType` is `0`/`1`, not a string.** No JSON string-enum converter is configured server-side. Map it to an enum client-side; don't switch on a string.
2. **Composite ids are opaque.** Never split on `:` or interpret the part after it — just round-trip the whole string.
3. **Partner `start-session` does not return a session id.** You must poll `GET /sessions` to discover it (4.5 / 4.8). Don't block a "session started" screen on a session id being present in the start response for Partner flows.
4. **Partner `session-details.connectorId` is a bare id, not a composite one** (4.7) — the one shape inconsistency in the whole API. Prefix it with `P:` yourself if you need to call `connector-status` with it.
5. **`unlock-connector`, `link-session-vehicle`, and `session-vehicle` all reject Partner ids with `success: false` and a descriptive message.** Don't hide or grey out these buttons for Partner chargers — showing them and surfacing the rejection message is the intended UX; the backend won't silently no-op.
6. **Partner `connector-status` is a snapshot, not live** — don't wire a manual "refresh status" action to it expecting new data on every tap.
7. **Every failure path still returns HTTP 200** with `success: false` and a `message` — the controller deliberately avoids non-200s so callers don't need special-case error handling per status code. Always check `success`, never rely on HTTP status alone.

## 6. Where this lives in the codebase (for reference, not needed to consume the API)

| File | Purpose |
|---|---|
| `OCPP.Core.Management/Controllers/UnifiedChargingController.cs` | The facade controller — all endpoints. |
| `OCPP.Core.Management/Models/UnifiedCharging/UnifiedChargingDtos.cs` | The unified DTO family. |
| `OCPP.Core.Management/Models/UnifiedCharging/UnifiedId.cs` | Composite id encode/decode. |
| `OCPP.Core.Management/docs/UNIFIED_CHARGING_API_README.md` | Full internal design writeup (this doc is the condensed, Android-focused version of it). |
| `ev-charging-front/src/app/admin/chargev3/` | The reference web consumer — same endpoints, single-flow UI. |
| `ev-charging-front/src/app/services/http.ts` | TypeScript interfaces/methods for this API (`UNIFIED CHARGING` section) — useful as a second reference alongside this doc. |
