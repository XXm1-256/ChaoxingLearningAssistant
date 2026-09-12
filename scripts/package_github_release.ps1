param(
    [string]$OutputDirectory = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Publish = Join-Path $Root 'artifacts\publish\win-x64'
$FullReadyMarker = Join-Path $Root 'artifacts\FULL_BUILD_READY.txt'
$ProjectFile = Join-Path $Root 'src\ChaoxingLearningAssistant\ChaoxingLearningAssistant.csproj'
$Stage = Join-Path $Root ('artifacts\github-release-' + [Guid]::NewGuid().ToString('N'))
$ProgramFolderName = -join @(0x5B66,0x4E60,0x901A,0x8BFE,0x7A0B,0x89C6,0x9891,0x64AD,0x653E,0x52A9,0x624B | ForEach-Object { [char]$_ })
$Program = Join-Path $Stage $ProgramFolderName
$Destination = Join-Path $OutputDirectory 'ChaoxingLearningAssistant_v1.46_Windows.zip'
$TutorialMarkdown = Join-Path $Root '使用教学.md'
$TutorialDocx = Join-Path $Root '使用教学.docx'
$TutorialPdf = Join-Path $Root '使用教学.pdf'
$FaqMarkdown = Join-Path $Root '常见问题与处理方法.md'
$FaqDocx = Join-Path $Root '常见问题与处理方法.docx'
$FaqPdf = Join-Path $Root '常见问题与处理方法.pdf'
foreach ($tutorial in @($TutorialMarkdown, $TutorialDocx, $TutorialPdf, $FaqMarkdown, $FaqDocx, $FaqPdf)) {
    if (-not (Test-Path -LiteralPath $tutorial)) { throw "Tutorial file is missing: $tutorial" }
}

$PublishedExe = Join-Path $Publish 'ChaoxingLearningAssistant.exe'
if (-not (Test-Path -LiteralPath $PublishedExe)) {
    throw 'Run BUILD_FULL.bat first; self-contained win-x64 output is missing.'
}
[xml]$ProjectXml = Get-Content -LiteralPath $ProjectFile
$ExpectedVersion = [string]$ProjectXml.Project.PropertyGroup.Version
$ActualVersion = (Get-Item -LiteralPath $PublishedExe).VersionInfo.FileVersion
if ($ActualVersion -notlike "$ExpectedVersion*") {
    throw "Published EXE version mismatch: expected $ExpectedVersion, found $ActualVersion"
}
if (-not (Test-Path -LiteralPath $FullReadyMarker)) {
    throw 'Run BUILD_FULL.bat first; the full-build verification marker is missing.'
}
$Marker = Get-Content -LiteralPath $FullReadyMarker -Raw
$ActualHash = (Get-FileHash -LiteralPath $PublishedExe -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Marker -notmatch [regex]::Escape("Version: $ExpectedVersion") -or
    $Marker -notmatch [regex]::Escape("ExeSha256: $ActualHash")) {
    throw 'The publish directory does not match the latest verified full build.'
}

New-Item -ItemType Directory -Path $Program -Force | Out-Null
try {
    Copy-Item -LiteralPath $TutorialMarkdown -Destination $Stage
    Copy-Item -LiteralPath $TutorialDocx -Destination $Stage
    Copy-Item -LiteralPath $TutorialPdf -Destination $Stage
    Copy-Item -LiteralPath $FaqMarkdown -Destination $Stage
    Copy-Item -LiteralPath $FaqDocx -Destination $Stage
    Copy-Item -LiteralPath $FaqPdf -Destination $Stage
    Get-ChildItem -LiteralPath $Publish | Where-Object {
        $_.Name -notin @('Data', 'WebView2', 'Logs', 'Diagnostics', 'portable.flag')
    } | Copy-Item -Destination $Program -Recurse -Force
    New-Item -ItemType File -Path (Join-Path $Program 'portable.flag') -Force | Out-Null

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }
    $TutorialMarkdownStage = Join-Path $Stage ([System.IO.Path]::GetFileName($TutorialMarkdown))
    $TutorialDocxStage = Join-Path $Stage ([System.IO.Path]::GetFileName($TutorialDocx))
    $TutorialPdfStage = Join-Path $Stage ([System.IO.Path]::GetFileName($TutorialPdf))
    $FaqMarkdownStage = Join-Path $Stage ([System.IO.Path]::GetFileName($FaqMarkdown))
    $FaqDocxStage = Join-Path $Stage ([System.IO.Path]::GetFileName($FaqDocx))
    $FaqPdfStage = Join-Path $Stage ([System.IO.Path]::GetFileName($FaqPdf))
    Compress-Archive -LiteralPath $TutorialMarkdownStage,$TutorialDocxStage,$TutorialPdfStage,$FaqMarkdownStage,$FaqDocxStage,$FaqPdfStage,$Program -DestinationPath $Destination -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $Stage) {
        $resolvedStage = [IO.Path]::GetFullPath($Stage)
        $artifactRoot = [IO.Path]::GetFullPath((Join-Path $Root 'artifacts')).TrimEnd('\') + '\'
        if (-not $resolvedStage.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing cleanup outside artifacts.'
        }
        Remove-Item -LiteralPath $Stage -Recurse -Force
    }
}

Write-Host "GITHUB_RELEASE_PACKAGE=$Destination"
Write-Host "SHA256=$((Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash.ToLowerInvariant())"
