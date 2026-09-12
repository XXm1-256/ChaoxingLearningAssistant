param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\validation\v1.44\app-main.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowCaptureNative {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
}
'@

$process = Get-Process -Name 'ChaoxingLearningAssistant' -ErrorAction Stop |
    Where-Object { $_.MainWindowHandle -ne 0 } |
    Sort-Object @{ Expression = { $_.MainWindowTitle -like '*v1.44*' }; Descending = $true },
                @{ Expression = { $_.StartTime }; Descending = $true } |
    Select-Object -First 1
if (-not $process) { throw 'The application window is not open.' }

$rect = New-Object WindowCaptureNative+RECT
if (-not [WindowCaptureNative]::GetWindowRect($process.MainWindowHandle, [ref]$rect)) {
    throw 'Could not read the application window bounds.'
}

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -lt 300 -or $height -lt 300) { throw 'The application window is too small to capture.' }

$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $hdc = $graphics.GetHdc()
    try {
        if (-not [WindowCaptureNative]::PrintWindow($process.MainWindowHandle, $hdc, 2)) {
            throw 'Could not render the application window.'
        }
    }
    finally {
        $graphics.ReleaseHdc($hdc)
    }
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host $OutputPath
