param([string]$game = 'E:\dump\LBA2RaceTrackBuild\Game', [int]$cube = 62, [string]$tp = '14700 4500 9884 1024', [string]$drive = 'up 400', [int]$ticks = 700, [string]$vars = 'vargame 74 3', [int]$every = 1, [string]$shot = '', [string]$car = '')
# -car <file>: the engine's race-track mode with that car setup file (RACEMOD.CPP; what Play uses on a folder with a race track built).
# Headless engine run: enter a cube, teleport Twinsen next to the buggy, get in (action), drive, log the hero's position.
# The engine: the freshly built one (native build output) if there is one, else the app's own extracted copy. Always muted, and
# each run gets a user folder of its own. The game folder defaults to the sandbox copy, never a real install.
$built = 'E:\dump\LBAAssembler\native\lba2-classic-community\out\build\windows_ucrt64_static\SOURCES\lba2cc.exe'
$eng = if (Test-Path $built) { $built } else { (Get-ChildItem E:\dump\LBAAssembler\bin\Debug\net10.0-windows\native\lba2cc.*.exe | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }
$user = "E:\dump\TEMP\engine_user_$([DateTime]::Now.ToString('yyyyMMddHHmmssfff'))"
New-Item -ItemType Directory -Force $user | Out-Null
$a = @('--headless','--no-audio','--game-dir',$game,'--user-dir',$user,'--no-autosave','--resolution','640x480','--exec-at','4','skipmodals 1')
if ($vars -ne '') { $a += @('--exec-at','5',$vars) }
$a += @('--exec-at','6',"cube $cube",'--exec-at','30',"teleport $tp",'--exec-at','50','input action 5','--exec-at','80','objtrace 0','--exec-at','85',"input $drive")
if ($shot -ne '') { $a += @('--exec-at',"$($ticks - 5)","screenshot $shot") }
$a += @('--tick',"$ticks",'--exit')
if ($car -ne '') { $env:LBA2_RACETRACK_FILE = $car } else { Remove-Item Env:LBA2_RACETRACK_FILE -ErrorAction SilentlyContinue }
$ErrorActionPreference = 'Continue'
$out = & $eng $a 2>&1 | ForEach-Object { "$_" }
Remove-Item Env:LBA2_RACETRACK_FILE -ErrorAction SilentlyContinue
$text = $out -join ' '
$prev = ''; $n = 0
foreach ($m in [regex]::Matches($text, 't=(\d+) obj=0 pos=(\S+) rot=(\S+) step=\S+ anim=(\d+) .*?move=(\d+) flags=(\d+)')) {
    $key = $m.Groups[2].Value
    if ($key -ne $prev) { $n++; if ($n % $every -eq 0) { "t=$($m.Groups[1].Value) pos=$key rot=$($m.Groups[3].Value) anim=$($m.Groups[4].Value) move=$($m.Groups[5].Value) flags=$($m.Groups[6].Value)" }; $prev = $key }
}
