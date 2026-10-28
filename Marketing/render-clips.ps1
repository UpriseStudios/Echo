# Renders every clip in Marketing/clip-list.json into Marketing/Clips (ignored by git), through the game's own Vertical
# export (1152 x 2048, 60 fps). Close the Unity editor first. Render one clip with -Id <id>.
#
#   powershell -ExecutionPolicy Bypass -File Marketing/render-clips.ps1 [-Id full-graveyard]

param([string]$Id = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$results = Join-Path $root 'TestResults/marketing.xml'
$log = Join-Path $root 'TestResults/marketing.log'
$env:ECHO_CLIP = $Id

& $unity -batchmode -projectPath $root -runTests -testPlatform PlayMode `
    -testFilter 'Echo.Tests.PlayMode.MarketingClips.RenderClips' -testResults $results -logFile $log | Out-Null
Select-String -Path $log -Pattern '\[MarketingClips\]' | ForEach-Object { $_.Line }
if ((Test-Path $results) -and (Select-String -Path $results -Pattern 'result="Passed"' -Quiet)) { Write-Host "`nClips are in Marketing/Clips." }
else { Write-Host "`nSomething failed: see $log"; exit 1 }
