Add-Type -AssemblyName System.Drawing
function RR($x,$y,$w,$h,$r){ $p=New-Object Drawing.Drawing2D.GraphicsPath; $d=$r*2
 $p.AddArc($x,$y,$d,$d,180,90);$p.AddArc($x+$w-$d,$y,$d,$d,270,90);$p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90);$p.AddArc($x,$y+$h-$d,$d,$d,90,90);$p.CloseFigure();$p }
$gold=[Drawing.Color]::FromArgb(212,175,55); $goldL=[Drawing.Color]::FromArgb(242,214,117); $goldD=[Drawing.Color]::FromArgb(140,109,31); $blk=[Drawing.Color]::FromArgb(10,10,10)
function NewBmp($s){ $b=New-Object Drawing.Bitmap $s,$s; $g=[Drawing.Graphics]::FromImage($b); $g.SmoothingMode='AntiAlias'; $g.Clear([Drawing.Color]::Transparent); ,@($b,$g) }
function DrawChest($g,$s,$bg){
  if($bg){ $p=RR 4 4 ($s-8) ($s-8) ($s*0.18); $g.FillPath((New-Object Drawing.SolidBrush $blk),$p); $g.DrawPath((New-Object Drawing.Pen $gold,($s*0.025)),$p) }
  $m=$s*0.2; $w=$s-2*$m; $top=$s*0.30; $mid=$s*0.50; $bot=$s*0.76
  $lid=New-Object Drawing.Drawing2D.GraphicsPath; $lid.AddArc($m,$top,$w,($mid-$top)*2,180,180); $lid.CloseFigure()
  $br=New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.PointF]::new(0,$top)),([Drawing.PointF]::new(0,$bot)),$goldL,$goldD
  $g.FillPath($br,$lid); $g.FillRectangle($br,$m,$mid,$w,$bot-$mid)
  $pen=New-Object Drawing.Pen $blk,($s*0.025); $g.DrawLine($pen,$m,$mid,$m+$w,$mid)
  $g.DrawLine($pen,$m+$w*0.25,$top+($mid-$top)*0.1,$m+$w*0.25,$bot); $g.DrawLine($pen,$m+$w*0.75,$top+($mid-$top)*0.1,$m+$w*0.75,$bot)
  $g.FillEllipse((New-Object Drawing.SolidBrush $blk),$s/2-$s*0.06,$mid-$s*0.05,$s*0.12,$s*0.12)
}
# logo (chest on black rounded square) + chest (no bg)
$r=NewBmp 256; DrawChest $r[1] 256 $true; $r[0].Save("Assets\logo.png",[Drawing.Imaging.ImageFormat]::Png)
$r2=NewBmp 128; DrawChest $r2[1] 128 $false; $r2[0].Save("Assets\chest.png",[Drawing.Imaging.ImageFormat]::Png)
# pirate-hour placeholder: gold clock
$r3=NewBmp 128; $g=$r3[1]; $pen=New-Object Drawing.Pen $gold,8; $g.DrawEllipse($pen,14,14,100,100)
$p2=New-Object Drawing.Pen $goldL,7; $p2.StartCap='Round';$p2.EndCap='Round'; $g.DrawLine($p2,64,64,64,32); $g.DrawLine($p2,64,64,90,76)
$r3[0].Save("Assets\pirate.png",[Drawing.Imaging.ImageFormat]::Png)
# ico (PNG-embedded)
$ms=New-Object IO.MemoryStream; $r[0].Save($ms,[Drawing.Imaging.ImageFormat]::Png); $png=$ms.ToArray()
$fs=[IO.File]::Create("Assets\app.ico"); $bw=New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0);$bw.Write([uint16]1);$bw.Write([uint16]1);$bw.Write([byte]0);$bw.Write([byte]0);$bw.Write([byte]0);$bw.Write([byte]0)
$bw.Write([uint16]1);$bw.Write([uint16]32);$bw.Write([uint32]$png.Length);$bw.Write([uint32]22);$bw.Write($png);$bw.Close()
