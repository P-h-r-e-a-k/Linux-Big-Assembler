namespace LBAAssembler;

// Each track's recommended setup (RaceCarSetup.UseTrackSetups), kept apart from the rest of the setup because it names the race tracks
// (Terrain.RaceTrackIsland), which the tools that share RaceCarSetup.cs (tools/BodyPipeline) don't build.
public sealed partial class RaceCarSetup
{
    // Each track's best setup (UseTrackSetups), by its race track entry's name (Terrain.RaceTrackIsland: Citadel Island's town circuit is
    // its entry "Citadel Island", its storm track "Citadel Island, storm track"; Celebration Island's statue track and its lava lake are
    // two islands). A setup that is easy to drive there, not just quick (2026-10-17, the user: "the best setup is not the one that gives the
    // fastest time ... Reducing the speed to 80 and setting steering to 150 feels better to drive and allows us to more easily stay in the
    // centre of the track"): docs/LBA2_RACE_TRACK_STRESS_TEST.md, "Easy to drive, not only fast"; tools/RaceTrackStress/drive.py. A light
    // tap of the steering at the setup's speed, and the tap back a moment later, moves the car no further across the road -- for the road's
    // room there -- than it does on the Temple of Bú at 80 km/h with steering 150 % (the user's own setup: the Temple's is that); of those
    // setups, the engine's planner's fastest lap. From the standalone game's car -- five gears to 45/57/63/97/110 km/h, acceleration 300 %,
    // braking 300 %, steering 200 % -- top speed, acceleration and steering each 70-130 % of the car's, the three summing to no more than 300
    // (the stress test's budget of 2026-10-16: what the tap takes from top speed and steering goes to acceleration), the braking as it was.
    // Island CX, Otringal and the lava lake are at the grid's least (77-79 km/h, steering 140 %), still a little over the Temple's tap.
    // A track built later has none until it is planned too.
    public static IReadOnlyDictionary<string, TrackSetup> TrackSetups { get; } = new (Terrain.RaceTrackIsland Island, TrackSetup Best)[]
    {
        (Terrain.RaceTrackIsland.Desert, new("Desert island", 99, 390, 140)),
        (Terrain.RaceTrackIsland.CitadelStorm, new("Citadel, storm lap", 96, 390, 150)),
        (Terrain.RaceTrackIsland.Citadel, new("Citadel, town circuit", 93, 390, 150)),
        (Terrain.RaceTrackIsland.Mosquibe, new("Mosquibees Island", 99, 390, 140)),
        (Terrain.RaceTrackIsland.Celebration, new("Celebration Island", 82, 390, 140)),
        (Terrain.RaceTrackIsland.CelebrationLava, new("Celebration, lava lake", 79, 390, 140)),
        (Terrain.RaceTrackIsland.Elevator, new("Elevator Platform", 79, 390, 145)),
        (Terrain.RaceTrackIsland.Sendell, new("Sendell's Well", 101, 390, 155)),
        (Terrain.RaceTrackIsland.Moon, new("The old moon", 79, 390, 140)),
        // (a dreamt sprint: in place of the dream's own top speed, DreamInfo.TopKmh, too)
        (Terrain.RaceTrackIsland.Polar, new("Polar Island", 77, 390, 140)),
        (Terrain.RaceTrackIsland.Knartas, new("Island of the Francos", 104, 390, 145)),
        (Terrain.RaceTrackIsland.Otringal, new("Otringal", 77, 390, 140)),
        (Terrain.RaceTrackIsland.Volcano, new("Volcano Island", 85, 390, 175)),
        (Terrain.RaceTrackIsland.Emerald, new("The Emerald Moon", 104, 390, 140)),
        (Terrain.RaceTrackIsland.Wannies, new("Wannies Island", 101, 390, 150)),
        (Terrain.RaceTrackIsland.Palace, new("The Emperor's palace", 93, 390, 140)),
        (Terrain.RaceTrackIsland.CxTower, new("Island CX", 77, 390, 140)),
        (Terrain.RaceTrackIsland.Temple, new("The Temple of Bú", 80, 390, 150)),
    }.ToDictionary(t => t.Island.Name, t => t.Best, StringComparer.OrdinalIgnoreCase);
}
