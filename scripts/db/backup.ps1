[CmdletBinding()]
param([string]$OutputRoot = (Join-Path $PSScriptRoot '..\..\backups\postgres'))

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) {
    throw 'PGPASSWORD must be provided through the environment.'
}

$pgDump = (Get-Command pg_dump.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($pgDump)) { $pgDump = 'C:\Program Files\PostgreSQL\17\bin\pg_dump.exe' }
$pgRestore = (Get-Command pg_restore.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($pgRestore)) { $pgRestore = 'C:\Program Files\PostgreSQL\17\bin\pg_restore.exe' }
if (-not (Test-Path -LiteralPath $pgDump)) { throw "pg_dump was not found at '$pgDump'." }

$timestamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$directory = New-Item -ItemType Directory -Force -Path (Join-Path $OutputRoot $timestamp)
$backupFile = Join-Path $directory.FullName 'orizon_agents.backup'
& $pgDump --host=127.0.0.1 --port=55432 --username=orizon --dbname=orizon_agents --format=custom --file=$backupFile --no-owner --no-acl --verbose
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit code $LASTEXITCODE." }
$catalog = & $pgRestore --list $backupFile
if ($LASTEXITCODE -ne 0 -or @($catalog).Count -eq 0) { throw 'The generated backup could not be listed.' }
Write-Output "Backup created: $backupFile"
Write-Output "Size bytes: $((Get-Item -LiteralPath $backupFile).Length)"
Write-Output "Catalog entries: $(@($catalog).Count)"
