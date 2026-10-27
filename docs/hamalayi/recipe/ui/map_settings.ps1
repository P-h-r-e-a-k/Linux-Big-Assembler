# File > Map settings…: the island's cliffs made ledges Twinsen can walk or ride off (as LBA1's), its weather left clear; OK, then the map
# saved -- through the interface alone.
. $PSScriptRoot\lib.ps1
$Button = [System.Windows.Automation.ControlType]::Button
$ComboBox = [System.Windows.Automation.ControlType]::ComboBox
function Choose($combo, $like) {
  $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 300
  SelectItem ($combo.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem))) | Where-Object { $_.Current.Name -like $like } | Select-Object -First 1)
  $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
}
Menu "File" "Map settings"
$w = $null; for ($i = 0; $i -lt 30 -and -not $w; $i++) { Start-Sleep -Milliseconds 200; $w = Win "Map settings" }
if (-not $w) { throw "no Map settings window" }
Start-Sleep 1
$r = $A::FromHandle($w.H)
Choose (ByNameType "Weather" $ComboBox $r) "Clear*"
Choose (ByNameType "Cliffs" $ComboBox $r) "Ledges to walk or ride off*"
Start-Sleep -Milliseconds 500; Shot $w.H "08_map_settings" | Out-Null
Invoke (ByNameType "OK" $Button $r); Start-Sleep 1
Menu File "Save map"; Start-Sleep 6
"saved"
