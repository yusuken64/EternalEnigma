$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$previewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Docs/Art/Previews/PaintedEnvironment'))
$biomes = @('Grassland','Desert','Water','Mountain','Forest','Tundra','Marsh','Volcanic')
foreach ($stage in @('Before','After')) {
    foreach ($group in @('Dungeon','Town','Overworld')) {
        $columns = if ($group -eq 'Dungeon') { 4 } else { 1 }
        $canvas = [Drawing.Bitmap]::new(320*$columns,224*8)
        $graphics = [Drawing.Graphics]::FromImage($canvas)
        $graphics.Clear([Drawing.Color]::FromArgb(24,29,34))
        $font = [Drawing.Font]::new('Segoe UI',10)
        try {
            for ($row=0; $row -lt 8; $row++) {
                for ($col=0; $col -lt $columns; $col++) {
                    $biome = $biomes[$row]
                    $environment = if ($col -lt 2) {'Interior'} else {'Outdoor'}
                    $layout = if ($col%2 -eq 0) {'Regular'} else {'Throne'}
                    $name = if ($group -eq 'Dungeon') {"Dungeon_${biome}_${environment}_${layout}"} else {"${group}_${biome}"}
                    $source = [Drawing.Bitmap]::new((Join-Path $previewRoot "$stage/${name}_Gameplay.png"))
                    try { $graphics.DrawImage($source,[Drawing.Rectangle]::new($col*320,$row*224+24,320,200)) }
                    finally { $source.Dispose() }
                    $graphics.DrawString($name,$font,[Drawing.Brushes]::White,$col*320+4,$row*224+3)
                }
            }
            $canvas.Save((Join-Path $previewRoot "${stage}_${group}_ContactSheet.png"),[Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $font.Dispose(); $graphics.Dispose(); $canvas.Dispose() }
    }
}

