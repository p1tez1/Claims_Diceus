-- Apply after EF migrations using a deployment administrator, never the API's runtime identity.
-- sqlcmd -d ClaimsModule -v RuntimeUser="claims_api" -i infra/sql/runtime-permissions.sql
-- Create the database user/login (or Azure identity) separately; it must not be dbo/db_owner/sysadmin.
-- Install Hangfire's schema with a deployment identity first. Rerun this script after installation.
SET XACT_ABORT ON;
DECLARE @RuntimeUser sysname = N'$(RuntimeUser)';
IF DATABASE_PRINCIPAL_ID(@RuntimeUser) IS NULL
    THROW 50001, 'Create the runtime database user before applying permissions.', 1;
IF @RuntimeUser = N'dbo' OR IS_ROLEMEMBER(N'db_owner', @RuntimeUser) = 1
    THROW 50002, 'The runtime user must not own the database or belong to db_owner.', 1;

BEGIN TRANSACTION;
IF DATABASE_PRINCIPAL_ID(N'ClaimsModuleRuntime') IS NULL
    EXEC(N'CREATE ROLE [ClaimsModuleRuntime] AUTHORIZATION [dbo]');
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[dbo] TO [ClaimsModuleRuntime];
GRANT DELETE ON OBJECT::[dbo].[IdempotencyRecords] TO [ClaimsModuleRuntime];
DENY UPDATE, DELETE ON OBJECT::[dbo].[ClaimAuditLog] TO [ClaimsModuleRuntime];
IF SCHEMA_ID(N'HangFire') IS NOT NULL
    GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[HangFire] TO [ClaimsModuleRuntime];
IF IS_ROLEMEMBER(N'ClaimsModuleRuntime', @RuntimeUser) <> 1
BEGIN
    DECLARE @MembershipSql nvarchar(max) = N'ALTER ROLE [ClaimsModuleRuntime] ADD MEMBER ' + QUOTENAME(@RuntimeUser);
    EXEC sp_executesql @MembershipSql;
END;
COMMIT;
