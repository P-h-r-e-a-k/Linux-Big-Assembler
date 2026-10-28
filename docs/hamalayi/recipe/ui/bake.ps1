# Re-bakes the open map's light through the terrain editor's "Baked light and shadows" section: Gain and Offset typed, Bake all light.
param([string]$Gain = "8", [string]$Offset = "5")
. $PSScriptRoot\lib.ps1
$root = Root
$Button = [System.Windows.Automation.ControlType]::Button
$h = ByNameType "Baked light and shadows" $Button $root
if ($h.Current.HelpText -like "closed*") { Invoke $h; Start-Sleep -Milliseconds 500 }
SetText (EditBox "Gain" $root) $Gain
SetText (EditBox "Offset" $root) $Offset
Invoke (ByNameType "Bake all light" $Button $root)
Start-Sleep 2
Snap "06_baked"
$s =$root.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) | Where-Object { $_.Current.Name -eq 'Save *' } | Select-Object -First 1
if ($s) { Invoke $s; Start-Sleep 3 }
Menu File "Save map"; Start-Sleep 4
"baked gain $Gain offset $Offset; saved"
