-- Milestone Phase 2: business audit trail. Safe to run more than once.
USE TAJIRISACCO;
GO
IF OBJECT_ID('AuditLog') IS NULL
CREATE TABLE AuditLog (
    Id BIGINT IDENTITY PRIMARY KEY,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId INT NULL,
    UserName NVARCHAR(200) NULL,
    Method NVARCHAR(10) NOT NULL,
    Path NVARCHAR(300) NOT NULL,
    Entity NVARCHAR(60) NULL,
    Action NVARCHAR(80) NULL,
    RecordId NVARCHAR(60) NULL,
    StatusCode INT NOT NULL,
    Ip NVARCHAR(64) NULL,
    INDEX IX_AuditLog_Created (CreatedUtc DESC),
    INDEX IX_AuditLog_User (UserId, CreatedUtc DESC)
);
GO
