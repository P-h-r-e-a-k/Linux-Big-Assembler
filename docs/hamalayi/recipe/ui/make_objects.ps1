# Makes scene 96's objects in the Object maker and places their copies -- through the interface alone (object_plan.json the plan):
#  Build > Buildings and decor > Make an object…; for each object its design opened (Open design…), Add it to the island; then each
#  copy: its vertex typed under Brush at a vertex and Apply the tool here (Add object), and its exact place typed into the Selected
#  object's X, Y, Z and Apply.
param([string[]]$Only = @(), [string[]]$Use = @(), [switch]$NoPlace, [switch]$Type)
. $PSScriptRoot\lib.ps1
$plan = Get-Content $PSScriptRoot\..\plan\object_plan.json -Raw | ConvertFrom-Json
$root = Root
$Button = [System.Windows.Automation.ControlType]::Button
function V($e, $s) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$s) }
SelectItem (ById "BuildDecorRadio"); Start-Sleep 2
New-Item -ItemType Directory -Force $PSScriptRoot\..\objects | Out-Null

$mw = Win "Object maker"
if (-not $mw) {
  Invoke (ByNameType "Make an object…" $Button $root)
  for ($i = 0; $i -lt 40 -and -not $mw; $i++) { Start-Sleep -Milliseconds 200; $mw = Win "Object maker" }
  if (-not $mw) { throw "no Object maker window" }
  Start-Sleep 2
}
$mr = $A::FromHandle($mw.H)
$cx = EditBox "Cell X" $mr; $cl = EditBox "Layer" $mr; $cz = EditBox "Cell Z" $mr; $cb = EditBox "Brick" $mr
$put = ByNameType "Put the brick here" $Button $mr
$again = ByNameType "Start again" $Button $mr
$addIt = ByNameType "Add it to the island" $Button $mr
$status = ById "ObjectMakerStatus" $mr

$vx = EditBox "Vertex X" $root; $vz = EditBox "Vertex Z" $root
$apply = ByNameType "Apply the tool here" $Button $root
$fx = EditBox "X" $root; $fy = EditBox "Y" $root; $fz = EditBox "Z" $root
$applyFields = ByNameType "Apply" $Button $root

$made = @{}
foreach ($o in $plan.objects) {
  if ($Only.Count -and $Only -notcontains $o.name) { continue }
  # (an object made on an earlier run: -Use name=body places that body)
  $use = $Use | Where-Object { $_ -like "$($o.name)=*" } | Select-Object -First 1
  if ($use) {
    $body = [int]($use -split '=')[1]; $made[$o.name] = $body
    V (EditBox "Object body" $root) $body
    SelectItem (ByName "Add object" $root)
    "$($o.name): body $body (made before)"
  } else {
  # (the finished design opened -- objects\<name>.lbaobject, the hut's roof already made whole; -Type types object_plan.json's bricks
  # into the cells instead, as the first run did: then the hut's two cells of LBA1's blank brick 222 (0,8,0 and 0,8,1) need 7870 and 7880)
  if ($Type) {
    Invoke $again
    foreach ($c in $o.cells) { V $cx $c[0]; V $cl $c[1]; V $cz $c[2]; V $cb $c[3]; Invoke $put }
  } else {
    Invoke (ByNameType "Open design…" $Button $mr); Start-Sleep -Milliseconds 800
    FileDialog "Open an object's design" (Resolve-Path "$PSScriptRoot\..\objects\$($o.name).lbaobject").Path; Start-Sleep -Milliseconds 800
  }
  Invoke $addIt; Start-Sleep 2
  $said = if ($status) { $status.Current.Name } else { ($root.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Text))) | Where-Object { $_.Current.Name -like "Body * added to *" -or $_.Current.Name -like "The object*" -or $_.Current.Name -like "LBA1 has no*" } | Select-Object -First 1).Current.Name }
  if ($said -notmatch "^Body (\d+) added") { throw "$($o.name): $said" }
  $body = [int]$Matches[1]; $made[$o.name] = $body
  "$($o.name): $said"
  if ($o.name -eq "hut") { Start-Sleep -Milliseconds 800; Shot $mw.H "07_object_maker_hut" | Out-Null }
  }
  if ($NoPlace) { continue }
  foreach ($p in $o.places) {
    # (Objects follow the ground unticked; a place on the cube's far edge, X or Z 32512, added from vertex 511: 512 is outside the cube)
    Toggle (ByName "Objects follow the ground" $root) $false
    V $vx ([math]::Min(511, 448 + [math]::Round($p.X / 512))); V $vz ([math]::Min(511, 448 + [math]::Round($p.Z / 512))); Invoke $apply
    Start-Sleep -Milliseconds 200
    V $fx $p.X; V $fy $p.Y; V $fz $p.Z; Invoke $applyFields
  }
  "  placed $($o.places.Count)"
}
$made | ConvertTo-Json
Snap "07_objects"
$s = $root.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty $Button)) | Where-Object { $_.Current.Name -eq 'Save *' } | Select-Object -First 1
if ($s) { Invoke $s; Start-Sleep 3 }
Menu File "Save map"; Start-Sleep 6
"saved"
