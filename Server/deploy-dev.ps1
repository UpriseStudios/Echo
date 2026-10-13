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

$current = (ugs config get environment-name).Trim()
if ($current -ne $Environment) {
    throw "The UGS CLI environment is '$current'. This script only deploys to '$Environment'; it will not change the setting."
}

$server = $PSScriptRoot
$module = Join-Path $server 'EchoCloud/EchoCloud.csproj'

dotnet test (Join-Path $server 'Echo.Server.Tests/Echo.Server.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Server tests failed; nothing deployed.' }

dotnet build $module -c Release -t:ZipModule
if ($LASTEXITCODE -ne 0) { throw 'Module packaging failed; nothing deployed.' }
$ccm = Join-Path $server 'EchoCloud/bin/Release/net8.0/Module/EchoCloud.ccm'

$targets = @($ccm, (Join-Path $server 'Config/echo.rc'), (Join-Path $server 'Config/daily.lb'))
if (-not $SkipAccessControl) { $targets += (Join-Path $server 'Config/echo-project-policy.ac') }
$extra = @('-e', $Environment)
if ($DryRun) { $extra += '--dry-run' }
ugs deploy @targets @extra
if ($LASTEXITCODE -ne 0) { throw 'Deploy reported errors.' }
