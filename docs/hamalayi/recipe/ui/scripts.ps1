# The scene's LBA2 scripts typed into the script window (lba2scripts\actor<N>_life.c / _track.c): each actor chosen in the scene panel's
# Actors list (Build > Actors and zones), its Edit script…, the Life (C) text and the Track (C) text typed in, saved into the map (the
# confirmation answered OK).
param([int[]]$Only = @(), [switch]$SaveLast)
. $PSScriptRoot\lib.ps1
$Button = [System.Windows.Automation.ControlType]::Button
$dir = "$PSScriptRoot\..\scripts"
$root = Root
SelectItem (ById "ModeBuildButton" $root); Start-Sleep 2
Invoke (ById "DockTab_Build" $root); Start-Sleep 1
$radio = ById "BuildSceneRadio" $root; if ($radio) { SelectItem $radio; Start-Sleep 2 }
$se = Root
function SetScript($sw, $text) {
  $box = ById "ScriptTextBox" $sw
  $box.SetFocus()
  $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
  Start-Sleep -Milliseconds 600
}
$actors = Get-ChildItem $dir -Filter "actor*_life.c" | ForEach-Object { [int]($_.Name -replace 'actor(\d+)_life\.c', '$1') } | Sort-Object
foreach ($n in $(if ($Only.Count) { $Only } else { $actors })) {
  SelectItem (ByNameType "Actors" ([System.Windows.Automation.ControlType]::TabItem) $se); Start-Sleep -Milliseconds 300
  $al = ById "SceneActorsList" $se
  SelectItem ($al.FindAll($TS::Children, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))[$n]); Start-Sleep -Milliseconds 400
  $before = @(AppWindows | ForEach-Object { $_.H })
  Invoke (ByNameType "Edit script…" $Button $se)
  $sw = $null
  for ($i = 0; $i -lt 40 -and -not $sw; $i++) { Start-Sleep -Milliseconds 200; $sw = AppWindows | Where-Object { $before -notcontains $_.H -and $_.Title -notlike "LBA Assembler*" } | Select-Object -First 1 }
  if (-not $sw) { throw "no script window for actor $n" }
  Start-Sleep 1
  $swr = $A::FromHandle($sw.H)
  # (the track first: the life script names its labels)
  $track = ""
  if (Test-Path "$dir\actor${n}_track.c") {
    SelectItem (ById "ViewTrackButton" $swr); Start-Sleep -Milliseconds 400
    SetScript $swr (Get-Content -Raw "$dir\actor${n}_track.c")
    $track = (ById "StatusText" $swr).Current.Name
  }
  SelectItem (ById "ViewLifeButton" $swr); Start-Sleep -Milliseconds 400
  SetScript $swr (Get-Content -Raw "$dir\actor${n}_life.c")
  $life = (ById "StatusText" $swr).Current.Name
  "actor $n : life [$life] track [$track]"
  # (-SaveLast: the scripts name one another's -- Twinsen's the rider's and the rider's Twinsen's -- so all typed first, saved together
  # from the last one's window, the others' windows left open with their edits)
  $last = if ($Only.Count) { $Only[-1] } else { $actors[-1] }
  if ($SaveLast -and $n -ne $last) { continue }
  Shot $sw.H "12_script_$n" | Out-Null
  Invoke (ById "SaveButton" $swr); Start-Sleep -Milliseconds 800
  $confirm = AppWindows | Where-Object { $_.Title -like "Save scripts*" } | Select-Object -First 1
  if ($confirm) { [void](DlgButton $confirm.Title 1); Start-Sleep 1 }
  $after = AppWindows | Where-Object { $_.Title -like "Scripts not saved*" } | Select-Object -First 1
  if ($after) { Shot $after.H "script_fail_$n"; throw "actor $n : scripts not saved" }
  "  saved: $((ById "StatusText" $swr).Current.Name)"
  [void][HW]::PostMessage($sw.H, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero); Start-Sleep -Milliseconds 800
}
Menu File "Save map"; Start-Sleep 6
"map saved"
