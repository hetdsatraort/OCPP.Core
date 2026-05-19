-- ============================================================
-- Migration: Add ServiceTicket table
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'ServiceTicket'
)
BEGIN
    CREATE TABLE [dbo].[ServiceTicket] (
        [RecId]              NVARCHAR(50)   NOT NULL,
        [ServiceTicketId]    NVARCHAR(20)   NOT NULL,
        [UserId]             NVARCHAR(50)   NOT NULL,
        [Category]           NVARCHAR(50)   NOT NULL,
        [Subject]            NVARCHAR(200)  NOT NULL,
        [Description]        NVARCHAR(2000) NOT NULL,
        [Status]             NVARCHAR(20)   NOT NULL DEFAULT 'Open',
        [Priority]           NVARCHAR(10)   NOT NULL DEFAULT 'Medium',
        [RelatedSessionId]   NVARCHAR(50)   NULL,
        [AssignedToAdminId]  NVARCHAR(50)   NULL,
        [AdminNotes]         NVARCHAR(2000) NULL,
        [ResolutionNotes]    NVARCHAR(2000) NULL,
        [Active]             INT            NOT NULL DEFAULT 1,
        [CreatedOn]          DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedOn]          DATETIME2      NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_ServiceTicket] PRIMARY KEY ([RecId]),
        CONSTRAINT [UQ_ServiceTicket_ServiceTicketId] UNIQUE ([ServiceTicketId])
    );

    CREATE INDEX [IX_ServiceTicket_UserId]
        ON [dbo].[ServiceTicket] ([UserId]);

    CREATE INDEX [IX_ServiceTicket_Status]
        ON [dbo].[ServiceTicket] ([Status]);

    PRINT 'ServiceTicket table created successfully.';
END
ELSE
BEGIN
    PRINT 'ServiceTicket table already exists, skipping.';
END
