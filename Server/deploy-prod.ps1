# Deploys Echo's backend to the UGS *production* environment (version 2.0 release).
# Same module and config-as-code files as deploy-dev.ps1, with guardrails:
#   - nothing is deployed without -Confirm production (or -DryRun, which only shows what would change);
#   - it never changes the CLI's saved environment: -e production applies to this command only;
#   - the daily leaderboard's first reset is set to the next midnight UTC (UGS needs it in the future);
#   - -SwitchesOff deploys the Remote Config switches as false, to launch the backend dark and turn the Daily Haunt
#     and server challenges on later in the dashboard.
# Production also needs DAILY_SEED_SALT_PROD and LEADERBOARDS_ADMIN_KEY_PROD in Secret Manager (see
# StoreListing/release-2.0-checklist.md); run `ReplayProbe smoke production` afterwards to check them.
#
#   powershell -ExecutionPolicy Bypass -File Server/deploy-prod.ps1 -DryRun
#   powershell -ExecutionPolicy Bypass -File Server/deploy-prod.ps1 -Confirm production [-SwitchesOff]

param([switch]$DryRun, [switch]$SwitchesOff, [string]$Confirm = '')
$ErrorActionPreference = 'Stop'
$Environment = 'production'

if (-not $DryRun -and $Confirm -ne $Environment) {
    throw "This deploys to PRODUCTION. Run with -DryRun to preview, or -Confirm production to deploy."
}

# Echo's own UGS project, from the Unity project (cloudProjectId), never the CLI's saved project: the CLI may be
# pointed at another game, and an Echo deploy must never land there.
$projectSettings = Join-Path (Split-Path $PSScriptRoot) 'ProjectSettings/ProjectSettings.asset'
$project = (Select-String -Path $projectSettings -Pattern '^\s*cloudProjectId:\s*(\S+)' | Select-Object -First 1).Matches[0].Groups[1].Value
if (-not $project) { throw "No cloudProjectId in $projectSettings." }

$server = $PSScriptRoot
$module = Join-Path $server 'EchoCloud/EchoCloud.csproj'

dotnet test (Join-Path $server 'Echo.Server.Tests/Echo.Server.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Server tests failed; nothing deployed.' }

dotnet build $module -c Release -t:ZipModule
if ($LASTEXITCODE -ne 0) { throw 'Module packaging failed; nothing deployed.' }
$ccm = Join-Path $server 'EchoCloud/bin/Release/net8.0/Module/EchoCloud.ccm'

# Production copies of the config, under the same file names (the names are the ids UGS uses).
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('echo-prod-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $lb = Get-Content (Join-Path $server 'Config/daily.lb') -Raw | ConvertFrom-Json
    $lb.ResetConfig.Start = [DateTime]::UtcNow.Date.AddDays(1).ToString("yyyy-MM-ddT00:00:00Z")
    $utf8 = New-Object System.Text.UTF8Encoding $false   # no byte-order mark
    [System.IO.File]::WriteAllText((Join-Path $staging 'daily.lb'), ($lb | ConvertTo-Json -Depth 5), $utf8)

    $rc = Get-Content (Join-Path $server 'Config/echo.rc') -Raw | ConvertFrom-Json
    if ($SwitchesOff) {
        $rc.entries.daily_enabled = $false
        $rc.entries.server_challenges_enabled = $false
    }
    [System.IO.File]::WriteAllText((Join-Path $staging 'echo.rc'), ($rc | ConvertTo-Json -Depth 5), $utf8)

    Write-Host "Deploying to Echo project $project, $Environment (first daily reset $($lb.ResetConfig.Start); switches daily=$($rc.entries.daily_enabled), challenges=$($rc.entries.server_challenges_enabled))"
    $targets = @($ccm, (Join-Path $staging 'echo.rc'), (Join-Path $staging 'daily.lb'), (Join-Path $server 'Config/echo-project-policy.ac'))
    $extra = @('-p', $project, '-e', $Environment)
    if ($DryRun) { $extra += '--dry-run' }
    ugs deploy @targets @extra
    if ($LASTEXITCODE -ne 0) { throw 'Deploy reported errors.' }
}
finally {
    Remove-Item -Recurse -Force $staging
}
