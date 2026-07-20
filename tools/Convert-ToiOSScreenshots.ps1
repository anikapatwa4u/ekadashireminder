# Convert-ToiOSScreenshots.ps1
# Converts Android portrait screenshots to iOS App Store screenshot sizes.
# Scales each image to fit inside the target size (preserving aspect ratio)
# and pads the remaining space with a solid background color (letterbox),
# so images are never stretched or distorted.

param(
    [string]$SourceDir = "C:\Users\pawank\OneDrive - Microsoft\Pictures\Android",
    [string]$OutputDir = "C:\Users\pawank\OneDrive - Microsoft\Pictures\iOS",
    # Target iOS App Store screenshot size (6.5" display). 1242x2688 or 1284x2778 accepted.
    [int]$TargetWidth  = 1242,
    [int]$TargetHeight = 2688,
    # Background used for the padded (letterbox) area. App theme orange by default.
    [string]$BackgroundHex = "#FF7722",
    # Only convert these files (portrait screenshots). Add/remove as needed.
    [string[]]$Files = @("main.png", "city.png", "Coutry.png")
)

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Parse background color
$bg = [System.Windows.Media.Color]::FromRgb(
    [Convert]::ToByte($BackgroundHex.Substring(1,2),16),
    [Convert]::ToByte($BackgroundHex.Substring(3,2),16),
    [Convert]::ToByte($BackgroundHex.Substring(5,2),16))
$bgBrush = New-Object System.Windows.Media.SolidColorBrush($bg)

foreach ($file in $Files) {
    $srcPath = Join-Path $SourceDir $file
    if (-not (Test-Path $srcPath)) {
        Write-Warning "Skipping missing file: $srcPath"
        continue
    }

    # Load source
    $stream = [System.IO.File]::OpenRead($srcPath)
    $frame  = [System.Windows.Media.Imaging.BitmapFrame]::Create(
        $stream,
        [System.Windows.Media.Imaging.BitmapCreateOptions]::None,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $stream.Close()

    $srcW = $frame.PixelWidth
    $srcH = $frame.PixelHeight

    # Scale to fit inside target while preserving aspect ratio
    $scale = [Math]::Min($TargetWidth / $srcW, $TargetHeight / $srcH)
    $drawW = $srcW * $scale
    $drawH = $srcH * $scale
    $offX  = ($TargetWidth  - $drawW) / 2
    $offY  = ($TargetHeight - $drawH) / 2

    # Compose onto a target-sized canvas
    $visual = New-Object System.Windows.Media.DrawingVisual
    $ctx = $visual.RenderOpen()
    $ctx.DrawRectangle($bgBrush, $null,
        (New-Object System.Windows.Rect(0, 0, $TargetWidth, $TargetHeight)))
    $ctx.DrawImage($frame,
        (New-Object System.Windows.Rect($offX, $offY, $drawW, $drawH)))
    $ctx.Close()

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $TargetWidth, $TargetHeight, 96, 96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)

    # Encode PNG
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))

    $outName = [System.IO.Path]::GetFileNameWithoutExtension($file) +
               "_ios_${TargetWidth}x${TargetHeight}.png"
    $outPath = Join-Path $OutputDir $outName
    $outStream = [System.IO.File]::Create($outPath)
    $encoder.Save($outStream)
    $outStream.Close()

    Write-Host "Created $outPath ($srcW x $srcH  ->  $TargetWidth x $TargetHeight)"
}

Write-Host "`nDone. iOS screenshots are in: $OutputDir"
