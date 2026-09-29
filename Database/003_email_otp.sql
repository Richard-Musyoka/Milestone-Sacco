/* Phase 3b – email one-time codes: delivery tracking + admin backup. Idempotent.
   (The app also applies these columns automatically on start-up; this script is for running it yourself.)
   Codes are stored encrypted (ASP.NET Data Protection) ONLY while valid, then wiped when used or expired.
   Administrators read them in the app: Users & roles -> Sign-in codes. Do not read them from SQL – they are not plain text. */
IF COL_LENGTH('OtpChallenges','CodeProtected')  IS NULL ALTER TABLE OtpChallenges ADD CodeProtected NVARCHAR(600) NULL;
IF COL_LENGTH('OtpChallenges','Destination')    IS NULL ALTER TABLE OtpChallenges ADD Destination NVARCHAR(200) NULL;
IF COL_LENGTH('OtpChallenges','DeliveryStatus') IS NULL ALTER TABLE OtpChallenges ADD DeliveryStatus NVARCHAR(20) NULL;
IF COL_LENGTH('OtpChallenges','DeliveryError')  IS NULL ALTER TABLE OtpChallenges ADD DeliveryError NVARCHAR(400) NULL;
GO
/* Handy for troubleshooting delivery (no codes shown): */
-- SELECT TOP 50 o.CreatedUtc, u.Email, o.Channel, o.DeliveryStatus, o.DeliveryError, o.ExpiresUtc, o.ConsumedUtc
-- FROM OtpChallenges o JOIN Users u ON u.Id = o.UserId ORDER BY o.CreatedUtc DESC;

/* ---- Emergency read-back (when email fails) ----
   The app writes a readable copy of a code to CodePlain ONLY when email delivery failed AND
   Security:OtpPlainBackupOnFailure is true (default: on in Development, off in Production). Wiped when used/expired.
   Read it here:   SELECT * FROM vw_OtpBackup ORDER BY CreatedUtc DESC;                                            */
IF COL_LENGTH('OtpChallenges','CodePlain') IS NULL ALTER TABLE OtpChallenges ADD CodePlain NVARCHAR(12) NULL;
GO
CREATE OR ALTER VIEW vw_OtpBackup AS
SELECT o.CreatedUtc, u.Email, o.Channel, o.CodePlain AS Code, o.DeliveryStatus, o.DeliveryError, o.ExpiresUtc
FROM OtpChallenges o JOIN Users u ON u.Id = o.UserId
WHERE o.ConsumedUtc IS NULL AND o.ExpiresUtc > SYSUTCDATETIME() AND o.CodePlain IS NOT NULL;
GO
