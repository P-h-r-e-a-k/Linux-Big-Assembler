# The ground under scene 96's tree clumps lowered to LBA1's (ground_fix.json): Flatten to level, the square brush at radius 0, a stroke a
# run of vertices of one level; then the cells round them painted with their tiles (cliffs stood up the slope); then steep ground blocked
# again over the whole island -- through the terrain editor's interface alone.
. $PSScriptRoot\lib.ps1
$fix = Get-Content $PSScriptRoot\..\plan\ground_fix.json -Raw | ConvertFrom-Json
$root = Root
$Button = [System.Windows.Automation.ControlType]::Button
function V($e, $s) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$s) }
SelectItem (ById "BuildTerrainRadio"); Start-Sleep 1
SelectItem (ByName "Flatten to level" $root)
Toggle (ByName "Square brush (whole cells, full strength)" $root) $true
SetRange (ByNameType "Radius" ([System.Windows.Automation.ControlType]::Slider) $root) 0
SetRange (ByNameType "Strength" ([System.Windows.Automation.ControlType]::Slider) $root) 100
$level = EditBox "Level" $root; $vx = EditBox "Vertex X" $root; $vz = EditBox "Vertex Z" $root
$apply = ByNameType "Apply the tool here" $Button $root; $stroke = ByNameType "Stroke to here" $Button $root
$current = $null
foreach ($r in $fix.runs) {
  if ($r.level -ne $current) { V $level $r.level; $current = $r.level }
  V $vx (448 + $r.x0); V $vz (448 + $r.z); Invoke $apply
  if ($r.x1 -ne $r.x0) { V $vx (448 + $r.x1); Invoke $stroke }
}
"lowered: $($fix.runs.Count) runs"
$atlasHeader = ByNameType "Ground atlas" $Button $root
if ($atlasHeader.Current.HelpText -like "closed*") { Invoke $atlasHeader; Start-Sleep -Milliseconds 500 }
$tx = EditBox "Tile X" $root; $ty = EditBox "Tile Y" $root; $tsize = EditBox "Tile size" $root
$choose = ByNameType "Choose this tile" $Button $root
$slope = ByName "Stand the tile up the slope (cliffs)" $root
foreach ($c in $fix.tiles) {
  V $tx $c.tile[0]; V $ty $c.tile[1]; V $tsize 32; Invoke $choose
  SelectItem (ByName "Paint atlas tile" $root)
  Toggle $slope ([bool]$c.cliff)
  V $vx (448 + $c.x); V $vz (448 + $c.z); Invoke $apply
}
"painted: $($fix.tiles.Count) cells"
Snap "05_tree_ground"
SetText (EditBox "Unwalkable angle °" $root) "45"
Invoke (ByNameType "Block steep ground on the whole island" $Button $root); Start-Sleep 2
$s = $root.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) | Where-Object { $_.Current.Name -eq 'Save *' } | Select-Object -First 1
if ($s) { Invoke $s; Start-Sleep 3 }
Menu File "Save map"; Start-Sleep 6
"saved"
