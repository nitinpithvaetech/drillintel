namespace DrillIntel.Licensing.Issuer.Register;

internal static class RegisterSchema
{
    // Keep provider-specific DDL isolated here. The schema uses portable relational concepts
    // and explicit UTC text / integer minor currency units to ease a later PostgreSQL migration.
    internal const string Sql = """
        CREATE TABLE IF NOT EXISTS Customers (
            CustomerId INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            Contact TEXT NULL,
            Notes TEXT NULL,
            CreatedAtUtc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Rigs (
            RigId INTEGER PRIMARY KEY,
            CustomerId INTEGER NOT NULL REFERENCES Customers(CustomerId),
            RigName TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UNIQUE(CustomerId, RigName)
        );

        CREATE TABLE IF NOT EXISTS Licenses (
            LicenseId TEXT PRIMARY KEY,
            RigId INTEGER NOT NULL REFERENCES Rigs(RigId),
            Plan TEXT NOT NULL,
            StartUtc TEXT NOT NULL,
            ExpiryUtc TEXT NOT NULL,
            MaxDevices INTEGER NOT NULL CHECK (MaxDevices > 0),
            Status TEXT NOT NULL CHECK (Status IN ('active', 'suspended', 'revoked')),
            CreatedAtUtc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS EntitlementRevisions (
            LicenseId TEXT NOT NULL REFERENCES Licenses(LicenseId),
            Revision INTEGER NOT NULL CHECK (Revision > 0),
            IssuedAtUtc TEXT NOT NULL,
            Kid TEXT NOT NULL,
            SignedFileRef TEXT NOT NULL,
            FileSha256 TEXT NOT NULL CHECK (length(FileSha256) = 64),
            PRIMARY KEY (LicenseId, Revision)
        );

        CREATE TABLE IF NOT EXISTS Devices (
            DeviceId INTEGER PRIMARY KEY,
            LicenseId TEXT NOT NULL REFERENCES Licenses(LicenseId),
            KeyHash TEXT NOT NULL,
            KeyProtection TEXT NOT NULL CHECK (KeyProtection IN ('tpm', 'software')),
            Status TEXT NOT NULL CHECK (Status IN ('active', 'revoked')),
            ActivatedAtUtc TEXT NOT NULL,
            ActivationExpiryUtc TEXT NOT NULL,
            ReplacementForDeviceId INTEGER NULL REFERENCES Devices(DeviceId),
            UNIQUE (LicenseId, KeyHash)
        );

        CREATE TABLE IF NOT EXISTS Requests (
            RequestId TEXT PRIMARY KEY,
            LicenseId TEXT NOT NULL REFERENCES Licenses(LicenseId),
            KeyHash TEXT NOT NULL,
            ReceivedAtUtc TEXT NOT NULL,
            Outcome TEXT NOT NULL CHECK (Outcome IN ('issued', 'rejected', 'duplicate')),
            OutcomeDetails TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS RequestInbox (
            RequestId TEXT PRIMARY KEY,
            LicenseId TEXT NOT NULL REFERENCES Licenses(LicenseId),
            KeyHash TEXT NOT NULL CHECK (length(KeyHash) = 64),
            KeyProtection TEXT NOT NULL CHECK (KeyProtection IN ('tpm', 'software')),
            TimestampUtc TEXT NOT NULL,
            ReceivedAtUtc TEXT NOT NULL,
            SourceFile TEXT NOT NULL,
            Status TEXT NOT NULL CHECK (Status IN ('pending', 'duplicate', 'rejected'))
        );

        CREATE TABLE IF NOT EXISTS ActivationFiles (
            ActivationFileId TEXT PRIMARY KEY,
            LicenseId TEXT NOT NULL REFERENCES Licenses(LicenseId),
            DeviceId INTEGER NOT NULL REFERENCES Devices(DeviceId),
            RequestId TEXT NULL REFERENCES Requests(RequestId),
            Revision INTEGER NOT NULL,
            Kid TEXT NOT NULL,
            IssuedAtUtc TEXT NOT NULL,
            ActivationExpiryUtc TEXT NOT NULL,
            ArchiveRef TEXT NOT NULL,
            FileSha256 TEXT NOT NULL CHECK (length(FileSha256) = 64),
            UNIQUE (LicenseId, DeviceId, Revision, IssuedAtUtc)
        );

        CREATE TABLE IF NOT EXISTS Payments (
            OrderRef TEXT PRIMARY KEY,
            LicenseId TEXT NOT NULL REFERENCES Licenses(LicenseId),
            AmountMinor INTEGER NOT NULL CHECK (AmountMinor >= 0),
            Currency TEXT NOT NULL CHECK (length(Currency) = 3),
            Processor TEXT NOT NULL,
            Status TEXT NOT NULL CHECK (Status IN ('paid', 'refunded', 'chargeback')),
            PaidAtUtc TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS AuditLog (
            AuditId INTEGER PRIMARY KEY,
            TimestampUtc TEXT NOT NULL,
            Operator TEXT NOT NULL,
            Action TEXT NOT NULL,
            LicenseId TEXT NULL REFERENCES Licenses(LicenseId),
            Details TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Rigs_CustomerId ON Rigs(CustomerId);
        CREATE INDEX IF NOT EXISTS IX_Licenses_RigId ON Licenses(RigId);
        CREATE INDEX IF NOT EXISTS IX_Devices_License_Status ON Devices(LicenseId, Status);
        CREATE INDEX IF NOT EXISTS IX_Requests_License_KeyHash ON Requests(LicenseId, KeyHash);
        CREATE INDEX IF NOT EXISTS IX_RequestInbox_License_Received ON RequestInbox(LicenseId, ReceivedAtUtc);
        CREATE INDEX IF NOT EXISTS IX_ActivationFiles_License_Device ON ActivationFiles(LicenseId, DeviceId);
        CREATE INDEX IF NOT EXISTS IX_AuditLog_License_Timestamp ON AuditLog(LicenseId, TimestampUtc);
        """;
}
