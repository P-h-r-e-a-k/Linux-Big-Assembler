# "Twinsen with snowboard" (LBA1's entity 74) made in the Character maker through its interface alone: the game's tunic Twinsen (BODY.HQR
# 1) for its body, a pink snowboard hung from the point at his feet (along his front, as LBA1's), two grey bindings on it.
. $PSScriptRoot\charlib.ps1
CMSet "Game body" 1; CMPress "Start from it"
function Part($pt, $ox, $oy, $oz, $sx, $sy, $sz, $turn, $col) {
  CMSet "From point" $pt; CMSet "Offset X" $ox; CMSet "Offset Y" $oy; CMSet "Offset Z" $oz
  CMSet "Size X" $sx; CMSet "Size Y" $sy; CMSet "Size Z" $sz; CMSet "Turn" $turn; CMSet "Colour" $col
  CMPress "Add the part"
}
Part 0 0 -25 0 260 50 1000 0 70      # the board
Part 0 0 8 230 230 16 70 0 48        # a binding under each foot
Part 0 0 8 -230 230 16 70 0 48
CMSet "Body name" "Twinsen on his snowboard"
Invoke ((CM).FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) | Where-Object { $_.Current.Name -eq "Set" } | Select-Object -First 1)
CMSet "Character name" "Twinsen with snowboard"
CMPress "Save into the map"
CMShot "10_rider_body"
