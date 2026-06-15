# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a multi-tier EV charging management platform branded as **HyCharge**. It implements the OCPP (Open Charge Point Protocol) and OCPI (Open Charge Point Interface) standards for managing EV charging infrastructure.

## Repository Structure

```
EV Charging/
├── ev-charging-front/     # Angular 21 frontend (admin dashboard + user app)
├── OCPP.Core/             # .NET 8 backend (3 sub-projects)
│   ├── OCPP.Core.Server/       # WebSocket OCPP protocol handler
│   ├── OCPP.Core.Management/   # REST API + JWT auth
│   ├── OCPP.Core.Database/     # EF Core entities + DbContext
│   └── OCPI.Core.Roaming/      # OCPI roaming protocol server
├── node-ort-rzp/          # Node.js/Express Razorpay payment gateway wrapper
└── Publish/               # Deployment build artifacts
```

## Commands

### Frontend (ev-charging-front)

```bash
cd ev-charging-front
npm install
ng serve                          # Dev server at http://localhost:4200
ng build                          # Production build with SSR
ng test                           # Run tests (Vitest)
node dist/ev-charging-front/server/server.mjs   # Run SSR server
```

### .NET Backend (OCPP.Core)

```bash
cd OCPP.Core
dotnet restore
dotnet build
dotnet run --project OCPP.Core.Server      # OCPP WebSocket server (HTTP:8081, HTTPS:8091)
dotnet run --project OCPP.Core.Management  # Management REST API (HTTP:8082, HTTPS:8092)
dotnet run --project OCPI.Core.Roaming     # OCPI roaming server (port 6100)

# Entity Framework migrations
dotnet ef migrations add <MigrationName> --project OCPP.Core.Database --startup-project OCPP.Core.Management
dotnet ef database update --project OCPP.Core.Database --startup-project OCPP.Core.Management
```

### Payment Gateway (node-ort-rzp)

```bash
cd node-ort-rzp
npm install
node app.js
```

## Architecture

### Backend: Three .NET 8 Services

**OCPP.Core.Server** — Handles the WebSocket OCPP protocol. Charge points (physical hardware) connect here. It routes OCPP 1.6, 2.0, and 2.1 messages through `OCPPMiddleware.cs` to protocol-specific controllers (`ControllerOCPP16.cs`, `ControllerOCPP20.cs`, `ControllerOCPP21.cs`). Supported messages include BootNotification, Authorize, StartTransaction, StopTransaction, MeterValues, StatusNotification, and remote commands (RemoteStart, RemoteStop, Reset, UnlockConnector).

**OCPP.Core.Management** — REST API consumed by the Angular frontend. Provides JWT authentication, charge point/tag/connector management, session tracking, hub management, and hardware master data. `ChargingSessionController.cs` (182KB) and `ChargingHubController.cs` (74KB) contain the heaviest business logic.

**OCPI.Core.Roaming** — Implements the OCPI protocol for roaming partnerships with other charging networks. Manages partner credentials, location sharing, token exchange, and CDR (Charge Data Record) handling.

All three services share **OCPP.Core.Database** — a single EF Core `OCPPCoreContext` targeting SQL Server (production) or SQLite (local dev). The database address is `103.87.173.185:1436`, database `OCPP.Core`.

### Frontend: Angular 21

Feature-based structure under `src/app/admin/`:
- **auth/** — Login, register, OTP login, password reset
- **user/** — User dashboard, wallet, charging sessions, reports
- **evc/** — Charging infrastructure management (hubs, stations, guns, simulators)
- **charge/** and **chargev2/** — Charging operation flows
- **user-management/** — Admin user management
- **ocpi-roaming/** and **ocpi-partners/** — OCPI roaming UI

Key cross-cutting concerns:
- `services/http.ts` — Single HTTP service with all API interfaces and typed request/response DTOs
- `interceptors/auth.interceptor.ts` — Injects JWT Bearer token on all outgoing requests
- `guards/auth.guard.ts` — Route protection with SSR awareness (uses `isPlatformBrowser`)
- `environments/environment.ts` — API base URLs and OCPP WebSocket URL

Routing is fully lazy-loaded. The OCPP WebSocket URL (`wss://evc-backend.ortdemo.com/OCPP/`) is used directly in the simulators feature.

### Payment: Node.js/Express

Wraps Razorpay APIs with a PostgreSQL backing store. Handles order creation, customer management, subscriptions, and Razorpay webhooks. On payment confirmation, it forwards authorization to the OCPP Management API to start/validate a charging session.

### CORS Origins

```
http://localhost:4200
https://evc-admin.ortdemo.com    (UAT)
https://admin.hycharge.in        (prod)
https://charge.hycharge.in
https://app.hycharge.in
```

## Key Domain Concepts

- **ChargePoint** — A physical charging station connecting via OCPP WebSocket
- **ChargingHub** — A logical grouping of charging stations at one location
- **ChargingGun** — An individual connector/port on a station
- **ChargeTag** — An RFID tag used to authorize charging
- **ChargingSession** — A user's active or completed charging session
- **Transaction** — The OCPP-level record of energy delivery
- **OcpiPartner** — An external charging network connected via OCPI roaming
- **CDR** (Charge Data Record) — Post-session billing record in OCPI

## Configuration

Frontend API endpoints are set in `ev-charging-front/src/environments/environment.ts`.

Backend config is in `appsettings.json` for each project. Key settings:
- `OCPPServer:ApiBaseUrl` — Management API URL used by OCPP Server for session callbacks
- `JWT:*` — Token signing key, issuer, expiry
- `Razorpay:*` — Payment gateway credentials
- `SmsSenderId` / `SmsApiKey` — KeepInTouch SMS gateway for OTP and notifications
