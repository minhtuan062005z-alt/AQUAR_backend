BEGIN TRANSACTION;
GO

CREATE TABLE [CompanyLocationConfigurations] (
    [Id] int NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [RadiusMeters] float NOT NULL,
    [Version] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CompanyLocationConfigurations] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [PayrollRecords] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [MonthlySalary] decimal(18,4) NOT NULL,
    [StandardDays] decimal(18,4) NOT NULL,
    [HoursPerDay] decimal(18,4) NOT NULL,
    [OvertimeMultiplier] decimal(18,4) NOT NULL,
    [Formula] nvarchar(32) NOT NULL,
    [ValidHours] decimal(18,4) NOT NULL,
    [ValidDays] decimal(18,4) NOT NULL,
    [OpenShifts] int NOT NULL,
    [LateCount] int NULL,
    [UnpaidLeaveDays] decimal(18,4) NULL,
    [ApprovedOvertimeHours] decimal(18,4) NOT NULL,
    [Allowances] decimal(18,4) NOT NULL,
    [Deductions] decimal(18,4) NOT NULL,
    [InputNote] nvarchar(2000) NOT NULL,
    [AttendancePay] decimal(18,4) NOT NULL,
    [OvertimePay] decimal(18,4) NOT NULL,
    [NetPay] decimal(18,4) NOT NULL,
    [State] nvarchar(16) NOT NULL,
    [CalculatedAtUtc] datetime2 NOT NULL,
    [CalculatedBy] int NOT NULL,
    [ApprovedAtUtc] datetime2 NULL,
    [ApprovedBy] int NULL,
    [ClosedAtUtc] datetime2 NULL,
    [ClosedBy] int NULL,
    [Version] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PayrollRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollRecords_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SalaryConfigurations] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [MonthlySalary] decimal(18,4) NOT NULL,
    [StandardDays] decimal(18,4) NOT NULL,
    [HoursPerDay] decimal(18,4) NOT NULL,
    [OvertimeMultiplier] decimal(18,4) NOT NULL,
    [Formula] nvarchar(32) NOT NULL,
    [ManagerCanView] bit NOT NULL,
    [Version] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SalaryConfigurations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SalaryConfigurations_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_PayrollRecords_StaffId_Year_Month] ON [PayrollRecords] ([StaffId], [Year], [Month]);
GO

CREATE UNIQUE INDEX [IX_SalaryConfigurations_StaffId] ON [SalaryConfigurations] ([StaffId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105133314_AddMonthlyPayrollAndCompanyConfiguration', N'8.0.11');
GO

COMMIT;
GO

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

