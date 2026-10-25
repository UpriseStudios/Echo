# Deploys Echo's backend to the UGS *development* environment only (version 2.0).
# Builds the Cloud Code module (linux-x64, zipped to EchoCloud.ccm) and deploys it with the config-as-code files.
# It never changes the CLI's environment setting, and refuses to run unless that setting is development.
#
#   powershell -ExecutionPolicy Bypass -File Server/deploy-dev.ps1            # module + config
#   powershell -ExecutionPolicy Bypass -File Server/deploy-dev.ps1 -DryRun    # show what would change
#   add -SkipAccessControl while the service account cannot manage project policies

param([switch]$DryRun, [switch]$SkipAccessControl)
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

$targets = @($ccm, (Join-Path $server 'Config/echo.rc'), (Join-Path $server 'Config/daily.lb'))
if (-not $SkipAccessControl) { $targets += (Join-Path $server 'Config/echo-project-policy.ac') }
$extra = @('-p', $project, '-e', $Environment)
if ($DryRun) { $extra += '--dry-run' }
ugs deploy @targets @extra
if ($LASTEXITCODE -ne 0) { throw 'Deploy reported errors.' }
