namespace LBAAssembler.Terrain;

// The laser cars' bolts: the game's own (the user, 2026-10-08: "it looks like we're not using the laser from in-game, let's reuse the
// one the game naturally fires, Twinsen can only normally fire green, but there's an animation for firing red lasers as it's used by
// enemies"). OBJFIX.HQR 63 is the bolt Twinsen's laser pistol fires (his entities' animation 38, F_THROW_OBJ_3D: a thin prism 560 long,
// the green ramp's 137); 72 the red bolt the enemies' guns fire (entities 31, 51 and 199: a spike 800 long, the red ramp's 79). The
// race-track mode flies them out of the front of the car (RACEMOD.CPP laser_models=, drive_laser=). Until then the build appended a
// bigger green bolt and a red one of its own to OBJFIX.HQR.
internal static class RaceTrackLasers
{
    public const int Green = 63, Red = 72;

    // The two bolts' models, and a line for the log. (Nothing to add to the game folder: they are the game's.)
    public static (int Green, int Red, string Log) Install(string gameDirectory) =>
        (Green, Red, $"the laser cars' bolts: the game's own, OBJFIX.HQR {Green} (Twinsen's laser pistol, green) and {Red} (the enemies' guns, red)");
}
