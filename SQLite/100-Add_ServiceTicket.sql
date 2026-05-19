-- ============================================================
-- Migration: Add ServiceTicket table (SQLite)
-- ============================================================

CREATE TABLE IF NOT EXISTS "ServiceTicket" (
    "RecId"              TEXT    NOT NULL,
    "ServiceTicketId"    TEXT    NOT NULL,
    "UserId"             TEXT    NOT NULL,
    "Category"           TEXT    NOT NULL,
    "Subject"            TEXT    NOT NULL,
    "Description"        TEXT    NOT NULL,
    "Status"             TEXT    NOT NULL DEFAULT 'Open',
    "Priority"           TEXT    NOT NULL DEFAULT 'Medium',
    "RelatedSessionId"   TEXT,
    "AssignedToAdminId"  TEXT,
    "AdminNotes"         TEXT,
    "ResolutionNotes"    TEXT,
    "Active"             INTEGER NOT NULL DEFAULT 1,
    "CreatedOn"          TEXT    NOT NULL DEFAULT (datetime('now')),
    "UpdatedOn"          TEXT    NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY ("RecId")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_ServiceTicket_ServiceTicketId"
    ON "ServiceTicket" ("ServiceTicketId");

CREATE INDEX IF NOT EXISTS "IX_ServiceTicket_UserId"
    ON "ServiceTicket" ("UserId");

CREATE INDEX IF NOT EXISTS "IX_ServiceTicket_Status"
    ON "ServiceTicket" ("Status");
