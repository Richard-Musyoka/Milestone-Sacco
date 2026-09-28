/* =====================================================================
   Phase 1 – security schema. Idempotent: safe to run more than once.
   Run against your Sacco database BEFORE starting the new build.
   ===================================================================== */

-- 1. Users: new columns (existing rows and the plain-text Password column are kept;
--    passwords are re-hashed automatically on each user's next successful login).
IF COL_LENGTH('Users', 'PasswordHash')        IS NULL ALTER TABLE Users ADD PasswordHash NVARCHAR(400) NULL;
IF COL_LENGTH('Users', 'Role')                IS NULL ALTER TABLE Users ADD Role NVARCHAR(40) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'Admin';
IF COL_LENGTH('Users', 'IsActive')            IS NULL ALTER TABLE Users ADD IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1;
IF COL_LENGTH('Users', 'FailedLoginCount')    IS NULL ALTER TABLE Users ADD FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_Failed DEFAULT 0;
IF COL_LENGTH('Users', 'LockoutEndUtc')       IS NULL ALTER TABLE Users ADD LockoutEndUtc DATETIME2 NULL;
IF COL_LENGTH('Users', 'TotpEnabled')         IS NULL ALTER TABLE Users ADD TotpEnabled BIT NOT NULL CONSTRAINT DF_Users_Totp DEFAULT 0;
IF COL_LENGTH('Users', 'TotpSecretProtected') IS NULL ALTER TABLE Users ADD TotpSecretProtected NVARCHAR(1000) NULL;
IF COL_LENGTH('Users', 'TotpLastStep')        IS NULL ALTER TABLE Users ADD TotpLastStep BIGINT NOT NULL CONSTRAINT DF_Users_TotpStep DEFAULT 0;
IF COL_LENGTH('Users', 'OtpFallbackEnabled')  IS NULL ALTER TABLE Users ADD OtpFallbackEnabled BIT NOT NULL CONSTRAINT DF_Users_OtpFb DEFAULT 0;
IF COL_LENGTH('Users', 'PasswordChangedUtc')  IS NULL ALTER TABLE Users ADD PasswordChangedUtc DATETIME2 NULL;
IF COL_LENGTH('Users', 'LastLoginUtc')        IS NULL ALTER TABLE Users ADD LastLoginUtc DATETIME2 NULL;
GO

-- 2. Backup codes (stored as SHA-256 hashes, single use)
IF OBJECT_ID('UserBackupCodes') IS NULL
CREATE TABLE UserBackupCodes (
    Id INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL,
    CodeHash CHAR(64) NOT NULL,
    UsedUtc DATETIME2 NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    INDEX IX_UserBackupCodes_User (UserId)
);

-- 3. Passkeys (WebAuthn)
IF OBJECT_ID('UserPasskeys') IS NULL
CREATE TABLE UserPasskeys (
    Id INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL,
    CredentialId VARBINARY(512) NOT NULL,
    PublicKeySpki VARBINARY(1024) NOT NULL,
    Algorithm INT NOT NULL,
    SignCount BIGINT NOT NULL DEFAULT 0,
    Name NVARCHAR(100) NOT NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastUsedUtc DATETIME2 NULL,
    INDEX IX_UserPasskeys_User (UserId)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_UserPasskeys_Cred')
    CREATE UNIQUE INDEX UX_UserPasskeys_Cred ON UserPasskeys (CredentialId);

-- 4. Known / trusted devices
IF OBJECT_ID('UserDevices') IS NULL
CREATE TABLE UserDevices (
    Id INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    Label NVARCHAR(120) NOT NULL,
    UserAgent NVARCHAR(400) NULL,
    LastIp NVARCHAR(64) NULL,
    Trusted BIT NOT NULL DEFAULT 0,
    TrustedUntilUtc DATETIME2 NULL,
    FirstSeenUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastSeenUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    INDEX IX_UserDevices_User (UserId),
    INDEX IX_UserDevices_Token (TokenHash)
);

-- 5. Server-side sessions (revocable)
IF OBJECT_ID('UserSessions') IS NULL
CREATE TABLE UserSessions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    DeviceId INT NULL,
    DeviceLabel NVARCHAR(120) NOT NULL,
    Ip NVARCHAR(64) NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastSeenUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresUtc DATETIME2 NOT NULL,
    RevokedUtc DATETIME2 NULL,
    INDEX IX_UserSessions_User (UserId)
);

-- 6. Login / security history
IF OBJECT_ID('LoginHistory') IS NULL
CREATE TABLE LoginHistory (
    Id BIGINT IDENTITY PRIMARY KEY,
    UserId INT NULL,
    Email NVARCHAR(256) NULL,
    EventType NVARCHAR(40) NOT NULL,
    Success BIT NOT NULL,
    Ip NVARCHAR(64) NULL,
    Device NVARCHAR(120) NULL,
    Detail NVARCHAR(300) NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    INDEX IX_LoginHistory_User (UserId, CreatedUtc DESC)
);

-- 7. One-time codes for email / SMS fallback
IF OBJECT_ID('OtpChallenges') IS NULL
CREATE TABLE OtpChallenges (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    Channel NVARCHAR(10) NOT NULL,
    CodeHash CHAR(64) NOT NULL,
    Attempts INT NOT NULL DEFAULT 0,
    ExpiresUtc DATETIME2 NOT NULL,
    ConsumedUtc DATETIME2 NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- 8. Password history (for the "password history count" policy)
IF OBJECT_ID('UserPasswordHistory') IS NULL
CREATE TABLE UserPasswordHistory (
    Id INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL,
    PasswordHash NVARCHAR(400) NOT NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    INDEX IX_UserPasswordHistory_User (UserId)
);
GO
