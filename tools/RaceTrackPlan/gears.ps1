# Headless: Twinsen gets into the buggy on the start line (scene 67), holds the throttle, and shifts gear at set ticks.
# Prints the speed (world units per second) every 10 ticks, from the traced positions.
param([string]$game = 'E:\dump\LBA2RaceTrackBuild\Game', [string]$car = '', [string]$keys = '', [int]$ticks = 600, [string]$drive = 'up 500', [string]$shot = '', [string]$tp = '', [string]$extra = '')
$eng = 'E:\dump\LBAAssembler\native\lba2-classic-community\out\build\windows_ucrt64_static\SOURCES\lba2cc.exe'
$user = "E:\dump\TEMP\race\user_$([DateTime]::Now.ToString('yyyyMMddHHmmssfff'))"
New-Item -ItemType Directory -Force $user | Out-Null
$a = @('--headless','--no-audio','--game-dir',$game,'--user-dir',$user,'--no-autosave','--resolution','640x480','--fixed-dt','20','--exec-at','4','skipmodals 1','--exec-at','6','cube 67')
if ($tp -ne '') { $a += @('--exec-at','30',"teleport $tp") }
$a += @('--exec-at','50','input action 5','--exec-at','80','objtrace 0','--exec-at','85',"input $drive")
foreach ($k in ($keys -split ',' | Where-Object { $_ -ne '' })) { $p = $k -split ':'; $a += @('--exec-at', $p[0], "key $($p[1]) 3") }
foreach ($x in ($extra -split '\|' | Where-Object { $_ -ne '' })) { $i = $x.IndexOf(':'); $a += @('--exec-at', $x.Substring(0, $i), $x.Substring($i + 1)) }
if ($shot -ne '') { $a += @('--exec-at',"$($ticks - 5)","screenshot $shot") }
$a += @('--tick',"$ticks",'--exit')
if ($car -ne '') { $env:LBA2_RACETRACK_FILE = $car } else { Remove-Item Env:LBA2_RACETRACK_FILE -ErrorAction SilentlyContinue }
$ErrorActionPreference = 'Continue'
$out = & $eng $a 2>&1 | ForEach-Object { "$_" }
Remove-Item Env:LBA2_RACETRACK_FILE -ErrorAction SilentlyContinue
$text = $out -join ' '
$pts = New-Object 'System.Collections.Generic.SortedDictionary[int,object]'
foreach ($m in [regex]::Matches($text, 't=(\d+) obj=0 pos=(-?\d+),(-?\d+),(-?\d+) rot=\S+ step=\S+ anim=\d+ frame=\d+ track=\S+ label=\S+ comport=(\d+)')) { $pts[[int]$m.Groups[1].Value] = @([int]$m.Groups[2].Value, [int]$m.Groups[4].Value, [int]$m.Groups[5].Value) }
$line = @(); $lastT = -1; $lastP = $null
foreach ($t in $pts.Keys) {
    if ($lastT -ge 0 -and $t - $lastT -ge 200) { $p = $pts[$t]; $v = [math]::Sqrt(($p[0]-$lastP[0])*($p[0]-$lastP[0]) + ($p[1]-$lastP[1])*($p[1]-$lastP[1])) * 1000 / ($t - $lastT); $line += "$([int]($t/100)/10)s:$([int]$v)$(if ($p[2] -lt 12) {'(walk)'})"; $lastT = $t; $lastP = $p }
    elseif ($lastT -lt 0) { $lastT = $t; $lastP = $pts[$t] }
}
$line -join ' '
if ($shot -ne '') { $s = Get-ChildItem "$user\save\shoot\shot_*.png" | Select-Object -Last 1; if ($s) { Copy-Item $s.FullName $shot -Force; "shot $shot" } }
