# Textures scene 96's ground in the open map's terrain editor through its interface alone (tile_plan.json the plan):
#  1. each atlas slot: Tile X / Y / size typed, Choose this tile, Put an LBA1 brick here... (the brick's top, or a cliff's two sides);
#  2. each slot's cells painted with Paint atlas tile -- the square brush at radius 0, a stroke along each run of a row; the cliff tiles
#     stood up the slope;
#  3. the land's game code set to none (one big square dab), the sea's corner cells left water.
param([switch]$SkipAtlas, [switch]$SkipPaint)
. $PSScriptRoot\lib.ps1
$plan = Get-Content $PSScriptRoot\..\plan\tile_plan.json -Raw | ConvertFrom-Json
$root = Root
$Button = [System.Windows.Automation.ControlType]::Button
function V($e, $s) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$s) }
# (the atlas section open)
$atlasHeader = ByNameType "Ground atlas" $Button $root
if ($atlasHeader.Current.HelpText -like "closed*") { Invoke $atlasHeader; Start-Sleep -Milliseconds 500 }
$tx = EditBox "Tile X" $root; $ty = EditBox "Tile Y" $root; $tsize = EditBox "Tile size" $root
$choose = ByNameType "Choose this tile" $Button $root
$putBrick = ByNameType "Put an LBA1 brick here…" $Button $root
$slopeBox = ByName "Stand the tile up the slope (cliffs)" $root
function ChooseTile($x, $y) { V $tx $x; V $ty $y; V $tsize 32; Invoke $choose }

if (-not $SkipAtlas) {
  foreach ($e in $plan.slots) {
    ChooseTile $e.x $e.y
    Invoke $putBrick
    $d = $null; for ($i = 0; $i -lt 40 -and -not $d; $i++) { Start-Sleep -Milliseconds 150; $d = Win "Put an LBA1 brick in the ground atlas" }
    if (-not $d) { throw "no brick dialog for slot $($e.slot)" }
    $dr = $A::FromHandle($d.H)
    $face = ByNameType "Face" ([System.Windows.Automation.ControlType]::ComboBox) $dr
    $items = $face.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))
    if ($e.cliff) {
      V (EditBox "Brick number" $dr) $e.cliff[0]
      $face.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 200
      $items = $face.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))
      SelectItem $items[3]; $face.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
      V (EditBox "Brick under it" $dr) $e.cliff[1]
    } else { V (EditBox "Brick number" $dr) $e.brick }
    Start-Sleep -Milliseconds 200
    if ($e.slot -eq 0 -or $e.slot -eq 4) { Start-Sleep -Milliseconds 600; Shot $d.H ("03_put_brick_slot{0}" -f $e.slot) | Out-Null }
    Invoke (ByNameType "Put it in" $Button $dr)
    for ($i = 0; $i -lt 40 -and (Win "Put an LBA1 brick in the ground atlas"); $i++) { Start-Sleep -Milliseconds 100 }
  }
  "atlas: $($plan.slots.Count) tiles put in"
  Snap "03_atlas"
}

if (-not $SkipPaint) {
  SelectItem (ByName "Paint atlas tile" $root)
  Toggle (ByName "Square brush (whole cells, full strength)" $root) $true
  SetRange (ByNameType "Radius" ([System.Windows.Automation.ControlType]::Slider) $root) 0
  $vx = EditBox "Vertex X" $root; $vz = EditBox "Vertex Z" $root
  $apply = ByNameType "Apply the tool here" $Button $root; $stroke = ByNameType "Stroke to here" $Button $root
  $n = 0; $watch = [Diagnostics.Stopwatch]::StartNew()
  foreach ($e in $plan.slots) {
    ChooseTile $e.x $e.y
    SelectItem (ByName "Paint atlas tile" $root)
    Toggle $slopeBox ([bool]$e.cliff)
    foreach ($r in $e.runs) {
      V $vx (448 + $r.x0); V $vz (448 + $r.z); Invoke $apply
      if ($r.x1 -ne $r.x0) { V $vx (448 + $r.x1); Invoke $stroke }
      $n++
    }
  }
  "painted: $n runs in $([int]$watch.Elapsed.TotalSeconds) s"
  # the game code: none on the land (the new map's beach was sea), water left on the sea's corner
  SelectItem (ByName "Paint game code" $root)
  $code = ByNameType "Game code" ([System.Windows.Automation.ControlType]::ComboBox) $root
  $code.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 300
  SelectItem (($code.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem))))[0]); $code.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
  SetRange (ByNameType "Radius" ([System.Windows.Automation.ControlType]::Slider) $root) 32
  V $vx 480; V $vz 480; Invoke $apply
  SetRange (ByNameType "Radius" ([System.Windows.Automation.ControlType]::Slider) $root) 0
  $code.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 300
  SelectItem (($code.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem))))[1]); $code.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
  foreach ($c in $plan.sea_cells) { V $vx (448 + $c[0]); V $vz (448 + $c[1]); Invoke $apply }
  "game codes painted"
  Snap "03_painted"
  SaveAll
}
