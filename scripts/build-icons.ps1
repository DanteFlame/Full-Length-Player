$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$inputDir = Join-Path $PSScriptRoot '../assets/icons'
$outputDir = Join-Path $PSScriptRoot '../src/FullLengthPlayer/GeneratedIcons'
New-Item -ItemType Directory -Force $outputDir | Out-Null
# Package the supplied artwork at the native Windows icon sizes. No new artwork.
foreach ($file in Get-ChildItem $inputDir -Filter '*.jpeg') {
    $original = [Drawing.Image]::FromFile($file.FullName)
    try {
        $frames = @()
        foreach ($size in @(16,24,32,48,64,128,256)) {
            $bitmap = [Drawing.Bitmap]::new($size,$size)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($original,0,0,$size,$size)
                $memory = [IO.MemoryStream]::new()
                try { $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png); $frames += ,$memory.ToArray() }
                finally { $memory.Dispose() }
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
        $stream = [IO.File]::Create((Join-Path $outputDir ($file.BaseName + '.ico')))
        $writer = [IO.BinaryWriter]::new($stream)
        try {
            $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
            $offset = 6 + 16 * $frames.Count
            $sizes = @(16,24,32,48,64,128,256)
            for ($i=0; $i -lt $frames.Count; $i++) {
                $side = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
                $writer.Write([byte]$side); $writer.Write([byte]$side); $writer.Write([byte]0); $writer.Write([byte]0)
                $writer.Write([uint16]1); $writer.Write([uint16]32)
                $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
                $offset += $frames[$i].Length
            }
            foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
        } finally { $writer.Dispose(); $stream.Dispose() }
    } finally { $original.Dispose() }
}
