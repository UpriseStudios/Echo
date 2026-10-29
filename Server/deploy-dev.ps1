# Deploys Echo's backend to the UGS *development* environment only (version 2.0).
# Builds the Cloud Code module (linux-x64, zipped to EchoCloud.ccm) and deploys it with the config-as-code files.
# It never changes the CLI's environment setting, and refuses to run unless that setting is development.
#
#   powershell -ExecutionPolicy Bypass -File Server/deploy-dev.ps1            # module + config
#   powershell -ExecutionPolicy Bypass -File Server/deploy-dev.ps1 -DryRun    # show what would change
#   add -SkipAccessControl while the service account cannot manage project policies
#   add -SkipSchedules while it lacks the Scheduler and Triggers roles (the first lobby of the day builds the set anyway)

param([switch]$DryRun, [switch]$SkipAccessControl, [switch]$SkipSchedules)
$ErrorActionPreference = 'Stop'
$Environment = 'development'

# Echo's own UGS project, from the Unity project (cloudProjectId), never the CLI's saved project: the CLI may be
# pointed at another game, and an Echo deploy must never land there.
$projectSettings = Join-Path (Split-Path $PSScriptRoot) 'ProjectSettings/ProjectSettings.asset'
$project = (Select-String -Path $projectSettings -Pattern '^\s*cloudProjectId:\s*(\S+)' | Select-Object -First 1).Matches[0].Groups[1].Value
if (-not $project) { throw "No cloudProjectId in $projectSettings." }
Write-Host "Echo project $project, environment $Environment"

$server = $PSScriptRoot
$module = Join-Path $server 'EchoCloud/EchoCloud.csproj'

dotnet test (Join-Path $server 'Echo.Server.Tests/Echo.Server.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Server tests failed; nothing deployed.' }

dotnet build $module -c Release -t:ZipModule
if ($LASTEXITCODE -ne 0) { throw 'Module packaging failed; nothing deployed.' }
$ccm = Join-Path $server 'EchoCloud/bin/Release/net8.0/Module/EchoCloud.ccm'

# UGS refuses a leaderboard whose first reset has passed, so a copy starts at the next midnight UTC (as deploy-prod.ps1).
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('echo-dev-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $targets = @($ccm, (Join-Path $server 'Config/echo.rc'))
    foreach ($board in Get-ChildItem (Join-Path $server 'Config') -Filter '*.lb') {
        $lb = Get-Content $board.FullName -Raw | ConvertFrom-Json
        # The first reset after now that the board's schedule allows: the next midnight, or the next Monday's for a
        # weekly board ("0 0 * * 1").
        $first = [DateTime]::UtcNow.Date.AddDays(1)
        if ($lb.ResetConfig.Schedule -eq '0 0 * * 1') { while ($first.DayOfWeek -ne [DayOfWeek]::Monday) { $first = $first.AddDays(1) } }
        $lb.ResetConfig.Start = $first.ToString("yyyy-MM-ddT00:00:00Z")
        [System.IO.File]::WriteAllText((Join-Path $staging $board.Name), ($lb | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding $false))
        $targets += (Join-Path $staging $board.Name)
    }
    if (-not $SkipAccessControl) { $targets += (Join-Path $server 'Config/echo-project-policy.ac') }
    # Scheduler and Triggers (modes doc: the Community Graveyard's ghosts are chosen at 00:00 UTC).
    if (-not $SkipSchedules) { $targets += (Get-ChildItem (Join-Path $server 'Config') -Include '*.sched', '*.tr' -Recurse | ForEach-Object { $_.FullName }) }
    $extra = @('-p', $project, '-e', $Environment)
    if ($DryRun) { $extra += '--dry-run' }
    ugs deploy @targets @extra
    if ($LASTEXITCODE -ne 0) { throw 'Deploy reported errors.' }
}
finally {
    Remove-Item -Recurse -Force $staging
}
