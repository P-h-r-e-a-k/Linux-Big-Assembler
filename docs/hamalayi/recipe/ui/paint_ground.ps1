# Paints scene 96's ground in the open map's terrain editor through its interface alone: Flatten to level, the square brush at radius 0,
# full strength, for each height the Level typed in, then each run of vertices of that height along a row: its first vertex typed into
# "Brush at a vertex" and "Apply the tool here", its last typed and "Stroke to here". The heights come from targets.json (the LBA1 reference).
param([int]$From = 0, [int]$Count = 100000)
. $PSScriptRoot\lib.ps1
$t = Get-Content $PSScriptRoot\..\plan\targets.json -Raw | ConvertFrom-Json
$root = Root
# (the controls, found once)
SelectItem (ByName "Flatten to level" $root)
Toggle (ByName "Square brush (whole cells, full strength)" $root) $true
SetRange (ByNameType "Radius" ([System.Windows.Automation.ControlType]::Slider) $root) 0
SetRange (ByNameType "Strength" ([System.Windows.Automation.ControlType]::Slider) $root) 100
Toggle (ByName "Block terrain steeper than the angle below" $root) $true
SetText (EditBox "Unwalkable angle °" $root) "45"
$level = EditBox "Level" $root
$vx = EditBox "Vertex X" $root
$vz = EditBox "Vertex Z" $root
$apply = ByNameType "Apply the tool here" ([System.Windows.Automation.ControlType]::Button) $root
$stroke = ByNameType "Stroke to here" ([System.Windows.Automation.ControlType]::Button) $root
function V($e, $s) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$s) }
$runs = @($t.runs) | Select-Object -Skip $From -First $Count
$current = $null; $n = 0; $watch = [Diagnostics.Stopwatch]::StartNew()
foreach ($r in $runs) {
  if ($r.level -ne $current) { V $level $r.level; $current = $r.level }
  V $vx (448 + $r.x0); V $vz (448 + $r.z); Invoke $apply
  if ($r.x1 -ne $r.x0) { V $vx (448 + $r.x1); Invoke $stroke }
  $n++
  if ($n % 100 -eq 0) { "$n strokes, $([int]$watch.Elapsed.TotalSeconds) s" }
}
"done: $n strokes in $([int]$watch.Elapsed.TotalSeconds) s"
Snap "02_heights"
SaveAll
