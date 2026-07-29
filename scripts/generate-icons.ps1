[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$iconDirectory = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($PSScriptRoot, '..', 'src', 'DisplayLight.App', 'Assets', 'Icons'))
[System.IO.Directory]::CreateDirectory($iconDirectory) | Out-Null

$brandColor = [System.Drawing.ColorTranslator]::FromHtml('#0E7490')
$white = [System.Drawing.Color]::White
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-DisplayLightBitmap {
    param(
        [ValidateSet('Normal', 'Active', 'Waiting', 'Error')]
        [string]$State,
        [int]$Size
    )

    $renderSize = $Size * 4
    $scale = $renderSize / 256.0
    $bitmap = [System.Drawing.Bitmap]::new(
        $renderSize,
        $renderSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $brandBrush = [System.Drawing.SolidBrush]::new($brandColor)
        $whiteBrush = [System.Drawing.SolidBrush]::new($white)
        $insetPen = [System.Drawing.Pen]::new($white, 9 * $scale)
        $eyePen = [System.Drawing.Pen]::new($white, 10 * $scale)
        $insetPen.Alignment = [System.Drawing.Drawing2D.PenAlignment]::Center
        $eyePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $eyePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        try {
            $screenPath = New-RoundedRectanglePath `
                -X (8 * $scale) `
                -Y (8 * $scale) `
                -Width (240 * $scale) `
                -Height (184 * $scale) `
                -Radius (29 * $scale)
            $basePath = New-RoundedRectanglePath `
                -X (70 * $scale) `
                -Y (218 * $scale) `
                -Width (116 * $scale) `
                -Height (26 * $scale) `
                -Radius (13 * $scale)
            $insetPath = New-RoundedRectanglePath `
                -X (21 * $scale) `
                -Y (21 * $scale) `
                -Width (214 * $scale) `
                -Height (158 * $scale) `
                -Radius (20 * $scale)

            try {
                $graphics.FillPath($brandBrush, $screenPath)
                $graphics.FillRectangle(
                    $brandBrush,
                    110 * $scale,
                    187 * $scale,
                    36 * $scale,
                    38 * $scale)
                $graphics.FillPath($brandBrush, $basePath)
                $graphics.DrawPath($insetPen, $insetPath)

                switch ($State) {
                    'Normal' {
                        $graphics.FillEllipse(
                            $whiteBrush,
                            72 * $scale,
                            81 * $scale,
                            24 * $scale,
                            44 * $scale)
                        $graphics.DrawBezier(
                            $eyePen,
                            154 * $scale,
                            102 * $scale,
                            164 * $scale,
                            113 * $scale,
                            180 * $scale,
                            113 * $scale,
                            190 * $scale,
                            102 * $scale)
                    }
                    'Active' {
                        $graphics.FillEllipse(
                            $whiteBrush,
                            72 * $scale,
                            81 * $scale,
                            24 * $scale,
                            44 * $scale)
                        $graphics.FillEllipse(
                            $whiteBrush,
                            160 * $scale,
                            81 * $scale,
                            24 * $scale,
                            44 * $scale)
                    }
                    'Waiting' {
                        $graphics.DrawBezier(
                            $eyePen,
                            66 * $scale,
                            102 * $scale,
                            76 * $scale,
                            113 * $scale,
                            92 * $scale,
                            113 * $scale,
                            102 * $scale,
                            102 * $scale)
                        $graphics.DrawBezier(
                            $eyePen,
                            154 * $scale,
                            102 * $scale,
                            164 * $scale,
                            113 * $scale,
                            180 * $scale,
                            113 * $scale,
                            190 * $scale,
                            102 * $scale)
                    }
                    'Error' {
                        foreach ($centerX in @(84, 172)) {
                            $graphics.DrawLine(
                                $eyePen,
                                ($centerX - 12) * $scale,
                                91 * $scale,
                                ($centerX + 12) * $scale,
                                115 * $scale)
                            $graphics.DrawLine(
                                $eyePen,
                                ($centerX + 12) * $scale,
                                91 * $scale,
                                ($centerX - 12) * $scale,
                                115 * $scale)
                        }
                    }
                }
            }
            finally {
                $screenPath.Dispose()
                $basePath.Dispose()
                $insetPath.Dispose()
            }
        }
        finally {
            $brandBrush.Dispose()
            $whiteBrush.Dispose()
            $insetPen.Dispose()
            $eyePen.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    if ($renderSize -eq $Size) {
        return $bitmap
    }

    $result = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $resultGraphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $resultGraphics.Clear([System.Drawing.Color]::Transparent)
        $resultGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $resultGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $resultGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $resultGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $resultGraphics.DrawImage(
            $bitmap,
            [System.Drawing.Rectangle]::new(0, 0, $Size, $Size),
            0,
            0,
            $renderSize,
            $renderSize,
            [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $resultGraphics.Dispose()
        $bitmap.Dispose()
    }

    return $result
}

function Save-MultiSizeIcon {
    param(
        [string]$State,
        [string]$Path
    )

    $images = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $bitmap = New-DisplayLightBitmap -State $State -Size $size
        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $images.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
            $bitmap.Dispose()
        }
    }

    $file = [System.IO.File]::Create($Path)
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $image = $images[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$image.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Length
        }

        foreach ($image in $images) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}

foreach ($state in @('Normal', 'Active', 'Waiting', 'Error')) {
    $preview = New-DisplayLightBitmap -State $state -Size 256
    try {
        $preview.Save(
            [System.IO.Path]::Combine($iconDirectory, "DisplayLight.$state.png"),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $preview.Dispose()
    }

    Save-MultiSizeIcon `
        -State $state `
        -Path ([System.IO.Path]::Combine($iconDirectory, "DisplayLight.$state.ico"))
}

[System.IO.File]::Copy(
    [System.IO.Path]::Combine($iconDirectory, 'DisplayLight.Normal.ico'),
    [System.IO.Path]::Combine($iconDirectory, 'DisplayLight.ico'),
    $true)

Write-Host "DisplayLight icons generated in $iconDirectory"
