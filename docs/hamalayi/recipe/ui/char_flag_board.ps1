# LBA1's slalom flag (entity 73: bodies 0, 17, 48, 49 -- red, green, blue, light blue -- and a waving animation) and its snowboard to pick
# up (entity 72: the board standing on its end) made in the Character maker through its interface alone, from nothing.
. $PSScriptRoot\charlib.ps1
function NewCharacter($name) {
  $combo = ByNameType "Character" ([System.Windows.Automation.ControlType]::ComboBox) (CM)
  $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 300
  $item = $combo.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem))) | Where-Object { $_.Current.Name -like "*a new character*" } | Select-Object -First 1
  SelectItem $item; $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
  CMPress "Open it" | Out-Null
  CMSet "Character name" $name
  CMTab "Body"; CMPress "Start empty" | Out-Null
}
function Part($shape, $pt, $ox, $oy, $oz, $sx, $sy, $sz, $turn, $col) {
  $s = ByNameType "Shape" ([System.Windows.Automation.ControlType]::ComboBox) (CM)
  $s.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 200
  $it = $s.FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem))) | Where-Object { $_.Current.Name -like "$shape*" } | Select-Object -First 1
  SelectItem $it; $s.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
  CMSet "From point" $pt; CMSet "Offset X" $ox; CMSet "Offset Y" $oy; CMSet "Offset Z" $oz
  CMSet "Size X" $sx; CMSet "Size Y" $sy; CMSet "Size Z" $sz; CMSet "Turn" $turn; CMSet "Colour" $col
  CMPress "Add the part"
}
function SetButton($help) { Invoke ((CM).FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) | Where-Object { $_.Current.Name -eq "Set" -and $_.Current.HelpText -like "$help*" } | Select-Object -First 1) }

# ---- the flag: a brown pole (bone 1, from the point at its foot), a pennant (bone 2) hung from the pole's top corner (point 7: the box's
# corner up and to the front: its points go 0-3 at its foot, 4-7 at its top -- see the Points list)
NewCharacter "Slalom flag"
Part "Box" 0 0 750 0 40 1500 40 0 16
Part "Flat triangle" 7 250 -150 -20 500 300 0 0 64
CMSet "Body number" 0; CMSet "Body name" "Red flag"; SetButton "Gives the body chosen"
foreach ($f in @(@(17, "Green flag", 128), @(48, "Blue flag", 192), @(49, "Light blue flag", 160))) {
  CMSet "Body number" $f[0]; CMSet "Body name" $f[1]; CMPress "New body (a copy of this one)" | Out-Null
  CMSet "Colour" $f[2]; CMSet "Recolour bone" 2; CMPress "Recolour it" | Out-Null
}
CMTab "Animations"
CMSet "Animation number" 0; CMSet "Animation name" "Waving"; CMSet "Copy of game animation" ""; CMPress "New animation" | Out-Null
CMPress "Add a keyframe (a copy of this one)" | Out-Null
CMPick "Keyframes" 0; CMSet "Milliseconds" 300; SetButton "The keyframe's time"
CMSet "Pose bone" 2; CMSet "Turn X" 0; CMSet "Turn Y" 12; CMSet "Turn Z" 0; CMPress "Set the bone" | Out-Null
CMPick "Keyframes" 1; CMSet "Milliseconds" 300; SetButton "The keyframe's time"
CMSet "Pose bone" 2; CMSet "Turn X" 0; CMSet "Turn Y" -12; CMSet "Turn Z" 0; CMPress "Set the bone" | Out-Null
CMPress "Save into the map"
CMShot "10_flag"

# ---- the snowboard to pick up: the board on its end (red, its edge green), two bindings across it
NewCharacter "Snowboard"
Part "Box" 0 0 500 0 280 1000 50 0 64
Part "Box" 0 -145 500 0 12 1000 56 0 128
Part "Box" 0 0 650 30 220 70 16 0 48
Part "Box" 0 0 350 30 220 70 16 0 48
CMSet "Body name" "Snowboard"; SetButton "Gives the body chosen"
CMPress "Save into the map"
CMShot "10_snowboard"
Menu File "Save map"; Start-Sleep 6
"saved"
