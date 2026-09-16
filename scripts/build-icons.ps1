$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'IconRaster.cs') -ReferencedAssemblies @([Drawing.Bitmap].Assembly.Location, [Drawing.Rectangle].Assembly.Location)
$root = Split-Path $PSScriptRoot
$inputDir = Join-Path $root 'publish/icon-originals'
$outputDir = Join-Path $root 'src/FullLengthPlayer/GeneratedIcons'
$evidence = Join-Path $root 'test-results'
New-Item -ItemType Directory -Force $inputDir,$outputDir,$evidence | Out-Null
Expand-Archive -LiteralPath (Join-Path $root 'FLP icons.zip') -DestinationPath $inputDir -Force
$originals = [ordered]@{
    'slate-red-solid' = '6D660D27-69DD-4700-9409-8E2EE9EEDF44.PNG'
    'teal-gold-solid' = '1626C306-02B9-4344-9345-AE9BAE6ED768.PNG'
    'teal-orange-solid' = 'E7FCB506-8426-45D8-BAD3-FBEA9DAC3049.PNG'
    'slate-red-glass' = '00C1BB58-F32A-40D4-9499-3715ACE74F8B.PNG'
    'teal-gold-glass' = 'AF674AD1-169E-417E-877E-0DDD13ED35A6.PNG'
    'teal-orange-glass' = 'A7DE3856-13DF-4C9F-8385-A12167ADC4A9.PNG'
}
$sizes = @(16,20,24,32,40,48,64,96,128,256)
$sheet = [Drawing.Bitmap]::new(700,600)
$canvas = [Drawing.Graphics]::FromImage($sheet)
$font = [Drawing.Font]::new('Segoe UI',9)
$canvas.Clear([Drawing.Color]::White)
$report = @(); $row = 0
try {
    foreach ($entry in $originals.GetEnumerator()) {
        $original = [Drawing.Bitmap]::new((Join-Path $inputDir $entry.Value))
        try {
            $crop = [IconRaster]::Bounds($original)
            $frames = @()
            $report += @{ id=$entry.Key; original=@($original.Width,$original.Height); crop=@($crop.X,$crop.Y,$crop.Width,$crop.Height); sizes=$sizes }
            $canvas.FillRectangle([Drawing.Brushes]::White,0,$row*100,350,100)
            $dark = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(35,35,35))
            try { $canvas.FillRectangle($dark,350,$row*100,350,100) } finally { $dark.Dispose() }
            $canvas.DrawString($entry.Key,$font,[Drawing.Brushes]::Black,5,$row*100+2)
            $canvas.DrawString($entry.Key,$font,[Drawing.Brushes]::White,355,$row*100+2)
            $previewX = 5
            foreach ($size in $sizes) {
                $bitmap = [IconRaster]::Render($original,$crop,$size)
                try {
                    if ($bitmap.GetPixel(0,0).A -ne 0 -or $bitmap.GetPixel($size-1,$size-1).A -ne 0) { throw "Opaque corner: $($entry.Key) $size" }
                    $memory = [IO.MemoryStream]::new()
                    try { $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png); $frames += ,$memory.ToArray() }
                    finally { $memory.Dispose() }
                    if ($entry.Key -eq 'teal-orange-solid' -and $size -eq 256) {
                        Write-Host ('README_ICON:' + [Convert]::ToBase64String($frames[-1]))
                    }
                    if ($size -in @(16,24,32,48,64,96)) {
                        $displaySize = [Math]::Min($size,72)
                        foreach ($shift in @(0,350)) {
                            $canvas.DrawImage($bitmap,$previewX+$shift,$row*100+24,$displaySize,$displaySize)
                        }
                        $previewX += $displaySize+8
                    }
                } finally { $bitmap.Dispose() }
            }
            $writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $outputDir ($entry.Key + '.ico'))))
            try {
                $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
                $offset = 6 + 16 * $frames.Count
                for ($i=0; $i -lt $frames.Count; $i++) {
                    $side = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
                    $writer.Write([byte]$side); $writer.Write([byte]$side); $writer.Write([byte]0); $writer.Write([byte]0)
                    $writer.Write([uint16]1); $writer.Write([uint16]32)
                    $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
                    $offset += $frames[$i].Length
                }
                foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
            } finally { $writer.Dispose() }
        } finally { $original.Dispose() }
        $row++
    }
    $sheet.Save((Join-Path $evidence 'icon-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
    $report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'icon-sizes.json')
    Write-Host ('ICON_BOUNDS:' + ($report | ConvertTo-Json -Depth 5 -Compress))
    Write-Host ('ICON_PREVIEW:' + [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $evidence 'icon-preview.png'))))
} finally { $font.Dispose(); $canvas.Dispose(); $sheet.Dispose() }
