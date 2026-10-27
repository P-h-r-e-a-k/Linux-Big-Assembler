# Helpers for driving the Debug editor (a copy in the sandbox's app folder, sandbox settings) as a user would: UI Automation for controls,
# posted mouse messages for the maps and views (the real cursor never moves), captures of the editor's own windows only.
# Dot-source it: . $PSScriptRoot\lib.ps1
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
if (-not ("HW" -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class HW {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr h, int id);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
"@
}
# (where the sandbox is: set $Global:Sandbox before dot-sourcing to put it elsewhere. It holds app\ -- a copy of the Debug build, its Maps
# folder the map's -- game\, a plain copy of an LBA2 game folder, and settings\. $Global:Lba1Dir is a copy of LBA1's folder, for its bricks.)
if (-not $Global:Sandbox) { $Global:Sandbox = "E:\dump\TEMP\hamalayi" }
if (-not $Global:Lba1Dir) { $Global:Lba1Dir = "E:\dump\TEMP\mappack\lba1" }
$Global:AppDir = "$Global:Sandbox\app"
$Global:Shots = "$Global:Sandbox\ui\shots"
New-Item -ItemType Directory -Force $Global:Shots | Out-Null
$A = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

function AppPid() { (Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -like "$Global:AppDir\LBAAssembler.exe" } | Select-Object -First 1).ProcessId }

function AppWindows($procId = (AppPid)) {
  $list = New-Object System.Collections.ArrayList
  $cb = [HW+EnumProc]{ param($h, $l)
    $p2 = 0; [void][HW]::GetWindowThreadProcessId($h, [ref]$p2)
    if ($p2 -eq $procId -and [HW]::IsWindowVisible($h)) { $sb = New-Object System.Text.StringBuilder 256; [void][HW]::GetWindowText($h, $sb, 256); [void]$list.Add([pscustomobject]@{ H = $h; Title = $sb.ToString() }) }
    return $true }
  [void][HW]::EnumWindows($cb, [IntPtr]::Zero)
  return $list
}

# the main window -- refusing anything that isn't on the sandbox
function Main() {
  $m = AppWindows | Where-Object { $_.Title -like "LBA Assembler*" } | Select-Object -First 1
  if (-not $m) { throw "no editor window" }
  if ($m.Title -notlike "*$Global:Sandbox*") { throw "NOT the sandbox: $($m.Title)" }
  return $m
}
function Root() { $A::FromHandle((Main).H) }
# Build > Actors and zones: the "Help and other editors" expander under the scene panel opened (the Character maker's button is in it)
function OpenOtherEditors() {
  $x = (Root).FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Group))) | Where-Object { $_.Current.Name -like "Help and other editors*" } | Select-Object -First 1
  if (-not $x) { $x = ByName "Help and other editors" }
  if ($x) { try { $x.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 600 } catch {} }
}
# the terrain editor's Save ("Save *" while there are changes), then File > Save map: after every step
function SaveAll() {
  $s = (Root).FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) | Where-Object { $_.Current.Name -eq 'Save *' } | Select-Object -First 1
  if ($s) { Invoke $s; Start-Sleep 3 }
  Menu File "Save map"; Start-Sleep 6
  "saved"
}
# a picture of the main window (into $Global:Shots), to check a step by
function Snap($name) { Start-Sleep -Milliseconds 800; Shot (Main).H $name | Out-Null }
function Win($title) { AppWindows | Where-Object { $_.Title -eq $title } | Select-Object -First 1 }

function Shot($h, $name) {
  if ($h -is [pscustomobject]) { $h = $h.H }
  $r = New-Object HW+RECT; [void][HW]::DwmGetWindowAttribute($h, 9, [ref]$r, 16)
  $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
  $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc(); [void][HW]::PrintWindow($h, $dc, 2); $g.ReleaseHdc($dc); $g.Dispose()
  $bmp.Save("$Global:Shots\$name.png"); $bmp.Dispose()
  "$Global:Shots\$name.png"
}

function Cond($prop, $value) { New-Object System.Windows.Automation.PropertyCondition($prop, $value) }
function ById($id, $root = (Root)) { $root.FindFirst($TS::Descendants, (Cond $A::AutomationIdProperty $id)) }
function ByName($name, $root = (Root)) { $root.FindFirst($TS::Descendants, (Cond $A::NameProperty $name)) }
function AllByType($type, $root = (Root)) { $root.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty $type)) }
function Invoke($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Toggle($e, $on) { $t = $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern); if (($t.Current.ToggleState -eq 'On') -ne $on) { $t.Toggle() } }
function SetText($e, $text) { $e.SetFocus(); $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text) }
function SetRange($e, $v) { $e.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($v) }
function SelectItem($e) { $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }

# a menu path: Menu "File" "New mapâ€¦"
function Menu() {
  # (each item found inside the one opened before it, by its name or the start of it)
  $parent = Root
  for ($i = 0; $i -lt $args.Count; $i++) {
    $items = $parent.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::MenuItem)))
    $want = $args[$i]
    $item = $items | Where-Object { $_.Current.Name -eq $want } | Select-Object -First 1
    if (-not $item) { $item = $items | Where-Object { $_.Current.Name -like "$want*" } | Select-Object -First 1 }
    if (-not $item) { throw "no menu item $($args[$i])" }
    if ($i -lt $args.Count - 1) { $item.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 500; $parent = $item }
    else { Invoke $item }
  }
}

# a dialog's standard button (1 OK, 2 Cancel, 6 Yes, 7 No) -- the WPF MessageBox
function DlgButton($title, $id) { $w = Win $title; if (-not $w) { return $false }; [void][HW]::SendMessage([HW]::GetDlgItem($w.H, $id), 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero); return $true }

# mouse over the main window's client area, posted (no real cursor): screen point (from UIA rectangles) -> client
function ClientOf($h, $sx, $sy) { $p = New-Object HW+POINT; $p.X = [int]$sx; $p.Y = [int]$sy; [void][HW]::ScreenToClient($h, [ref]$p); return $p }
function LParam($p) { [IntPtr](($p.Y -shl 16) -bor ($p.X -band 0xFFFF)) }
function PostMove($h, $sx, $sy, $buttons = 0) { $p = ClientOf $h $sx $sy; [void][HW]::PostMessage($h, 0x0200, [IntPtr]$buttons, (LParam $p)) }
function PostDown($h, $sx, $sy) { $p = ClientOf $h $sx $sy; [void][HW]::PostMessage($h, 0x0201, [IntPtr]1, (LParam $p)) }
function PostUp($h, $sx, $sy) { $p = ClientOf $h $sx $sy; [void][HW]::PostMessage($h, 0x0202, [IntPtr]0, (LParam $p)) }

function Start-Editor($map = $null) {
  if (AppPid) { throw "the editor copy is already running" }
  # (the build copied over the last one -- its Maps folder, the maps saved there, kept)
  New-Item -ItemType Directory -Force $Global:AppDir | Out-Null
  Copy-Item -Recurse -Force "$PSScriptRoot\..\..\..\..\bin\Debug\net10.0-windows\*" $Global:AppDir
  $settings = "$Global:Sandbox\settings"
  New-Item -ItemType Directory -Force $settings | Out-Null
  @{ GameDirectory = "$Global:Sandbox\game"; Lba1Directory = $Global:Lba1Dir; TempDirectory = "$Global:Sandbox\temp"; AppDataDirectory = "$Global:Sandbox\appdata";
     Lba2Audio = @{ Mute = $true; Music = 0; Voices = 0; Effects = 0 }; Lba1Audio = @{ Mute = $true; Music = 0; Voices = 0; Effects = 0 } } | ConvertTo-Json | Out-File -Encoding utf8 "$settings\settings.json"
  Copy-Item -Force "$settings\settings.json" "$Global:AppDir\settings.json"
  $env:LBA2_EDITOR_SETTINGS_DIR = $settings
  $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "$Global:Sandbox\.net"
  $argsList = @(); if ($map) { $argsList = @("--map", $map) }
  if ($argsList.Count) { Start-Process -FilePath "$Global:AppDir\LBAAssembler.exe" -ArgumentList $argsList | Out-Null } else { Start-Process -FilePath "$Global:AppDir\LBAAssembler.exe" | Out-Null }
  for ($i = 0; $i -lt 60; $i++) { Start-Sleep -Seconds 1; $m = AppWindows | Where-Object { $_.Title -like "LBA Assembler*" }; if ($m) { break } }
  Start-Sleep -Seconds 8
  Main
}

function Stop-Editor() { Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -like "$Global:AppDir\*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -Confirm:$false } }


# a control of a type by its accessible name (a label and its box can share the name)
function ByNameType($name, $type, $root = (Root)) { $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.AndCondition((Cond $A::NameProperty $name), (Cond $A::ControlTypeProperty $type)))) }
function EditBox($name, $root = (Root)) { ByNameType $name ([System.Windows.Automation.ControlType]::Edit) $root }

if (-not ("HW2" -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class HW2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
}
"@
}
# a common file dialog's file name typed in (its edit, inside the combo 1148, set by WM_SETTEXT) and its OK button (Open / Save) pressed
function FileDialog($title, $path) {
  $d = Win $title; if (-not $d) { throw "no dialog $title" }
  $script:found = [IntPtr]::Zero
  $cb = [HW2+EnumProc]{ param($h, $l)
    $sb = New-Object System.Text.StringBuilder 64; [void][HW2]::GetClassName($h, $sb, 64)
    if ($sb.ToString() -eq "Edit" -and ([HW2]::GetDlgCtrlID([HW2]::GetParent($h)) -eq 1148 -or [HW2]::GetDlgCtrlID($h) -eq 1001) -and $script:found -eq [IntPtr]::Zero) { $script:found = $h }
    return $true }
  [void][HW2]::EnumChildWindows($d.H, $cb, [IntPtr]::Zero)
  if ($script:found -eq [IntPtr]::Zero) { throw "no file name box in $title" }
  [void][HW2]::SendMessage($script:found, 0x000C, [IntPtr]::Zero, $path)
  Start-Sleep -Milliseconds 300
  [void](DlgButton $title 1)
}

# the editor started on the sandbox with a map opened through File > Open map, in Build mode's terrain editor
function Open-SkiResort() {
  $m = Start-Editor
  Menu "File" "Open map"; Start-Sleep -Seconds 3
  FileDialog "Open a map" "$Global:AppDir\Maps\skiresort.lbamap"; Start-Sleep -Seconds 12
  SelectItem (ById "ModeBuildButton"); Start-Sleep -Seconds 2
  Invoke (ById "DockTab_Build"); Start-Sleep -Seconds 1
  SelectItem (ById "BuildTerrainRadio"); Start-Sleep -Seconds 2
  (Main).Title
}
