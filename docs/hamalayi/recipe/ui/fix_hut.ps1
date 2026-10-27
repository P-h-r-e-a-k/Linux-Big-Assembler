# The hut's 3 x 3 block of ground (LBA1 roofs the hut with snow-coded bricks, so the ground reference took it for ground) flattened back to
# the snow round it, and its cells and the ring round them painted with their LBA1 ground's tiles -- through the terrain editor's interface.
. $PSScriptRoot\lib.ps1
$fix = Get-Content $PSScriptRoot\..\plan\hut_fix.json -Raw | ConvertFrom-Json
$root = Root
$Button = [System.Windows.Automation.ControlType]::Button
function V($e, $s) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$s) }
SelectItem (ById "BuildTerrainRadio"); Start-Sleep 1
SelectItem (ByName "Flatten to level" $root)
Toggle (ByName "Square brush (whole cells, full strength)" $root) $true
SetRange (ByNameType "Radius" ([System.Windows.Automation.ControlType]::Slider) $root) 0
SetRange (ByNameType "Strength" ([System.Windows.Automation.ControlType]::Slider) $root) 100
V (EditBox "Level" $root) $fix.flatten.level
$vx = EditBox "Vertex X" $root; $vz = EditBox "Vertex Z" $root
$apply = ByNameType "Apply the tool here" $Button $root; $stroke = ByNameType "Stroke to here" $Button $root
for ($z = $fix.flatten.z0; $z -le $fix.flatten.z1; $z++) {
  V $vx (448 + $fix.flatten.x0); V $vz (448 + $z); Invoke $apply
  V $vx (448 + $fix.flatten.x1); Invoke $stroke
}
"flattened"
$atlasHeader = ByNameType "Ground atlas" $Button $root
if ($atlasHeader.Current.HelpText -like "closed*") { Invoke $atlasHeader; Start-Sleep -Milliseconds 500 }
$tx = EditBox "Tile X" $root; $ty = EditBox "Tile Y" $root; $tsize = EditBox "Tile size" $root
$choose = ByNameType "Choose this tile" $Button $root
Toggle (ByName "Stand the tile up the slope (cliffs)" $root) $false
foreach ($c in $fix.cells) {
  V $tx $c.tile[0]; V $ty $c.tile[1]; V $tsize 32; Invoke $choose
  SelectItem (ByName "Paint atlas tile" $root)
  V $vx (448 + $c.x); V $vz (448 + $c.z); Invoke $apply
}
"painted $($fix.cells.Count) cells"
Snap "04_hut"
SaveAll
