/* =====================================================================
   006 - Payments, chamas / merry-go-rounds, member KYC, website, profiles.
   The app creates all of these automatically at startup (idempotent).
   Run this by hand only if you prefer to manage the schema yourself:
     sqlcmd -S . -d TAJIRISACCO -i Database\006_payments_chamas.sql
   ===================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.SystemConfig') IS NULL
  CREATE TABLE dbo.SystemConfig ([Key] NVARCHAR(100) NOT NULL PRIMARY KEY, [Value] NVARCHAR(MAX) NOT NULL,
    UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedBy NVARCHAR(200) NULL);

IF OBJECT_ID('dbo.SiteEnquiries') IS NULL
  CREATE TABLE dbo.SiteEnquiries (Id BIGINT IDENTITY(1,1) PRIMARY KEY, Kind NVARCHAR(30) NOT NULL, Name NVARCHAR(120) NOT NULL,
    Phone NVARCHAR(40) NULL, Email NVARCHAR(200) NULL, Message NVARCHAR(2000) NULL, Status NVARCHAR(20) NOT NULL DEFAULT 'New',
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), Ip NVARCHAR(64) NULL, HandledBy NVARCHAR(200) NULL, HandledUtc DATETIME2 NULL);

IF OBJECT_ID('dbo.UserProfiles') IS NULL
  CREATE TABLE dbo.UserProfiles (UserId INT NOT NULL PRIMARY KEY, Photo NVARCHAR(MAX) NULL, StaffNo NVARCHAR(30) NULL, JobTitle NVARCHAR(80) NULL,
    Department NVARCHAR(80) NULL, Branch NVARCHAR(80) NULL, NationalId NVARCHAR(20) NULL, KraPin NVARCHAR(20) NULL, DateOfBirth DATE NULL,
    Gender NVARCHAR(20) NULL, County NVARCHAR(40) NULL, PostalAddress NVARCHAR(120) NULL, AltPhone NVARCHAR(30) NULL, Language NVARCHAR(20) NULL,
    NextOfKinName NVARCHAR(120) NULL, NextOfKinRelation NVARCHAR(40) NULL, NextOfKinPhone NVARCHAR(30) NULL, Bio NVARCHAR(600) NULL,
    UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID('dbo.MemberExtras') IS NULL
  CREATE TABLE dbo.MemberExtras (MemberNo NVARCHAR(40) NOT NULL PRIMARY KEY, Photo NVARCHAR(MAX) NULL, KraPin NVARCHAR(20) NULL, County NVARCHAR(40) NULL,
    PostalAddress NVARCHAR(120) NULL, PayrollNo NVARCHAR(40) NULL, CheckOff BIT NOT NULL DEFAULT 0, NextOfKinName NVARCHAR(120) NULL,
    NextOfKinRelation NVARCHAR(40) NULL, NextOfKinPhone NVARCHAR(30) NULL, NextOfKinIdNo NVARCHAR(20) NULL, PreferredChannel NVARCHAR(20) NULL,
    UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID('dbo.Payments') IS NULL
  CREATE TABLE dbo.Payments (Id BIGINT IDENTITY(1,1) PRIMARY KEY, ReceiptNo NVARCHAR(30) NOT NULL, MemberId INT NULL, MemberName NVARCHAR(160) NULL,
    Channel NVARCHAR(20) NOT NULL, Reference NVARCHAR(60) NULL, Phone NVARCHAR(30) NULL, Amount DECIMAL(18,2) NOT NULL, Purpose NVARCHAR(30) NOT NULL,
    TargetId INT NULL, PostedTo NVARCHAR(300) NULL, AllocTable NVARCHAR(40) NULL, AllocIds NVARCHAR(400) NULL, PaidOn DATETIME2 NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Posted', Notes NVARCHAR(500) NULL, RecordedBy NVARCHAR(200) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ReversedBy NVARCHAR(200) NULL, ReversedUtc DATETIME2 NULL, ReverseReason NVARCHAR(300) NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Reference') CREATE INDEX IX_Payments_Reference ON dbo.Payments (Reference);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Member') CREATE INDEX IX_Payments_Member ON dbo.Payments (MemberId, PaidOn);

IF OBJECT_ID('dbo.Chamas') IS NULL
  CREATE TABLE dbo.Chamas (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(120) NOT NULL, RegNo NVARCHAR(60) NULL, Type NVARCHAR(30) NOT NULL,
    ContributionAmount DECIMAL(18,2) NOT NULL, Frequency NVARCHAR(20) NOT NULL, MeetingDay NVARCHAR(20) NULL, MeetingPlace NVARCHAR(120) NULL,
    StartDate DATE NOT NULL, CurrentCycle INT NOT NULL DEFAULT 1, Status NVARCHAR(20) NOT NULL DEFAULT 'Active', Description NVARCHAR(600) NULL,
    Color NVARCHAR(9) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy NVARCHAR(200) NULL);
IF OBJECT_ID('dbo.ChamaMembers') IS NULL
  CREATE TABLE dbo.ChamaMembers (Id INT IDENTITY(1,1) PRIMARY KEY, ChamaId INT NOT NULL, MemberId INT NOT NULL, Role NVARCHAR(30) NOT NULL DEFAULT 'Member',
    Position INT NOT NULL DEFAULT 0, JoinedOn DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE), Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    CONSTRAINT UQ_ChamaMember UNIQUE (ChamaId, MemberId));
IF OBJECT_ID('dbo.ChamaContributions') IS NULL
  CREATE TABLE dbo.ChamaContributions (Id BIGINT IDENTITY(1,1) PRIMARY KEY, ChamaId INT NOT NULL, MemberId INT NOT NULL, CycleNo INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL, PaidOn DATETIME2 NOT NULL, Reference NVARCHAR(60) NULL, RecordedBy NVARCHAR(200) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.ChamaPayouts') IS NULL
  CREATE TABLE dbo.ChamaPayouts (Id INT IDENTITY(1,1) PRIMARY KEY, ChamaId INT NOT NULL, CycleNo INT NOT NULL, MemberId INT NOT NULL, Amount DECIMAL(18,2) NOT NULL,
    DueDate DATE NOT NULL, PaidOn DATETIME2 NULL, Reference NVARCHAR(60) NULL, Status NVARCHAR(20) NOT NULL DEFAULT 'Scheduled', PaidBy NVARCHAR(200) NULL,
    CONSTRAINT UQ_ChamaCycle UNIQUE (ChamaId, CycleNo));

PRINT '006 applied. Organisation settings and website text are seeded by the app on its next start.';
