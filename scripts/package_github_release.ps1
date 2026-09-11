param(
    [string]$OutputDirectory = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Publish = Join-Path $Root 'artifacts\publish\win-x64'
$Stage = Join-Path $Root ('artifacts\github-release-' + [Guid]::NewGuid().ToString('N'))
$ProgramFolderName = -join @(0x5B66,0x4E60,0x901A,0x8BFE,0x7A0B,0x64AD,0x653E,0x8F85,0x52A9 | ForEach-Object { [char]$_ })
$Program = Join-Path $Stage $ProgramFolderName
$Destination = Join-Path $OutputDirectory 'ChaoxingLearningAssistant_v1.32_Windows.zip'
$TutorialHtml = Get-ChildItem -LiteralPath $Root -Filter '*.html' -File | Select-Object -First 1
if (-not $TutorialHtml) { throw 'Tutorial HTML is missing.' }
$TutorialMarkdown = Join-Path $Root ([System.IO.Path]::ChangeExtension($TutorialHtml.Name, '.md'))

if (-not (Test-Path -LiteralPath (Join-Path $Publish 'ChaoxingLearningAssistant.exe'))) {
    throw 'Run BUILD_FULL.bat first; self-contained win-x64 output is missing.'
}

New-Item -ItemType Directory -Path $Program -Force | Out-Null
try {
    Copy-Item -LiteralPath $TutorialHtml.FullName -Destination $Stage
    Copy-Item -LiteralPath $TutorialMarkdown -Destination $Stage
    Get-ChildItem -LiteralPath $Publish | Where-Object {
        $_.Name -notin @('Data', 'WebView2', 'Logs', 'Diagnostics', 'portable.flag')
    } | Copy-Item -Destination $Program -Recurse -Force
    New-Item -ItemType File -Path (Join-Path $Program 'portable.flag') -Force | Out-Null

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }
    $TutorialHtmlStage = Join-Path $Stage $TutorialHtml.Name
    $TutorialMarkdownStage = Join-Path $Stage ([System.IO.Path]::GetFileName($TutorialMarkdown))
    Compress-Archive -LiteralPath $TutorialHtmlStage,$TutorialMarkdownStage,$Program -DestinationPath $Destination -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $Stage) {
        Remove-Item -LiteralPath $Stage -Recurse -Force
    }
}

Write-Host "GITHUB_RELEASE_PACKAGE=$Destination"
Write-Host "SHA256=$((Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash.ToLowerInvariant())"
