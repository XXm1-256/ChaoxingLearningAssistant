param([switch]$Full)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_UI_LANGUAGE = 'en-US'

$Root = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $Root 'ChaoxingLearningAssistant.sln'
$Project = Join-Path $Root 'src\ChaoxingLearningAssistant\ChaoxingLearningAssistant.csproj'
$Tests = Join-Path $Root 'tests\ChaoxingLearningAssistant.Tests\ChaoxingLearningAssistant.Tests.csproj'
$Artifacts = Join-Path $Root 'artifacts'
$QuickRun = Join-Path $Artifacts 'quick-run'
$Publish = Join-Path $Artifacts 'publish\win-x64'
$PortableStage = Join-Path $Artifacts ('portable-stage-' + [Guid]::NewGuid().ToString('N'))
$ReadyMarker = Join-Path $Artifacts 'QUICK_RUN_READY.txt'
if (Test-Path $ReadyMarker) { Remove-Item $ReadyMarker -Force }
Set-Location $Root

function Assert-DotNetSdk {
    $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'DOTNET_NOT_FOUND: Install Microsoft .NET 8 SDK x64, then restart Windows.'
    }

    $sdks = @(dotnet --list-sdks 2>$null)
    if ($LASTEXITCODE -ne 0 -or $sdks.Count -eq 0) {
        throw 'DOTNET_SDK_NOT_FOUND: dotnet host exists, but no .NET SDK is installed. Install Microsoft .NET 8 SDK x64.'
    }

    $hasNet8 = $false
    foreach ($sdk in $sdks) {
        if ($sdk -match '^8\.') {
            $hasNet8 = $true
            break
        }
    }
    if (-not $hasNet8) {
        throw 'DOTNET_8_SDK_NOT_FOUND: A .NET SDK is installed, but .NET 8 SDK is required.'
    }

    Write-Host 'Detected SDKs:'
    $sdks | ForEach-Object { Write-Host "  $_" }
}

function Run-DotNet {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string[]]$Args
    )
    Write-Host ''
    Write-Host "== $Name =="
    & dotnet @Args
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Assert-DotNetSdk

Run-DotNet -Name 'Restore' -Args @('restore', $Solution)
Run-DotNet -Name 'Build' -Args @('build', $Solution, '-c', 'Release', '--no-restore')
Run-DotNet -Name 'Test' -Args @('test', $Tests, '-c', 'Release', '--no-build')

# Keep existing Data/profile directories. Rebuilding must never delete user data.
New-Item -ItemType Directory -Path $QuickRun -Force | Out-Null
New-Item -ItemType Directory -Path $Publish -Force | Out-Null

Write-Host ''
Write-Host '== Quick-run publish =='
Write-Host 'Creating a framework-dependent EXE for this computer first...'
Run-DotNet -Name 'Quick-run publish' -Args @(
    'publish', $Project,
    '-c', 'Release',
    '--no-restore',
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-o', $QuickRun
)
$QuickExe = Join-Path $QuickRun 'ChaoxingLearningAssistant.exe'
if (-not (Test-Path $QuickExe)) {
    throw 'QUICK_RUN_EXE_NOT_FOUND: quick-run publish completed but the EXE was not found.'
}
Set-Content -Path (Join-Path $Artifacts 'QUICK_RUN_READY.txt') -Value @(
    'Version: 1.39.0',
    'Quick-run build is ready.',
    'Open: artifacts\quick-run\ChaoxingLearningAssistant.exe',
    'This build requires .NET 8 Desktop Runtime on the computer.'
) -Encoding ASCII

Write-Host ''
Write-Host '==================================================='
Write-Host 'QUICK_RUN_READY'
Write-Host "EXE: $QuickExe"
Write-Host '==================================================='
Write-Host ''

if (-not $Full) {
    Write-Host 'QUICK_BUILD_SUCCESS'
    Write-Host 'Ready to use: artifacts\quick-run\ChaoxingLearningAssistant.exe'
    Write-Host 'Optional portable ZIP / installer: run BUILD_FULL.bat later.'
    exit 0
}

Write-Host '== Restore win-x64 runtime packs =='
Write-Host 'First self-contained build may download Microsoft runtime packs.'
Write-Host 'This step can stay quiet for a while on a slow network.'
Write-Host 'Do NOT press Ctrl+C and do NOT close the window.'
Run-DotNet -Name 'Restore win-x64 runtime packs' -Args @(
    'restore', $Project,
    '-r', 'win-x64'
)

Run-DotNet -Name 'Publish self-contained win-x64' -Args @(
    'publish', $Project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '--no-restore',
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-o', $Publish
)

Write-Host ''
Write-Host '== Portable ZIP =='
New-Item -ItemType Directory -Path $PortableStage -Force | Out-Null
Get-ChildItem $Publish | Where-Object {
    $_.Name -notin @('Data', 'WebView2', 'Logs', 'Diagnostics', 'portable.flag')
} | Copy-Item -Destination $PortableStage -Recurse -Force
New-Item -ItemType File -Path (Join-Path $PortableStage 'portable.flag') -Force | Out-Null
$PortableZip = Join-Path $Artifacts 'ChaoxingLearningAssistant_Portable_win-x64.zip'
if (Test-Path $PortableZip) { Remove-Item $PortableZip -Force }
Compress-Archive -Path (Join-Path $PortableStage '*') -DestinationPath $PortableZip -CompressionLevel Optimal
Remove-Item $PortableStage -Recurse -Force

Write-Host ''
Write-Host '== Installer =='
$IsccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$Iscc = $IsccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if ($Iscc) {
    & $Iscc (Join-Path $Root 'Installer\ChaoxingLearningAssistant.iss')
    if ($LASTEXITCODE -ne 0) {
        throw "Installer build failed with exit code $LASTEXITCODE"
    }
} else {
    Write-Warning 'Inno Setup 6 was not detected. Installer build is skipped; quick-run and portable outputs remain available.'
}

Write-Host ''
Write-Host 'BUILD_SUCCESS'
Write-Host "Artifacts: $Artifacts"
