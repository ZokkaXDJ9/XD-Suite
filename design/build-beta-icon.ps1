[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AppSource, [Parameter(Mandatory=$true)][string]$Artwork, [Parameter(Mandatory=$true)][string]$SymbolColor)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$assets = Join-Path $AppSource 'Assets'
[xml]$svg = [System.IO.File]::ReadAllText($Artwork)
foreach($shape in $svg.DocumentElement.ChildNodes){ if($shape.LocalName -eq 'rect'){$shape.SetAttribute('fill','#39FF14')} else {foreach($attribute in @('fill','stroke')){ $value=$shape.GetAttribute($attribute); if($value -match '(?i)^(#FFFFFF|white|#C7EF92)$'){$shape.SetAttribute($attribute,$SymbolColor)} elseif($value -and $value -ne 'none'){$shape.SetAttribute($attribute,'#39FF14')} } } }
function Brush([string]$value) { if (-not $value -or $value -eq 'none') { return $null }; [System.Windows.Media.BrushConverter]::new().ConvertFromInvariantString($value) }
$images = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $draw = $visual.RenderOpen()
    $draw.PushTransform([System.Windows.Media.ScaleTransform]::new($size/128.0,$size/128.0))
    foreach ($shape in $svg.DocumentElement.ChildNodes) {
        if ($shape.LocalName -eq 'rect') {
            $draw.DrawRoundedRectangle((Brush $shape.GetAttribute('fill')),$null,[System.Windows.Rect]::new(0,0,128,128),28,28)
        } elseif ($shape.LocalName -eq 'path') {
            $pen = $null
            if ($shape.GetAttribute('stroke')) {
                $pen = [System.Windows.Media.Pen]::new((Brush $shape.GetAttribute('stroke')),[double]::Parse($shape.GetAttribute('stroke-width'),[Globalization.CultureInfo]::InvariantCulture))
                $pen.StartLineCap = $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
                $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
            }
            $draw.DrawGeometry((Brush $shape.GetAttribute('fill')),$pen,[System.Windows.Media.Geometry]::Parse($shape.GetAttribute('d')))
        }
    }
    $draw.Pop(); $draw.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    if ($size -ge 64) {
        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $buffer = New-Object System.IO.MemoryStream
        $encoder.Save($buffer); $bytes = $buffer.ToArray(); $buffer.Dispose()
        if ($size -eq 256) { [System.IO.File]::WriteAllBytes((Join-Path $assets 'Beta-256.png'),$bytes) }
    } else {
        $straight = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap,[System.Windows.Media.PixelFormats]::Bgra32,$null,0)
        $pixels = New-Object byte[] ($size*$size*4); $straight.CopyPixels($pixels,$size*4,0)
        $maskStride = [int]([Math]::Ceiling($size/32.0)*4)
        $buffer = New-Object System.IO.MemoryStream
        $writer = [System.IO.BinaryWriter]::new($buffer)
        $writer.Write([int]40); $writer.Write([int]$size); $writer.Write([int]($size*2)); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([int]0); $writer.Write([int]($size*$size*4+$maskStride*$size))
        for ($i=0;$i -lt 4;$i++) { $writer.Write([int]0) }
        for ($y=$size-1;$y -ge 0;$y--) { $writer.Write($pixels,$y*$size*4,$size*4) }
        for ($y=$size-1;$y -ge 0;$y--) {
            $mask = New-Object byte[] $maskStride
            for ($x=0;$x -lt $size;$x++) { if ($pixels[($y*$size+$x)*4+3] -eq 0) { $index = [int][Math]::Floor($x/8.0); $mask[$index] = $mask[$index] -bor (0x80 -shr ($x%8)) } }
            $writer.Write($mask)
        }
        $writer.Flush(); $bytes = $buffer.ToArray(); $writer.Dispose()
    }
    $images += [pscustomobject]@{Size=$size;Bytes=$bytes}
}
$output = [System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $assets 'Beta.ico')))
try {
    $output.Write([uint16]0); $output.Write([uint16]1); $output.Write([uint16]$images.Count)
    $offset = 6 + 16*$images.Count
    foreach ($entry in $images) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $output.Write([byte]$dimension); $output.Write([byte]$dimension); $output.Write([byte]0); $output.Write([byte]0)
        $output.Write([uint16]1); $output.Write([uint16]32); $output.Write([uint32]$entry.Bytes.Length); $output.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $images) { $output.Write([byte[]]$entry.Bytes) }
} finally { $output.Dispose() }
Write-Host 'Generated tag editor icon: 16/20/24/32/40/48/64/128/256 pixels.'
