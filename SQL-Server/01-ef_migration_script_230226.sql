BEGIN TRANSACTION;
GO

ALTER TABLE [ConnectorStatus] ADD [Active] int NOT NULL DEFAULT 1;
GO

CREATE TABLE [BatteryCapacityMasters] (
    [RecId] nvarchar(50) NOT NULL,
    [BatteryCapcacity] nvarchar(50) NOT NULL,
    [BatteryCapcacityUnit] nvarchar(20) NULL,
    [Active] int NOT NULL DEFAULT 1,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_BatteryCapacityMasters] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [BatteryTypeMasters] (
    [RecId] nvarchar(50) NOT NULL,
    [BatteryType] nvarchar(100) NOT NULL,
    [Active] int NOT NULL DEFAULT 1,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_BatteryTypeMasters] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [CarManufacturerMasters] (
    [RecId] nvarchar(50) NOT NULL,
    [ManufacturerName] nvarchar(100) NOT NULL,
    [ManufacturerLogoImage] nvarchar(50) NULL,
    [Active] int NOT NULL DEFAULT 1,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_CarManufacturerMasters] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [ChargerTypeMasters] (
    [RecId] nvarchar(50) NOT NULL,
    [ChargerType] nvarchar(100) NOT NULL,
    [ChargerTypeImage] nvarchar(50) NULL,
    [Additional_Info_1] nvarchar(200) NULL,
    [Active] int NOT NULL DEFAULT 1,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_ChargerTypeMasters] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [ChargingHubs] (
    [RecId] nvarchar(50) NOT NULL,
    [AddressLine1] nvarchar(200) NULL,
    [AddressLine2] nvarchar(200) NULL,
    [AddressLine3] nvarchar(200) NULL,
    [ChargingHubImage] nvarchar(50) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(100) NULL,
    [Pincode] nvarchar(20) NULL,
    [Latitude] nvarchar(50) NULL,
    [Longitude] nvarchar(50) NULL,
    [OpeningTime] time NOT NULL,
    [ClosingTime] time NOT NULL,
    [TypeATariff] nvarchar(50) NULL,
    [TypeBTariff] nvarchar(50) NULL,
    [Amenities] nvarchar(500) NULL,
    [AdditionalInfo1] nvarchar(200) NULL,
    [AdditionalInfo2] nvarchar(200) NULL,
    [AdditionalInfo3] nvarchar(200) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_ChargingHubs] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [ChargingSessions] (
    [RecId] nvarchar(50) NOT NULL,
    [ChargingGunId] nvarchar(50) NULL,
    [ChargingStationID] nvarchar(50) NULL,
    [StartMeterReading] nvarchar(50) NULL,
    [EndMeterReading] nvarchar(50) NULL,
    [EnergyTransmitted] nvarchar(50) NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NOT NULL,
    [ChargingSpeed] nvarchar(50) NULL,
    [ChargingTariff] nvarchar(50) NULL,
    [ChargingTotalFee] nvarchar(50) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_ChargingSessions] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [EVModelMasters] (
    [RecId] nvarchar(50) NOT NULL,
    [ModelName] nvarchar(100) NULL,
    [ManufacturerId] nvarchar(50) NULL,
    [Variant] nvarchar(100) NULL,
    [BatterytypeId] nvarchar(50) NULL,
    [BatteryCapacityId] nvarchar(50) NULL,
    [CarModelImage] nvarchar(50) NULL,
    [TypeASupport] nvarchar(10) NULL,
    [TypeBSupport] nvarchar(10) NULL,
    [ChadeMOSupport] nvarchar(10) NULL,
    [CCSSupport] nvarchar(10) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_EVModelMasters] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [Users] (
    [RecId] nvarchar(50) NOT NULL,
    [FirstName] nvarchar(100) NULL,
    [LastName] nvarchar(100) NULL,
    [EMailID] nvarchar(200) NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [CountryCode] nvarchar(10) NULL,
    [Password] nvarchar(200) NULL,
    [ProfileImageID] nvarchar(50) NULL,
    [AddressLine1] nvarchar(200) NULL,
    [AddressLine2] nvarchar(200) NULL,
    [AddressLine3] nvarchar(200) NULL,
    [State] nvarchar(100) NULL,
    [City] nvarchar(100) NULL,
    [PinCode] nvarchar(20) NULL,
    [ProfileCompleted] nvarchar(10) NULL,
    [LastLogin] nvarchar(50) NULL,
    [UserRole] nvarchar(50) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [UserVehicles] (
    [RecId] nvarchar(50) NOT NULL,
    [UserId] nvarchar(50) NULL,
    [EVManufacturerID] nvarchar(50) NULL,
    [CarModelID] nvarchar(50) NULL,
    [CarModelVariant] nvarchar(100) NULL,
    [CarRegistrationNumber] nvarchar(50) NULL,
    [DefaultConfig] int NOT NULL,
    [BatteryTypeId] nvarchar(50) NULL,
    [BatteryCapacityId] nvarchar(50) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_UserVehicles] PRIMARY KEY ([RecId])
);
GO

CREATE TABLE [ChargingStations] (
    [RecId] nvarchar(50) NOT NULL,
    [ChargingPointId] nvarchar(100) NULL,
    [ChargingHubId] nvarchar(50) NULL,
    [ChargingGunCount] int NOT NULL,
    [ChargingStationImage] nvarchar(50) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_ChargingStations] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_ChargingStation_ChargePoint] FOREIGN KEY ([ChargingPointId]) REFERENCES [ChargePoint] ([ChargePointId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChargingStation_ChargingHub] FOREIGN KEY ([ChargingHubId]) REFERENCES [ChargingHubs] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FileMasters] (
    [RecId] nvarchar(50) NOT NULL,
    [UserId] nvarchar(50) NULL,
    [FileName] nvarchar(200) NULL,
    [FileType] nvarchar(50) NULL,
    [FileURL] nvarchar(500) NULL,
    [Remarks] nvarchar(500) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_FileMasters] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_FileMaster_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RefreshTokens] (
    [RecId] nvarchar(50) NOT NULL,
    [UserId] nvarchar(50) NOT NULL,
    [Token] nvarchar(500) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedByIp] nvarchar(50) NULL,
    [RevokedAt] datetime2 NULL,
    [RevokedByIp] nvarchar(50) NULL,
    [ReplacedByToken] nvarchar(500) NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_RefreshToken_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([RecId]) ON DELETE CASCADE
);
GO

CREATE TABLE [WalletTransactionLogs] (
    [RecId] nvarchar(50) NOT NULL,
    [UserId] nvarchar(50) NULL,
    [PreviousCreditBalance] nvarchar(50) NULL,
    [CurrentCreditBalance] nvarchar(50) NULL,
    [TransactionType] nvarchar(50) NULL,
    [PaymentRecId] nvarchar(50) NULL,
    [ChargingSessionId] nvarchar(50) NULL,
    [AdditionalInfo1] nvarchar(200) NULL,
    [AdditionalInfo2] nvarchar(200) NULL,
    [AdditionalInfo3] nvarchar(200) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_WalletTransactionLogs] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_WalletTransactionLog_ChargingSession] FOREIGN KEY ([ChargingSessionId]) REFERENCES [ChargingSessions] ([RecId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WalletTransactionLog_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChargingHubReviews] (
    [RecId] nvarchar(50) NOT NULL,
    [ChargingHubId] nvarchar(50) NULL,
    [ChargingStationId] nvarchar(50) NULL,
    [Rating] int NOT NULL,
    [Description] nvarchar(1000) NULL,
    [ReviewTime] datetime2 NOT NULL,
    [ReviewImage1] nvarchar(50) NULL,
    [ReviewImage2] nvarchar(50) NULL,
    [ReviewImage3] nvarchar(50) NULL,
    [ReviewImage4] nvarchar(50) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_ChargingHubReviews] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_ChargingHubReview_ChargingHub] FOREIGN KEY ([ChargingHubId]) REFERENCES [ChargingHubs] ([RecId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChargingHubReview_ChargingStation] FOREIGN KEY ([ChargingStationId]) REFERENCES [ChargingStations] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PaymentHistories] (
    [RecId] nvarchar(50) NOT NULL,
    [TransactionType] nvarchar(50) NULL,
    [UserId] nvarchar(50) NULL,
    [ChargingStationId] nvarchar(50) NULL,
    [SessionDuration] time NOT NULL,
    [PaymentMethod] nvarchar(50) NULL,
    [AdditionalInfo1] nvarchar(200) NULL,
    [AdditionalInfo2] nvarchar(200) NULL,
    [AdditionalInfo3] nvarchar(200) NULL,
    [OrderId] nvarchar(100) NULL,
    [PaymentId] nvarchar(100) NULL,
    [UserRemarks] nvarchar(500) NULL,
    [Active] int NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_PaymentHistories] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_PaymentHistory_ChargingStation] FOREIGN KEY ([ChargingStationId]) REFERENCES [ChargingStations] ([RecId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PaymentHistory_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_BatteryCapacityMasters_BatteryCapcacity] ON [BatteryCapacityMasters] ([BatteryCapcacity]);
GO

CREATE INDEX [IX_BatteryTypeMasters_BatteryType] ON [BatteryTypeMasters] ([BatteryType]);
GO

CREATE INDEX [IX_CarManufacturerMasters_ManufacturerName] ON [CarManufacturerMasters] ([ManufacturerName]);
GO

CREATE INDEX [IX_ChargerTypeMasters_ChargerType] ON [ChargerTypeMasters] ([ChargerType]);
GO

CREATE INDEX [IX_ChargingHubReviews_ChargingHubId] ON [ChargingHubReviews] ([ChargingHubId]);
GO

CREATE INDEX [IX_ChargingHubReviews_ChargingStationId] ON [ChargingHubReviews] ([ChargingStationId]);
GO

CREATE INDEX [IX_ChargingStations_ChargingHubId] ON [ChargingStations] ([ChargingHubId]);
GO

CREATE INDEX [IX_ChargingStations_ChargingPointId] ON [ChargingStations] ([ChargingPointId]);
GO

CREATE INDEX [IX_FileMasters_UserId] ON [FileMasters] ([UserId]);
GO

CREATE INDEX [IX_PaymentHistories_ChargingStationId] ON [PaymentHistories] ([ChargingStationId]);
GO

CREATE INDEX [IX_PaymentHistories_UserId] ON [PaymentHistories] ([UserId]);
GO

CREATE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
GO

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
GO

CREATE INDEX [IX_WalletTransactionLogs_ChargingSessionId] ON [WalletTransactionLogs] ([ChargingSessionId]);
GO

CREATE INDEX [IX_WalletTransactionLogs_UserId] ON [WalletTransactionLogs] ([UserId]);
GO

ALTER TABLE [ConnectorStatus] ADD CONSTRAINT [FK_ConnectorStatus_ChargePoint_ChargePointId] FOREIGN KEY ([ChargePointId]) REFERENCES [ChargePoint] ([ChargePointId]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260118042113_AddHardwareMasterTables', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChargingHubs] ADD [ChargingHubName] nvarchar(200) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260121112103_ChargingHubName', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [FileMasters] ADD [FileContent] varbinary(max) NULL;
GO

ALTER TABLE [FileMasters] ADD [FileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260121232426_FileMasterEntsAdded', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChargingSessions] ADD [UserId] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260122004840_ChrgingSessionUserID', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChargingSessions] ADD [TransactionId] int NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260126220825_TransactionIdToChSess', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [UserVehicles] ADD [ChargerTypeId] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260127015351_ChargerType', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserVehicles]') AND [c].[name] = N'ChargerTypeId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UserVehicles] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [UserVehicles] ALTER COLUMN [ChargerTypeId] nvarchar(50) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260127015605_ChargerType2', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ChargingGuns] (
    [RecId] nvarchar(50) NOT NULL,
    [ChargingStationId] nvarchar(50) NULL,
    [ConnectorId] nvarchar(50) NULL,
    [ChargingHubId] nvarchar(50) NULL,
    [ChargerTypeId] nvarchar(50) NULL,
    [ChargerTariff] nvarchar(50) NULL,
    [PowerOutput] nvarchar(50) NULL,
    [ChargerStatus] nvarchar(50) NULL,
    [ChargerMeterReading] nvarchar(50) NULL,
    [AdditionalInfo1] nvarchar(200) NULL,
    [AdditionalInfo2] nvarchar(200) NULL,
    [Active] int NOT NULL DEFAULT 1,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_ChargingGuns] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_ChargingGuns_ChargerTypeMaster] FOREIGN KEY ([ChargerTypeId]) REFERENCES [ChargerTypeMasters] ([RecId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChargingGuns_ChargingHub] FOREIGN KEY ([ChargingHubId]) REFERENCES [ChargingHubs] ([RecId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChargingGuns_ChargingStation] FOREIGN KEY ([ChargingStationId]) REFERENCES [ChargingStations] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_ChargingGuns_ChargerTypeId] ON [ChargingGuns] ([ChargerTypeId]);
GO

CREATE INDEX [IX_ChargingGuns_ChargingHubId] ON [ChargingGuns] ([ChargingHubId]);
GO

CREATE INDEX [IX_ChargingGuns_ChargingStationId] ON [ChargingGuns] ([ChargingStationId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260129113421_Guns', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChargingSessions] ADD [SoCEnd] float NULL;
GO

ALTER TABLE [ChargingSessions] ADD [SoCLastUpdate] datetime2 NULL;
GO

ALTER TABLE [ChargingSessions] ADD [SoCStart] float NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260204003814_SoC', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChargingHubReviews] ADD [UserId] nvarchar(50) NULL;
GO

CREATE INDEX [IX_ChargingHubReviews_UserId] ON [ChargingHubReviews] ([UserId]);
GO

ALTER TABLE [ChargingHubReviews] ADD CONSTRAINT [FK_ChargingHubReview_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([RecId]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260205032716_UserIdForReviews', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Users] ADD [CreditBalance] nvarchar(50) NULL;
GO

CREATE TABLE [PaymentValidations] (
    [RecId] nvarchar(50) NOT NULL,
    [UserId] nvarchar(50) NOT NULL,
    [OrderId] nvarchar(100) NOT NULL,
    [PaymentId] nvarchar(100) NULL,
    [PaymentSignature] nvarchar(500) NULL,
    [Amount] bigint NOT NULL,
    [Currency] nvarchar(10) NULL,
    [Status] nvarchar(50) NOT NULL,
    [PaymentMethod] nvarchar(50) NULL,
    [IpAddress] nvarchar(50) NULL,
    [UserAgent] nvarchar(500) NULL,
    [VerifiedAt] datetime2 NULL,
    [ProcessedAt] datetime2 NULL,
    [PaymentHistoryId] nvarchar(50) NULL,
    [WalletTransactionId] nvarchar(50) NULL,
    [VerificationMessage] nvarchar(500) NULL,
    [SecurityHash] nvarchar(500) NULL,
    [VerificationAttempts] int NOT NULL,
    [FailureReason] nvarchar(500) NULL,
    [Metadata] nvarchar(2000) NULL,
    [AdditionalInfo1] nvarchar(200) NULL,
    [AdditionalInfo2] nvarchar(200) NULL,
    [AdditionalInfo3] nvarchar(200) NULL,
    [Active] int NOT NULL DEFAULT 1,
    [CreatedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NOT NULL,
    CONSTRAINT [PK_PaymentValidations] PRIMARY KEY ([RecId]),
    CONSTRAINT [FK_PaymentValidation_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([RecId]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_PaymentValidations_OrderId] ON [PaymentValidations] ([OrderId]);
GO

CREATE INDEX [IX_PaymentValidations_PaymentId] ON [PaymentValidations] ([PaymentId]);
GO

CREATE INDEX [IX_PaymentValidations_Status] ON [PaymentValidations] ([Status]);
GO

CREATE INDEX [IX_PaymentValidations_UserId] ON [PaymentValidations] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260206183559_Payment', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChargingSessions] ADD [BatteryIncreaseLimit] float NULL;
GO

ALTER TABLE [ChargingSessions] ADD [CostLimit] float NULL;
GO

ALTER TABLE [ChargingSessions] ADD [EnergyLimit] float NULL;
GO

ALTER TABLE [ChargingSessions] ADD [TimeLimit] int NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260209114857_ChargingHubLimits', N'8.0.17');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [OtpValidations] (
    [RecId] nvarchar(50) NOT NULL,
    [AuthId] nvarchar(50) NOT NULL,
    [PhoneNumber] nvarchar(20) NOT NULL,
    [CountryCode] nvarchar(10) NULL,
    [OtpCode] nvarchar(10) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
    [VerifiedAt] datetime2 NULL,
    [AttemptCount] int NOT NULL DEFAULT 0,
    [RequestIp] nvarchar(50) NULL,
    [VerifyIp] nvarchar(50) NULL,
    [UserId] nvarchar(50) NULL,
    [Purpose] nvarchar(50) NULL,
    CONSTRAINT [PK_OtpValidations] PRIMARY KEY ([RecId])
);
GO

CREATE INDEX [IX_OtpValidations_AuthId] ON [OtpValidations] ([AuthId]);
GO

CREATE INDEX [IX_OtpValidations_PhoneNumber] ON [OtpValidations] ([PhoneNumber]);
GO

CREATE INDEX [IX_OtpValidations_PhoneNumber_CreatedAt] ON [OtpValidations] ([PhoneNumber], [CreatedAt]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260215010000_OTPLogin', N'8.0.17');
GO

COMMIT;
GO

