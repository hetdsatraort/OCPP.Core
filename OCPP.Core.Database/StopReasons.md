Stop reasons are now recorded as structured data at the moment a session is stopped. Charging Sessions, Partner Sessions and Transactions all read that data, and fall back to the wallet text only for sessions ended before this change. The IDE shows no errors in the files I checked; nothing has been built.

Before deploying, you need to add and run a migration. It creates six nullable columns: StopReason (50) and StopReasonDetail (200) on ChargingSessions, OcpiPartnerSession and OcpiHostedSession. No backfill is needed:


dotnet ef migrations add SessionStopReasons --project OCPP.Core.Database --startup-project OCPP.Core.Management
dotnet ef database update --project OCPP.Core.Database --startup-project OCPP.Core.Management
One shared definition. SessionStopReasons.cs in the Database project holds the codes and one Describe() method for label, category and description:

AppStop
LimitReached (detail lists the limits, e.g. "Energy, Cost")
ChargerEnded
ZeroEnergy
PartnerStop
ChargerDisconnected
SystemClosed
Management, Roaming and all three pages use it, so the wording is the same everywhere.

Where each reason is written

Flow	Where	Code written
OCPP app stop	End charging session	AppStop, or ChargerEnded if the OCPP server says the transaction was already closed
OCPP limit stop	Session limit check	LimitReached + the violated limits
OCPI app stop	OcpiAdminController stop	AppStop (won't overwrite a limit stop already in progress)
OCPI limit stop	OcpiOrphanSessionService	LimitReached + the violated limits
Partner's customer stops at our charger	HandleStopSessionAsync	PartnerStop on the hosted session
System closes	Zero energy, zombie/stuck sessions, charger offline, invalidated, ended by charger or partner	The matching code
The app stop no longer needs a timing guess. The OCPP server's reply to the stop call says directly whether the charger had already ended the transaction.

Reading it back

SessionStopReasonMapper.cs uses the stored code first. Only when it is null does it fall back to the wallet text (OCPP) or the existing limit/stop-attempt flags (OCPI). It's now the only code that reads the wallet text, and anything it infers is marked isDerived.
The charging-session list and details APIs, and all three partner-session endpoints, now return a stopReason object.
The Transactions resolver now reads these stored reasons instead of parsing wallet text.
Frontend

Charging Sessions: the old "Auto-Closed" panel, which worked by parsing wallet text, is replaced by a "How this session ended" section for every ended session. The limit tags now come from the stored detail, and the transaction notes always show. The list shows the reason under the status chip.
Partner Sessions: the same reason line in the list (shown as "Stopping: …" while a stop is pending), plus a "How this session ended" section in the details dialog.
Transactions: a new purple colour for partner stops, and the tooltip says when a reason was inferred from older data.
Still worth knowing

Older sessions: the wallet fallback can't tell an app stop from a charger stop the app was tidying up after. Every session ended from now on is recorded exactly.
OCPI partner sessions: if a partner charger ends a session while our app's stop is still pending, it shows as "Stopped via app". The app's request is what we recorded, and OCPI doesn't report which happened first.