$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$refs=@('Shop/shop-sample.png','Select Character/Select characters_sample.png','MENU/Menu_sample.png','Select Character/Character_color ver.png','QUEST/avatar_shepherd_only.png','QUEST/avatar_bear_only.png','QUEST/avatar_sheep only.png','QUEST/avatar_bunny only.png','QUEST/avatar_guineapig_only.png','Shop/attachted/shop owner.png','Character sheet/bird_blue.png','Character sheet/bird_red.png','Character sheet/bird_yellow.png')
$output=Join-Path $root 'Docs/Art/Previews/TownInteriors'
[IO.Directory]::CreateDirectory($output)|Out-Null
$canvas=[Drawing.Bitmap]::new(1600,1360);$g=[Drawing.Graphics]::FromImage($canvas);$font=[Drawing.Font]::new('Segoe UI',12)
try {
 $g.Clear([Drawing.Color]::FromArgb(248,230,192))
 for($i=0;$i -lt $refs.Count;$i++) {
  $im=[Drawing.Bitmap]::new((Join-Path $root ('Assets/Bamao/BamaoUIPack/Sprites/'+$refs[$i])))
  try {$s=[Math]::Min(380/$im.Width,285/$im.Height);$x=($i%4)*400;$y=[Math]::Floor($i/4)*340;$g.DrawImage($im,[Drawing.Rectangle]::new($x+(400-$im.Width*$s)/2,$y,$im.Width*$s,$im.Height*$s));$g.DrawString([IO.Path]::GetFileName($refs[$i]),$font,[Drawing.Brushes]::SaddleBrown,$x+6,$y+298)}finally{$im.Dispose()}
 }
 $canvas.Save((Join-Path $output 'BamaoReferenceBoard.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$g.Dispose();$font.Dispose();$canvas.Dispose()}
