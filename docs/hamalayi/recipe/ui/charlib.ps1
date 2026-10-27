# The Character maker driven through its interface: its fields set by name, its buttons pressed by name, its status read back.
. $PSScriptRoot\lib.ps1
function CM() { $w = AppWindows | Where-Object { $_.Title -like "*Character maker*" } | Select-Object -First 1; if (-not $w) { throw "no Character maker" }; $A::FromHandle($w.H) }
function CMWin() { AppWindows | Where-Object { $_.Title -like "*Character maker*" } | Select-Object -First 1 }
function CMSet($name, $value) {
  $e = ByNameType $name ([System.Windows.Automation.ControlType]::Edit) (CM)
  if (-not $e) { throw "no field $name" }
  $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$value)
}
function CMPress($name) {
  $b = ByNameType $name ([System.Windows.Automation.ControlType]::Button) (CM)
  if (-not $b) { throw "no button $name" }
  Invoke $b; Start-Sleep -Milliseconds 150
  $s = (ById "CharacterMakerStatus" (CM)).Current.Name
  return $s
}
function CMTab($name) { SelectItem (ByNameType $name ([System.Windows.Automation.ControlType]::TabItem) (CM)); Start-Sleep -Milliseconds 300 }
function CMPick($list, $index) {
  $l = ByNameType $list ([System.Windows.Automation.ControlType]::List) (CM)
  $items = $l.FindAll($TS::Children, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))
  SelectItem $items[$index]; Start-Sleep -Milliseconds 150
}
function CMView($degrees) { SetRange (ByNameType "Turn the view" ([System.Windows.Automation.ControlType]::Slider) (CM)) $degrees; Start-Sleep -Milliseconds 300 }
function CMShot($name) { Shot (CMWin).H $name }
# (the Character maker opened if it isn't: Build > Actors and zones > Help and other editors > Character maker...)
if (-not (CMWin)) {
  SelectItem (ById "BuildSceneRadio"); Start-Sleep 2
  OpenOtherEditors
  Invoke (ById "BuildCharacterButton")
  for ($i = 0; $i -lt 40 -and -not (CMWin); $i++) { Start-Sleep -Milliseconds 250 }
  Start-Sleep 2
}
