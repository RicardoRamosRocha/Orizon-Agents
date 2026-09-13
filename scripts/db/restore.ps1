[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [string]$TargetDatabase = 'orizon_agents_recovery',
    [switch]$Official,
    [switch]$ConfirmOfficial
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $BackupFile)) { throw "Backup file was not found: $BackupFile" }
if ($TargetDatabase -eq 'orizon_agents' -and (-not $Official -or -not $ConfirmOfficial)) {
    throw 'Official restore requires -Official and -ConfirmOfficial. Prefer a recovery database first.'
}
if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) { throw 'PGPASSWORD must be provided through the environment.' }

$pgRestore = (Get-Command pg_restore.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($pgRestore)) { $pgRestore = 'C:\Program Files\PostgreSQL\17\bin\pg_restore.exe' }
if ($TargetDatabase -eq 'orizon_agents') {
    if ((Read-Host 'Type RESTORE OFFICIAL to continue') -cne 'RESTORE OFFICIAL') { throw 'Official restore cancelled.' }
}

& $pgRestore --host=127.0.0.1 --port=55432 --username=orizon --dbname=$TargetDatabase --no-owner --no-acl --exit-on-error --verbose $BackupFile
if ($LASTEXITCODE -ne 0) { throw "pg_restore failed with exit code $LASTEXITCODE." }
Write-Output "Restore completed into $TargetDatabase."
