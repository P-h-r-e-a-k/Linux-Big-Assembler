# The snowboarding Twinsen's nine animations (LBA1 entity 74's: 0, 1, 7, 8, 106, 110, 188, 189, 190) made keyframe by keyframe in the
# Character maker through its interface alone -- the final values: times in milliseconds, the jump's four keyframes in the air. The stance:
# side-on to the board (bone 1 turned 60 degrees), leaning forward (bone 3), arms out (bones 4 and 6), looking down the board (head, bone 14);
# the riding ones move 666 a keyframe of 200 ms, as LBA1's do.
. $PSScriptRoot\charlib.ps1
CMTab "Animations"
function Status() { (ById "CharacterMakerStatus" (CM)).Current.Name }
function NewAnim($g, $name) {
  CMSet "Animation number" $g; CMSet "Animation name" $name; CMSet "Copy of game animation" ""
  $s = CMPress "New animation"; if ($s -notlike "Animation $g*") { throw "anim $g : $s" }
}
function Key($i) { CMPick "Keyframes" $i }
function AddKey() { CMPress "Add a keyframe (a copy of this one)" | Out-Null }
# the keyframe chosen: its time, how far it moves (forward is +Z, up +Y) and whether it leaves the ground
function Timing($ms, $mz, $my = 0, $air = $false) {
  CMSet "Milliseconds" $ms; CMSet "Move X" 0; CMSet "Move Y" $my; CMSet "Move Z" $mz
  Toggle (ByNameType "Leaves the ground" ([System.Windows.Automation.ControlType]::CheckBox) (CM)) $air
  Invoke ((CM).FindAll($TS::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) | Where-Object { $_.Current.Name -eq "Set" -and $_.Current.HelpText -like "The keyframe's time*" } | Select-Object -First 1)
}
function B($b, $x, $y, $z) { CMSet "Pose bone" $b; CMSet "Turn X" $x; CMSet "Turn Y" $y; CMSet "Turn Z" $z; CMPress "Set the bone" | Out-Null }
function BAll($b, $x, $y, $z) { CMSet "Pose bone" $b; CMSet "Turn X" $x; CMSet "Turn Y" $y; CMSet "Turn Z" $z; CMPress "Same in every keyframe" | Out-Null }
# the stance, in every keyframe of the animation chosen; `lean` the torso, `arms` how far out
function Stance($lean = 15, $arms = 40, $side = -60) { BAll 1 0 $side 0; BAll 3 $lean 0 0; BAll 4 0 0 $arms; BAll 6 0 0 (-$arms); BAll 14 0 45 0 }

# (animation 0 is there: the still one Save into the map made; its name and stance)
CMPick "Animations" 0
CMSet "Animation name" "Standing on the board"; CMPress "Set number and name" | Out-Null
Key 0; Timing 1000 0; Stance

NewAnim 1 "Pushing off"
Stance; AddKey; Key 1; Timing 500 300; B 3 25 0 0; B 4 0 0 30; B 6 0 0 -30
Key 0; Timing 500 300

NewAnim 190 "Riding straight"
Stance 20 40; AddKey; AddKey
Key 0; Timing 200 400
Key 1; Timing 200 666; B 3 25 0 0; B 4 0 0 45; B 6 0 0 -45
Key 2; Timing 200 666
CMPick "Keyframes" 1; CMPress "Loop back to it" | Out-Null

NewAnim 188 "Turning left"
Stance 20 40; AddKey
Key 0; Timing 200 666; BAll 1 0 -60 15; BAll 14 0 45 -10
Key 1; Timing 200 666; B 4 0 0 55; B 6 0 0 -25

NewAnim 189 "Turning right"
Stance 20 40; AddKey
Key 0; Timing 200 666; BAll 1 0 -60 -15; BAll 14 0 45 10
Key 1; Timing 200 666; B 4 0 0 25; B 6 0 0 -55

# the jump: a crouch on the ground, then four keyframes in the air rising and falling as he goes on (over the cliffs the runs drop off)
NewAnim 106 "Jumping"
Stance 20 40; AddKey; AddKey; AddKey
Key 0; Timing 120 400; B 3 35 0 0
Key 1; Timing 160 700 300 $true; B 3 5 0 0; B 4 0 0 75; B 6 0 0 -75
Key 2; Timing 160 700 100 $true; B 3 10 0 0; B 4 0 0 65; B 6 0 0 -65
Key 3; Timing 160 700 -100 $true; B 3 35 0 0; B 4 0 0 35; B 6 0 0 -35
AddKey; Key 4; Timing 160 700 -300 $true
CMPick "Keyframes" 4; CMPress "Loop back to it" | Out-Null

NewAnim 110 "Stopping"
Stance -5 55 -80; AddKey; AddKey
Key 0; Timing 200 400; B 3 -10 0 0
Key 1; Timing 300 150; B 3 -15 0 0; B 4 0 0 60; B 6 0 0 -60
Key 2; Timing 300 0; B 3 5 0 0; B 4 0 0 30; B 6 0 0 -30
CMPick "Keyframes" 2; CMPress "Loop back to it" | Out-Null

NewAnim 7 "Falling"
Stance -20 80; AddKey
Key 0; Timing 160 0
Key 1; Timing 160 0; B 4 0 0 95; B 6 0 0 -95; B 3 -30 0 0

NewAnim 8 "Landing"
Stance 35 30; AddKey
Key 0; Timing 120 0
Key 1; Timing 200 0; B 3 15 0 0; B 4 0 0 40; B 6 0 0 -40
CMPick "Keyframes" 1; CMPress "Loop back to it" | Out-Null

CMPress "Save into the map"
CMShot "10_rider_anims"
