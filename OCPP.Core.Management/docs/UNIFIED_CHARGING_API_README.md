# Unified Charging API

## What this is

HyCharge has always had two separate, fully-working charging systems:

- **Local (OCPP)** — `ChargingHubController` + `ChargingSessionController`. Manages our own charging hubs, stations, and guns, and starts/stops sessions on our own hardware over OCPP.
- **OCPI partner roaming** — `OcpiPartnerHubController`. Mirrors the same hub → station → gun shape, but sourced from partner CPOs' synced locations, and starts/stops sessions by proxying to the separate `OCPI.Core.Roaming` microservice.

These two systems are **structurally parallel and were never merged** — different database tables, different identifier schemes (local GUID `RecId` vs. partner numeric `Id`), and (until now) two separate API surfaces a caller had to know about and branch between.

`UnifiedChargingController` is a new **facade/aggregator layer on top of both**, added so a caller — the `chargev3` frontend, or any future integration — can browse, start, monitor, and stop charging sessions across both networks through **one set of endpoints and one DTO shape**, without needing to know or care which network a given charger belongs to.

**Nothing about the existing systems changed.** `ChargingHubController`, `ChargingSessionController`, and `OcpiPartnerHubController` are untouched and continue to work exactly as before for any existing consumer. `UnifiedChargingController` is purely additive.

## How it works: in-process delegation, not reimplementation

The naive way to build this would be to reimplement wallet billing, zombie-session cleanup, SoC caching, session-limit checks, and the OCPI proxy calls a second time inside the new controller. That would immediately drift out of sync with the original logic.

Instead, `UnifiedChargingController` constructs the existing controllers **in-process** and calls their action methods directly as plain C# calls — no HTTP round-trip, no duplicated logic:

```csharp
var sessionCtl = ActivatorUtilities.CreateInstance<ChargingSessionController>(HttpContext.RequestServices);
sessionCtl.ControllerContext = this.ControllerContext; // shares User/claims/HttpContext
var result = await sessionCtl.StartChargingSession(localRequestDto);
```

- `ActivatorUtilities.CreateInstance<T>` resolves the target controller's constructor dependencies (`OCPPCoreContext`, `ILogger<T>`, `IConfiguration`, `IHttpClientFactory`, etc.) from the current request's scoped service provider — the same `DbContext` instance, so entity tracking stays consistent across the delegated call.
- Sharing `ControllerContext` means `User.FindFirst(...)`, `User.IsInRole(...)`, and `ModelState` all behave identically to a normal request.
- The delegated controller's `IActionResult` is unwrapped (`ExtractResult`) and reshaped into the unified DTOs described below.

Net effect: every existing behavior — wallet debits, zombie-session cleanup, SoC polling, OCPI partner proxying, session-limit enforcement — keeps running through the exact same code path it always has. `UnifiedChargingController` only adds a routing + reshaping layer on top.

## Composite identifiers

Local and partner entities use incompatible identifier schemes (local GUID `RecId` vs. partner numeric `Id`), so every unified endpoint addresses a hub/station/connector/session through one **composite string id**:

| Format | Meaning |
|---|---|
| `L:{recId}` | Local hub / station / gun / session — `recId` is the existing GUID `RecId`. |
| `P:{numericId}` | Partner location / EVSE / connector — the numeric DB id from `OcpiPartnerLocation`/`OcpiPartnerEvse`/`OcpiPartnerConnector`. |
| `P:{sessionId}` | Partner session — the OCPI session id string. |

`Models/UnifiedCharging/UnifiedId.cs` provides `Encode(provider, nativeId)` / `TryParse(composite)` to build and decode these consistently. Every controller action starts by parsing the composite id to decide which of the two systems to route into.

## Endpoints

Base route: `api/UnifiedCharging`

| Method & route | Auth | What it does |
|---|---|---|
| `POST search-locations` | Anonymous | Merged geo-radius search across local hubs and partner hubs, sorted by distance. |
| `POST comprehensive-list` | Anonymous | Merged, paginated, nested hub → station → connector listing across both networks. |
| `GET location-details/{id}` | Anonymous | Full detail for one location (decodes the id prefix, routes to the matching system), with nested stations/connectors. |
| `GET connector-status/{id}` | Anonymous | Live status for a Local connector; a last-synced snapshot for a Partner connector (there's no live-poll endpoint on the partner side). |
| `POST start-session` | Authorize | Starts a session on whichever network the connector id resolves to. |
| `POST stop-session` | Authorize | Stops a session on whichever network the session id resolves to. |
| `GET session-details/{id}` | Authorize | Rich unified session detail — same shape for both networks (see below). |
| `GET sessions` | Authorize | Merged, paginated list of the caller's own sessions across both networks, sorted by start time. |
| `POST unlock-connector` | Authorize | Local connectors only. Partner connectors return a clear "not supported" message — see **Known gaps** below. |

### Starting a session

```http
POST /api/UnifiedCharging/start-session
{
  "connectorId": "L:3f2a1c9e-...",     // or "P:42"
  "chargeTagId": "AAA63CDF",
  "tokenUid": "AAA63CDF",              // only used for Partner connectors
  "energyLimit": 25,
  "costLimit": 500,
  "timeLimit": 120,
  "batteryIncreaseLimit": 80
}
```

The controller resolves the composite id (looking up the local `ChargingGuns` row or the `OcpiPartnerConnector` row as needed), builds the equivalent request the underlying controller already expects, and delegates. The response is a `UnifiedSessionDto` for Local sessions; for Partner sessions the OCPI roaming service assigns the real session id asynchronously, so the response's `data.raw` field carries the roaming service's response untouched rather than a fully-populated `UnifiedSessionDto` (see **Known gaps**).

### Session detail shape (`UnifiedSessionDto`)

This is the important part: **both networks return the same shape**, including two fields that previously only existed on one side:

```json
{
  "id": "L:...",
  "providerType": 0,               // 0 = Local, 1 = Partner (no string enum converter is configured)
  "status": "Active",
  "isActive": true,
  "startTime": "2026-07-15T09:12:00",
  "endTime": null,
  "meterStart": 12.4,
  "meterCurrent": 18.7,
  "energyDelivered": 6.3,
  "cost": 94.5,
  "currency": "INR",
  "locationName": "MG Road Hub",
  "partnerName": null,             // set for Partner sessions
  "stationId": "L:...",
  "connectorId": "L:...",
  "energyLimit": 25,
  "costLimit": 500,
  "timeLimit": 120,
  "batteryIncreaseLimit": 80,
  "limitProgress": { "energyPct": 25.2, "costPct": 18.9, "timePct": 10.0 },
  "batteryStateOfCharge": {
    "startSoC": 42.0, "endSoC": null, "currentSoC": 47.5, "soCGain": 5.5,
    "lastUpdate": "2026-07-15T09:20:00", "unit": "%", "isRealtime": true,
    "dataSource": "OCPP Server Cache (Live)"
  },
  "walletTransaction": { "...": "present when the session has been billed" },
  "raw": { "...": "untouched response from the delegated controller" }
}
```

Before this facade existed:
- **`limitProgress`** was only computed for partner sessions (`OcpiPartnerHubController`). `GetSessionDetails`/`GetSessions` now compute the same energy/cost/time percentage math for Local sessions too, closing that gap.
- **`batteryStateOfCharge`** was a rich object (start/end/gain/`isRealtime`/`dataSource`) for Local sessions only; Partner sessions only exposed a flat `currentStateOfCharge` percentage. Partner sessions are now mapped into the same rich shape, with `isRealtime: false` and `dataSource: "Partner CPO Report"` since that's genuinely all the partner CPO provides.

## Live offline correction for local chargers

`ChargingHubController.GetComprehensiveList` (which `comprehensive-list` delegates to for local hubs) counts a charger as "available" purely from `ChargingGuns.ChargerStatus` / `ConnectorStatus.LastStatus` in the DB. Those fields are only refreshed by the periodic `GunStatusService` background sync (every ~10 minutes, `GunStatus:CheckIntervalMinutes`) — so a charge point that just dropped its WebSocket connection keeps showing its last-known "Available" status until that next sync tick, even though it's actually offline right now.

`comprehensive-list` corrects this before mapping to the unified shape: `CorrectOfflineAvailabilityAsync` collects every distinct `ChargingPointId` in the local response, checks live connectivity for each (the same `ConnectionStatus` endpoint on `OCPP.Core.Server` that `GunStatusSyncService` uses, deduped per charge point and checked concurrently), and zeroes out `AvailableChargers` / marks connectors `"Offline"` for any station whose charge point is confirmed offline. This mutates the delegated controller's in-memory response only — it doesn't write to the DB or touch `ChargingHubController` itself, so the raw `/charginghub/comprehensive-list` endpoint's known-stale behavior is unchanged for any existing consumer.

One deliberate asymmetry versus `GunStatusSyncService`: that service **fails closed** (treats a failed connectivity check as offline) because it's scoped to one gun. This correction **fails open** (treats a failed check as online) because it drives a whole listing endpoint — a transient hiccup reaching the OCPP server must not make every local charger in the response look unavailable.

## Known gaps (by design, not oversights)

- **`connector-status` for Partner connectors is a snapshot, not live.** There's no live-poll endpoint for partner EVSEs anywhere in the codebase today — this returns whatever was last synced from the partner.
- **`unlock-connector` doesn't support Partner connectors.** The OCPI eMSP-role `UNLOCK_CONNECTOR` command isn't wired up anywhere in this codebase; the endpoint returns a clear rejection message rather than silently no-op-ing.
- **`start-session`'s response for a Partner connector is mostly a passthrough.** The OCPI roaming service assigns the real session id asynchronously (see `OcpiAdminController.EmspStartSession` in `OCPI.Core.Roaming`) and its exact response schema isn't owned by this facade, so it's forwarded as-is under `data.raw` rather than force-fit into `UnifiedSessionDto`.
- **`session-details/{id}` for a Partner session pages through `GetPartnerSessions`** (with a generous page size) to find the matching row, since that endpoint has no single-session filter. Fine for typical per-user session volumes; would need a dedicated single-session endpoint upstream to scale further.
- **`providerType` serializes as a plain number (0/1), not a string.** No `JsonStringEnumConverter` is configured in `Startup.cs`, so `ProviderType.Local`/`ProviderType.Partner` come across the wire as `0`/`1`. The frontend's `ChargingProviderType` enum in `services/http.ts` mirrors this exactly.

## Frontend

`ev-charging-front/src/app/admin/chargev3/` is the reference consumer. Unlike `chargev2` (which has a hard "Our Chargers" / "Partner Chargers" mode toggle and two parallel code paths), `chargev3` is a single flow: one location → station → connector picker mixing both networks (tagged with a small provider badge), one active-sessions list, one start/stop flow — calling only `/api/UnifiedCharging/*`. That's the actual payoff of building this facade: the frontend genuinely stops needing to know which network a charger belongs to.

`services/http.ts` has the matching TypeScript interfaces (`UnifiedLocationDto`, `UnifiedSessionDto`, etc.) and methods (`getUnifiedComprehensiveList`, `startUnifiedSession`, `getUnifiedSessions`, ...) in the `UNIFIED CHARGING` section.

## Files

| File | Purpose |
|---|---|
| `Controllers/UnifiedChargingController.cs` | The facade controller — all endpoints, delegation, and JSON reshaping helpers. |
| `Models/UnifiedCharging/UnifiedChargingDtos.cs` | The unified DTO family (`UnifiedLocationDto`, `UnifiedSessionDto`, request DTOs, etc.). |
| `Models/UnifiedCharging/UnifiedId.cs` | Composite id encode/decode helper. |
