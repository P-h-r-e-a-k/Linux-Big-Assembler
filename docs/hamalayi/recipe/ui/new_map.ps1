# The ski resort's map made through the interface alone: the editor started on the sandbox, File > New map (an LBA2 island, one cube,
# land at 512, the Desert's look the dialog offers), then File > Save map as… into the editor's Maps folder as skiresort.lbamap -- the file
# every later step opens (Open-SkiResort) and saves into (File > Save map).
. $PSScriptRoot\lib.ps1
Start-Editor | Out-Null
Menu "File" "New map"; Start-Sleep -Seconds 3
$w = Win "New map"; if (-not $w) { throw "no New map window" }
$r = $A::FromHandle($w.H)
SetText (EditBox "Name" $r) "Ski resort"
SetText (EditBox "File name" $r) "SKIRESRT"
SetText (EditBox "Cubes across (X)" $r) "1"
SetText (EditBox "Cubes down (Z)" $r) "1"
SetText (EditBox "Land height" $r) "512"
Start-Sleep -Milliseconds 800; Shot $w.H "01_new_map" | Out-Null
Invoke ($r.FindFirst($TS::Descendants, (Cond $A::NameProperty "Create"))); Start-Sleep -Seconds 15
Menu "File" "Save map as"; Start-Sleep -Seconds 2
FileDialog "Save the map" "$Global:AppDir\Maps\skiresort.lbamap"; Start-Sleep -Seconds 6
(Main).Title
Snap "01_map_created"
