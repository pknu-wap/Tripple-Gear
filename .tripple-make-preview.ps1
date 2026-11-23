$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies ([Drawing.Bitmap].Assembly.Location),([Drawing.Color].Assembly.Location),([Drawing.Point].Assembly.Location) -TypeDefinition @'
using System;
using System.Drawing;
public static class ColorKey {
    public static void Remove(Bitmap image) {
        for (int y = 0; y < image.Height; y++) {
            for (int x = 0; x < image.Width; x++) {
                Color p = image.GetPixel(x, y);
                if (p.R > 110 && p.B > 95 && p.R > 1.45*p.G+28 && p.B > 1.4*p.G+25)
                    image.SetPixel(x, y, Color.Transparent);
            }
        }
    }
}
'@
$folder = Join-Path $PWD 'animation-previews'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$ink = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#CAD4DF'))
$line = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#374553'))
$floor = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#141E28'))
$font = [Drawing.Font]::new('Segoe UI', 12, [Drawing.FontStyle]::Bold)

foreach ($kind in @('melee','ranged')) {
    $source = Join-Path $PWD ".tripple-preview-$kind.b64"
    $buffer = [IO.MemoryStream]::new([Convert]::FromBase64String([IO.File]::ReadAllText($source)))
    $sheet = [Drawing.Image]::FromStream($buffer)
    $cells = @()
    $width = [int]($sheet.Width / 4)
    $height = [int]($sheet.Height / 2)
    for ($index = 0; $index -lt 8; $index++) {
        $cell = [Drawing.Bitmap]::new($width,$height)
        $cg = [Drawing.Graphics]::FromImage($cell)
        $row = [int][Math]::Floor($index/4)
        $area = [Drawing.Rectangle]::new(($index % 4)*$width,$row*$height,$width,$height)
        $cg.DrawImage($sheet,[Drawing.Rectangle]::new(0,0,$width,$height),$area,[Drawing.GraphicsUnit]::Pixel)
        $cg.Dispose()
        [ColorKey]::Remove($cell)
        $cells += $cell
    }
    $sheet.Dispose()
    $buffer.Dispose()

    if ($kind -eq 'melee') {
        $second = 'CLAW STRIKE'
        $offset = @(0,1,0,-2,0,-3,11,-2)
        $floorY = @(146,145,146,145,146,146,146,146)
    } else {
        $second = 'MISSILE RECOIL'
        $offset = @(0,0,0,0,0,0,-7,0)
        $floorY = @(149,149,149,149,149,149,149,149)
    }
    $attack = @(0,0,0,1,1,2,3,3,0,0,0,0)
    for ($tick = 0; $tick -lt $attack.Count; $tick++) {
        $frame = [Drawing.Bitmap]::new(696,221)
        $g = [Drawing.Graphics]::FromImage($frame)
        $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $g.Clear([Drawing.ColorTranslator]::FromHtml('#202C38'))
        $g.FillRectangle($floor,0,187,696,34)
        $g.DrawLine($line,0,187,696,187)
        $g.DrawLine($line,348,0,348,221)
        $g.DrawString('WALK',$font,$ink,17,13)
        $g.DrawString($second,$font,$ink,365,13)
        $indexes = @($tick % 4), @(4 + $attack[$tick])
        for ($panel = 0; $panel -lt 2; $panel++) {
            $index = [int]$indexes[$panel][0]
            $drawWidth = 177
            $drawHeight = [int]($drawWidth*$height/$width)
            $anchor = [int]($floorY[$index]*$drawHeight/175)
            $g.DrawImage($cells[$index],[Drawing.Rectangle]::new(85+$panel*348+$offset[$index],188-$anchor,$drawWidth,$drawHeight))
        }
        $g.Dispose()
        $frame.Save((Join-Path $folder "$kind-$tick.gif"),[Drawing.Imaging.ImageFormat]::Gif)
        $frame.Dispose()
    }
    foreach ($cell in $cells) { $cell.Dispose() }
}
