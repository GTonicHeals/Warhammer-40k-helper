# Publishes the app to the hosted WarhammerHelper service.
# Run from anywhere:  powershell -ExecutionPolicy Bypass -File .\deploy.ps1
# It asks for admin rights (needed to stop/start the service). The hosted campaign.db
# is backed up first and never overwritten; new migrations apply when the service starts.

$ErrorActionPreference = 'Stop'

$ServiceName = 'WarhammerHelper'
$PublishDir  = 'C:\Apps\Warhammer-Helper-Published'
$Project     = Join-Path $PSScriptRoot "T'au\Warhammer.csproj"
$Db          = Join-Path $PublishDir 'campaign.db'
$HealthUrl   = 'http://localhost:7200/'

# Relaunch elevated if needed.
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Start-Process powershell -Verb RunAs -ArgumentList "-NoExit -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    return
}

Write-Host "== Building release into a staging folder ==" -ForegroundColor Cyan
$staging = Join-Path $env:TEMP "warhammer-publish-$(Get-Date -Format yyyyMMddHHmmss)"
dotnet publish $Project -c Release -o $staging
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed; the service was not touched." }

# Never ship a database from the build.
Get-ChildItem $staging -Filter 'campaign.db*' -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "== Backing up the hosted campaign.db ==" -ForegroundColor Cyan
if (Test-Path $Db) {
    $backupDir = Join-Path $PublishDir 'backups'
    New-Item -ItemType Directory -Force $backupDir | Out-Null
    $backup = Join-Path $backupDir "campaign-$(Get-Date -Format yyyyMMdd-HHmmss).db"
    Write-Host "   -> $backup"
}

Write-Host "== Stopping $ServiceName ==" -ForegroundColor Cyan
Stop-Service $ServiceName
(Get-Service $ServiceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))

# Copy after the stop so SQLite has released the file (includes -wal/-shm if present).
if (Test-Path $Db) {
    Copy-Item $Db $backup
    foreach ($ext in '-wal', '-shm') { if (Test-Path "$Db$ext") { Copy-Item "$Db$ext" "$backup$ext" } }
}

Write-Host "== Copying new build into $PublishDir ==" -ForegroundColor Cyan
robocopy $staging $PublishDir /E /NFL /NDL /NJH /NP /XF campaign.db campaign.db-wal campaign.db-shm /XD backups | Out-Host
if ($LASTEXITCODE -ge 8) { Write-Warning "robocopy reported errors (code $LASTEXITCODE)." }
Remove-Item $staging -Recurse -Force

Write-Host "== Starting $ServiceName ==" -ForegroundColor Cyan
Start-Service $ServiceName
(Get-Service $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

# First request runs the migrations; give it a few tries.
$ok = $false
for ($i = 0; $i -lt 10 -and -not $ok; $i++) {
    Start-Sleep -Seconds 2
    try { $ok = (Invoke-WebRequest $HealthUrl -UseBasicParsing -TimeoutSec 10).StatusCode -eq 200 } catch {}
}
if ($ok) { Write-Host "Deployed. $HealthUrl is answering." -ForegroundColor Green }
else     { Write-Warning "Service started but $HealthUrl did not answer. Backup: $backup" }
