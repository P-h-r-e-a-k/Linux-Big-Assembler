# The ski resort's lines of dialogue (LBA1's island 6 text, file 9: 0, 23, 36, 37) typed into File > Map text… as the map's own lines
# 1000-1003, saved, and the map saved -- through the interface alone.
. $PSScriptRoot\lib.ps1
$Button = [System.Windows.Automation.ControlType]::Button
function V($e, $s) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue([string]$s) }
$lines = @(
  @(1000, "Good day."),
  @(1001, "I'm looking for the plans for the Teleportation Center."),
  @(1002, "Use your red card to use the skilift."),
  @(1003, "Sorry, I don't know where they are.")
)
Menu "File" "Map text"
$w = $null; for ($i = 0; $i -lt 30 -and -not $w; $i++) { Start-Sleep -Milliseconds 200; $w = AppWindows | Where-Object { $_.Title -like "*Map text*" } | Select-Object -First 1 }
if (-not $w) { throw "no Map text window" }
Start-Sleep 1
$r = $A::FromHandle($w.H)
$number = EditBox "Number" $r; $words = EditBox "Words" $r
$put = ByNameType "Put this line" $Button $r
foreach ($l in $lines) { V $number $l[0]; V $words $l[1]; Invoke $put }
Invoke (ByNameType "Save" $Button $r); Start-Sleep 1
(ById "MapTextStatus" $r).Current.Name
Shot $w.H "09_map_text"
Menu File "Save map"; Start-Sleep 6
