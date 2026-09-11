param(
    [string]$OutputDirectory = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Version = 'v1.30'
$FolderName = 'ChaoxingLearningAssistant_v1.30'
$ManifestPath = Join-Path $Root 'SOURCE_SHA256.json'
$MasterPath = Join-Path $Root 'ChaoxingLearningAssistant_MASTER_v1.30.txt'
$PayloadZip = Join-Path $OutputDirectory 'ChaoxingLearningAssistant_v1.30_master_payload.zip'
$SourceZip = Join-Path $OutputDirectory 'ChaoxingLearningAssistant_v1.30_source.zip'
$ReferenceZip = Join-Path $OutputDirectory 'ChaoxingLearningAssistant_v1.29_source.zip'
$Utf8 = [System.Text.UTF8Encoding]::new($false)
$Utf8Bom = [System.Text.UTF8Encoding]::new($true)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Get-RelativePath([string]$Path) {
    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $rootUri = [Uri]::new($rootPath)
    $pathUri = [Uri]::new([System.IO.Path]::GetFullPath($Path))
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
}

function Get-PayloadFiles {
    Get-ChildItem -LiteralPath $Root -File -Recurse | Where-Object {
        $relative = Get-RelativePath $_.FullName
        $parts = $relative -split '/'
        $parts -notcontains 'bin' -and
        $parts -notcontains 'obj' -and
        $parts -notcontains 'artifacts' -and
        $parts -notcontains 'reference' -and
        $_.Name -notlike 'ChaoxingLearningAssistant_MASTER_v*.txt' -and
        $_.Name -ne 'BUILD_LOG.txt'
    } | Sort-Object FullName
}

function New-Zip([System.IO.FileInfo[]]$Files, [string]$Destination, [string]$Prefix = '') {
    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }

    $archive = [System.IO.Compression.ZipFile]::Open($Destination, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $Files) {
            $relative = Get-RelativePath $file.FullName
            $entryName = if ($Prefix) { "$Prefix/$relative" } else { $relative }
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

# The manifest intentionally excludes itself and MASTER to avoid recursive hashes.
$hashes = [ordered]@{}
foreach ($file in @(Get-PayloadFiles | Where-Object { $_.FullName -ne $ManifestPath })) {
    $hashes[(Get-RelativePath $file.FullName)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
[System.IO.File]::WriteAllText($ManifestPath, ($hashes | ConvertTo-Json) + "`n", $Utf8)

$payloadFiles = @(Get-PayloadFiles)
New-Zip -Files $payloadFiles -Destination $PayloadZip
$payloadBytes = [System.IO.File]::ReadAllBytes($PayloadZip)
$payloadHashHex = (Get-FileHash -LiteralPath $PayloadZip -Algorithm SHA256).Hash.ToLowerInvariant()
$payloadBase64 = [Convert]::ToBase64String($payloadBytes, [Base64FormattingOptions]::InsertLineBreaks)

$restoreScript = [System.IO.File]::ReadAllText((Join-Path $Root 'scripts\restore_master.py'), $Utf8).TrimEnd()
$expectations = [System.IO.File]::ReadAllText((Join-Path $Root 'PRODUCT_EXPECTATIONS.md'), $Utf8).TrimEnd()
$releaseFiles = Get-ChildItem -LiteralPath $Root -Filter 'RELEASE_v*.md' | Sort-Object Name
$releaseHistory = ($releaseFiles | ForEach-Object {
    "===== $($_.Name) =====`n" + [System.IO.File]::ReadAllText($_.FullName, $Utf8).TrimEnd()
}) -join "`n`n"
$changeLog = [System.IO.File]::ReadAllText((Join-Path $Root 'CHANGELOG.md'), $Utf8).TrimEnd()
$errorArchive = [System.IO.File]::ReadAllText((Join-Path $Root 'ERROR_ARCHIVE.md'), $Utf8).TrimEnd()
$testReport = [System.IO.File]::ReadAllText((Join-Path $Root 'TEST_REPORT.md'), $Utf8).TrimEnd()
$userGuide = [System.IO.File]::ReadAllText((Join-Path $Root 'USER_GUIDE.md'), $Utf8).TrimEnd()
$knownLimits = [System.IO.File]::ReadAllText((Join-Path $Root 'KNOWN_LIMITATIONS.md'), $Utf8).TrimEnd()

$master = @"
学习通课程播放辅助 — 累计式自包含 MASTER v1.30
日期：2026-09-11。当前开发基线为 v1.30，历史测试结论不得套用到本版。

本文件不再嵌套已损坏的旧 MASTER 字符串，而是从当前可读档案重新汇总全部版本历史。
只保留本 TXT 也能恢复源码：将下方 Python 代码保存为 restore_master.py，运行：
python restore_master.py ChaoxingLearningAssistant_MASTER_v1.30.txt restored_v1.30
需 Python 3，目标目录必须不存在。

===== 还原脚本开始 =====
$restoreScript
===== 还原脚本结束 =====

===== 最终产品预期 =====
$expectations

===== 各版本 Release 记录 =====
$releaseHistory

===== 累计变更记录 =====
$changeLog

===== 累计错误档案 =====
$errorArchive

===== 累计测试报告 =====
$testReport

===== 当前使用说明 =====
$userGuide

===== 当前已知边界 =====
$knownLimits

SOURCE_ZIP_SHA256=$payloadHashHex
===== V1.30 SOURCE ZIP BASE64 BEGIN =====
$payloadBase64
===== V1.30 SOURCE ZIP BASE64 END =====
"@
if ($master.Contains([char]0xFFFD) -or $master -match '瀛︿|閫氳|绔犺|鎾斁') {
    throw 'MASTER contains replacement characters or known mojibake markers.'
}
# MASTER 面向 Windows 记事本直接阅读，保留 UTF-8 BOM，避免旧版记事本误判为 ANSI 后显示乱码。
[System.IO.File]::WriteAllText($MasterPath, $master, $Utf8Bom)

$outerFiles = @($payloadFiles) + @((Get-Item -LiteralPath $MasterPath))
New-Zip -Files $outerFiles -Destination $SourceZip -Prefix $FolderName

if (Test-Path -LiteralPath $ReferenceZip) {
    $archive = [System.IO.Compression.ZipFile]::Open($SourceZip, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $ReferenceZip,
            'reference/ChaoxingLearningAssistant_v1.29_source.zip',
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "SOURCE_PACKAGE=$SourceZip"
Write-Host "MASTER=$MasterPath"
Write-Host "PAYLOAD_SHA256=$payloadHashHex"
