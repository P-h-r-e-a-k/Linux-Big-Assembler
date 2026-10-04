param([int]$cube = 67, [string]$tp = '', [string]$out = 'E:\dump\TEMP\hshot.png', [int]$wait = 60, [string]$game = 'E:\dump\LBA2RaceTrackBuild\Game', [string]$vars = '', [string]$car = '')
# -car <file>: the engine's race-track mode with that car setup file (RACEMOD.CPP; what Play uses on a folder with a race track built).
# Headless engine screenshot of a cube of the sandbox game (optionally with the hero teleported: "x y z").
# The engine: the freshly built one (native build output) if there is one, else the app's own extracted copy. Always muted, and
# each run gets a user folder of its own.
$built = 'E:\dump\LBAAssembler\native\lba2-classic-community\out\build\windows_ucrt64_static\SOURCES\lba2cc.exe'
$eng = if (Test-Path $built) { $built } else { (Get-ChildItem E:\dump\LBAAssembler\bin\Debug\net10.0-windows\native\lba2cc.*.exe | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }
$user = "E:\dump\TEMP\engine_user_$([DateTime]::Now.ToString('yyyyMMddHHmmssfff'))"
New-Item -ItemType Directory -Force $user | Out-Null
$a = @('--headless','--no-audio','--game-dir',$game,'--user-dir',$user,'--no-autosave','--resolution','640x480','--exec-at','4','skipmodals 1')
if ($vars -ne '') { $a += @('--exec-at','5',$vars) }
$a += @('--exec-at','6',"cube $cube")
if ($tp -ne '') { $a += @('--exec-at','30',"teleport $tp") }
$a += @('--exec-at',"$wait","dumpstate",'--exec-at',"$($wait+1)","screenshot $out",'--tick',"$($wait+30)",'--exit')
if ($car -ne '') { $env:LBA2_RACETRACK_FILE = $car } else { Remove-Item Env:LBA2_RACETRACK_FILE -ErrorAction SilentlyContinue }
$ErrorActionPreference = 'Continue'
$log = & $eng $a 2>&1 | ForEach-Object { "$_" }
Remove-Item Env:LBA2_RACETRACK_FILE -ErrorAction SilentlyContinue
$shot = (Get-ChildItem "$user\save\shoot\shot_*.png" | Sort-Object LastWriteTime | Select-Object -Last 1)
Copy-Item $shot.FullName $out -Force
$j = Get-ChildItem "$user\save\shoot\state_*.json" | Sort-Object LastWriteTime | Select-Object -Last 1
$d = Get-Content $j.FullName -Raw | ConvertFrom-Json
$d.actors | ForEach-Object { "actor $($_.index): x=$($_.x) y=$($_.y) z=$($_.z) life=$($_.life) body=$($_.body) flags=$($_.flags)" }
"saved $out"
