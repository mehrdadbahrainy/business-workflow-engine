# Backup and Restore

This runbook covers a manual logical backup of the PostgreSQL database used by the production Compose stack. PostgreSQL custom-format archives are compressed by default and are restored with `pg_restore`; they can be copied without PowerShell text redirection, which can corrupt binary files. See the PostgreSQL 17 [`pg_dump`](https://www.postgresql.org/docs/17/app-pgdump.html) and [`pg_restore`](https://www.postgresql.org/docs/17/app-pgrestore.html) references.

The Compose named volume protects data when containers are recreated, but it is not a backup. These steps do not schedule backups, copy them off-host, or back up identity-provider configuration. Store archives in an access-controlled location separate from the host and set a retention policy that fits the deployment.

Run these PowerShell commands from the repository root. The production stack must already be running, with its `.env` file in place.

## Create a backup

The dump runs in the PostgreSQL container and is copied to the host as a binary archive. The container's temporary copy is removed after the transfer.

```powershell
$compose = @("compose", "--env-file", ".env", "-f", "compose.production.yaml")
$archive = ".\workflow-engine-backup-$(Get-Date -Format yyyyMMdd-HHmmss).dump"

& docker @compose exec -T postgres sh -c 'rm -f /tmp/workflow-engine-backup.dump && umask 077 && pg_dump --format=custom --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --file=/tmp/workflow-engine-backup.dump'
if ($LASTEXITCODE -ne 0) { throw "PostgreSQL backup failed." }

& docker @compose cp postgres:/tmp/workflow-engine-backup.dump $archive
if ($LASTEXITCODE -ne 0) { throw "Copying the backup archive to the host failed." }

& docker @compose exec -T postgres rm -f /tmp/workflow-engine-backup.dump
if ($LASTEXITCODE -ne 0) { throw "The host backup exists, but temporary-file cleanup failed." }

Write-Output "Backup created at $((Resolve-Path -LiteralPath $archive).Path)"
```

Move the resulting archive to the deployment's protected off-host backup location. Do not commit backup archives; this repository ignores `workflow-engine-backup-*.dump` files.

## Restore a backup

> **Destructive:** restoring replaces the current application database. Schedule a maintenance window, verify the archive and target environment, and preserve a separate copy of the current database before proceeding. Keep the API and web containers stopped until the restore and checks are complete.

Set `$archive` to the backup file to restore. This procedure stops API and web traffic, drops and recreates the configured application database, and restores the archive into that database. The PostgreSQL container and its named volume remain in place.

```powershell
$compose = @("compose", "--env-file", ".env", "-f", "compose.production.yaml")
$archivePath = Read-Host "Path to the backup archive"
$archive = (Resolve-Path -LiteralPath $archivePath).Path

& docker @compose stop api web
if ($LASTEXITCODE -ne 0) { throw "Could not stop the API and web containers." }

& docker @compose cp $archive postgres:/tmp/workflow-engine-restore.dump
if ($LASTEXITCODE -ne 0) { throw "Copying the archive into the PostgreSQL container failed." }

& docker @compose exec -T postgres sh -c 'dropdb --if-exists --username="$POSTGRES_USER" "$POSTGRES_DB" && createdb --username="$POSTGRES_USER" --owner="$POSTGRES_USER" "$POSTGRES_DB"'
if ($LASTEXITCODE -ne 0) { throw "Could not recreate the application database. Leave the API and web containers stopped." }

& docker @compose exec -T postgres sh -c 'pg_restore --exit-on-error --no-owner --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" /tmp/workflow-engine-restore.dump'
if ($LASTEXITCODE -ne 0) { throw "Restore failed. Leave the API and web containers stopped and investigate before retrying." }

& docker @compose exec -T postgres rm -f /tmp/workflow-engine-restore.dump
if ($LASTEXITCODE -ne 0) { throw "The restore succeeded, but temporary-file cleanup failed." }

& docker @compose up -d api web
if ($LASTEXITCODE -ne 0) { throw "The database was restored, but the API and web containers did not start." }
```

After startup, confirm the API health endpoint responds and inspect a known request and its history through the API/UI. Periodically rehearse restoration into an isolated deployment; a successful dump command alone does not prove that recovery will meet operational needs.
