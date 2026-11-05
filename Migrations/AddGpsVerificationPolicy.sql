BEGIN TRANSACTION;
GO

ALTER TABLE [CompanyLocationConfigurations] ADD [MaxAccuracyMeters] float NOT NULL DEFAULT 50.0E0;
GO

ALTER TABLE [CompanyLocationConfigurations] ADD [MaxAgeSeconds] int NOT NULL DEFAULT 30;
GO

ALTER TABLE [CompanyLocationConfigurations] ADD [RequireGpsVerification] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [CompanyLocationConfigurations] ADD [UpdatedAtUtc] datetime2 NULL;
GO

ALTER TABLE [CompanyLocationConfigurations] ADD [UpdatedBy] int NULL;
GO

ALTER TABLE [AttendanceLocations] ADD [AccuracyMeters] float NULL;
GO

ALTER TABLE [AttendanceLocations] ADD [CapturedAtUtc] datetime2 NULL;
GO

ALTER TABLE [AttendanceLocations] ADD [Method] nvarchar(16) NULL;
GO

ALTER TABLE [AttendanceLocations] ADD [VerificationResult] nvarchar(32) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105143209_AddGpsVerificationPolicy', N'8.0.11');
GO

COMMIT;
GO

