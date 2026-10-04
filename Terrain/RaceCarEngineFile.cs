using System.Globalization;
using System.IO;
using System.Text;

namespace LBAAssembler.Terrain;

// The race car's setup (RaceCarSetup, kept in the settings) written as the engine's car file for its race-track mode (native RACEMOD.CPP), with
// what the game folder's RACETRACK.JSON says about the track: the start line, the checkpoints and the opponents' lines.
internal static class RaceCarEngineFile
{
    // An opponent as the engine is told of it: its name, its line, how many points before the start line it starts without a grid, its
    // skill (the engine's pace: the one to beat's the setup's, Pace; the others' drawn between Pace and PaceHi each race), its character
    // (Top, Grip: shares of the car's top speed and cornering, as its line was planned with: RaceTrackBuilder), the scenes' copies of its
    // car, whether it is the motorbike, only a time to beat (no car) or the one to beat, and its car's body (-1: not one of the racer's).
    internal sealed record Racing(string Name, List<int[]> Path, int Grid, int Pace, double Top, double Grip, Dictionary<int, int> Actors, bool Bike, bool Ghost,
        bool Main = false, int PaceHi = 0, int Body = -1);

    // The others' skills, against the car setup's (the one to beat's): drawn at random between these, each race.
    public const int RandomBelow = 14, RandomAbove = 12;

    // The engine's car setup file (RACEMOD.CPP's format), with the start line, the checkpoints and the opponents from the game folder's
    // RACETRACK.JSON when it has one (each opponent's line in the file named in `pathFiles`, in the order of Opponents). Of an island with
    // a track in each weather file, the one the folder was built to race (RaceTrackService.Raced), in its weather; the other's cars are
    // kept out of sight. `story`: the game played as a game, not a race started on its line -- the story's gates and results (Citadel
    // Island's, RaceTrackStory) are written too.
    internal static string EngineFile(this RaceCarSetup car, RaceTrackService.TrackInfo? info, IReadOnlyList<string>? pathFiles = null, string? raisedFile = null, bool story = false,
        string? guideFile = null)
    {
        var track = info is null ? null : RaceTrackService.Raced(info);
        string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var text = new StringBuilder("# LBA Assembler race car setup (read by the engine's race-track mode, RACEMOD.CPP)\n");
        text.Append(car.CarKeys());
        // (a line's height, when it has one: a lap that passes over itself crosses the line's place at other heights too)
        static string Height(RaceTrackService.StartLineInfo line) => line.Y is { } y ? $" {y}" : "";
        if (track?.StartLine is { } l) text.Append($"startline={l.CubeX} {l.CubeZ} {l.X0} {l.Z0} {l.X1} {l.Z1} {l.DirX} {l.DirZ}{Height(l)}\n");
        foreach (var c in track?.Checkpoints ?? new()) text.Append($"checkpoint={c.CubeX} {c.CubeZ} {c.X0} {c.Z0} {c.X1} {c.Z1} {c.DirX} {c.DirZ}{Height(c)}\n");
        // (the checkpoints seen: red lines across the road, for testing)
        if (track?.Checkpoints is { Count: > 0 }) text.Append($"checkpoint_lines={(car.ShowCheckpoints ? 1 : 0)}\n");
        // a raised road: the file with its middle, point by point (the engine's floor there)
        if (raisedFile is not null) text.Append($"raised={raisedFile}\n");
        // ... and how much its grade changes a car's speed (a rollercoaster of a lap)
        if (raisedFile is not null && track?.Gravity is { } gravity and > 0) text.Append($"gravity={N(gravity)}\n");
        // ... and the camera that rides the road behind the car: how far behind (units), how high, how far ahead it looks
        if (raisedFile is not null && track?.RailCamera is { Length: 3 } cam) text.Append($"railcam={N(cam[0] * 512)} {N(cam[1])} {N(cam[2] * 512)}\n");
        // Celebration Island's file with the statue, or (the lava lake's track) the one before it rises, whatever the game's own variable says
        if (info is not null && RaceTrackIsland.ByName(info.Island) is { } raced && (raced.Statue || raced.OtherFile)) text.Append($"statue={(raced.Statue ? 1 : 0)}\n");
        // an island drawn from a file the game never loads (the old moon's MOON.ILE, as island 3), and the lap's vertical loops
        if (info is not null && RaceTrackIsland.ByName(info.Island) is { RaceFile: { } raceFile } fileIsland) text.Append($"island_file={fileIsland.IslandByte} {raceFile}\n");
        foreach (var loop in track?.Loops ?? new()) text.Append($"loop={string.Join(' ', loop)}\n");
        // where the camera stands while the car flies a drop
        foreach (var dropCam in track?.JumpCameras ?? new()) text.Append($"jumpcam={string.Join(' ', dropCam)}\n");
        // the jumps the engine carries the car over (places in the raised road's file), and the island's scenes by cube for the cube
        // changes their flights make
        if (raisedFile is not null) foreach (var arc in track?.ArcJumps ?? new()) text.Append($"arcjump={string.Join(' ', arc)}\n");
        if (raisedFile is not null && track?.ArcJumps is { Count: > 0 }) foreach (var c in track.CubeScenes ?? new()) text.Append($"cube_scene={string.Join(' ', c)}\n");
        // the grid spots, and whether a qualifying lap sets the order the cars line up in (RACEMOD.CPP)
        foreach (var g in track?.Grid ?? new()) text.Append($"grid={string.Join(' ', g)}\n");
        foreach (var g in track?.Pits ?? new()) text.Append($"pit={string.Join(' ', g)}\n");
        if (track?.Grid is { Count: > 0 }) text.Append($"qualifying={(car.Qualifying ? 1 : 0)}\n");
        // (in the story the weather is the game's own: the set picks the track that goes with it)
        if (!story && RaceTrackService.FineWeather(info, car.FineWeather)) text.Append("weather=fine\n");
        if (track?.StoryArrow is >= 0 and var arrow) text.Append($"holo_arrow={arrow}\n");
        var opponents = car.Opponents(track);
        for (var i = 0; i < opponents.Count && pathFiles is not null && i < pathFiles.Count; i++)
        {
            // (the engine's first opponent is "opponent_", the next "opponent2_" and so on)
            var key = i == 0 ? "opponent" : $"opponent{i + 1}";
            var o = opponents[i];
            var pace = o.PaceHi > 0 ? $"{Math.Clamp(o.Pace, 10, 300)} {Math.Clamp(o.PaceHi, 10, 300)}" : $"{Math.Clamp(o.Pace, 10, 300)}";
            text.Append($"# {o.Name}{(o.Main ? " (the one to beat)" : "")}\n{key}_path={pathFiles[i]}\n{key}_grid={o.Grid}\n{key}_pace={pace}\n");
            // (its car at half its size, for the lightning spell: RaceTrackSmallCars)
            if (!o.Bike && o.Body >= 0 && !o.Ghost) text.Append($"{key}_small={RaceTrackSmallCars.SmallOf(o.Body)}\n");
            text.Append($"{key}_top={(int)Math.Round(o.Top * 100)}\n{key}_grip={(int)Math.Round(o.Grip * 100)}\n{key}_catchup={(car.OpponentsFightBack ? RaceCarSetup.CatchUpPercent : 0)}\n");
            text.Append($"{key}_name={o.Name}\n");
            // (the animations it stands and drives with: the cars' are the racer entity's 0 and 1, the bike's his own)
            if (o.Bike) text.Append($"{key}_anim={RaceTrackScenes.BikerIdleAnim} {RaceTrackScenes.BikerRideAnim}\n");
            foreach (var (scene, actor) in o.Actors) text.Append($"{key}_actor={scene} {actor}\n");
        }
        // a time to beat (a driver with no car: Raph's on Citadel Island's storm track), and in the story what beating it does
        var ghost = opponents.FindIndex(o => o.Ghost);
        var storm = story && info?.Story is not null && track is not null && ReferenceEquals(track, info) && info.Twin is not null;
        var town = story && info?.Story is not null && track is not null && ReferenceEquals(track, info.Twin);
        var lava = story && info?.Story is { Seller: true };
        // no opponent raced: the one to beat's line all the same, for what follows a line (the jet-pack, the penguins, the laps, the rescue)
        if (opponents.Count == 0 && guideFile is not null && Guide(track) is { } guide)
            text.Append($"# no opponent raced: {guide.Name}'s line, to drive by\nguide_path={guideFile}\nguide_top={(int)Math.Round(guide.Top * 100)}\nguide_grip={(int)Math.Round(guide.Grip * 100)}\n");
        // the one to beat, with a car (a race's win is finishing ahead of him)
        var main = opponents.FindIndex(o => o.Main && !o.Ghost);
        if (main >= 0) text.Append($"main={main + 1}\n");
        // the power-ups: the mushrooms along the lap and the penguin of each scene
        if (car.PowerUps && track?.Mushrooms is { Count: > 0 } mushrooms)
        {
            text.Append("powerups=1\n");
            text.Append($"penguin_mode={(car.PenguinsWalk ? "walk" : "fuse")}\n");
            foreach (var m in mushrooms) text.Append($"mushroom={m[0]} {m[1]}\n");
            foreach (var pg in track.Penguins ?? new()) text.Append($"penguin={pg[0]} {pg[1]}\n");
            foreach (var oil in track.Oil ?? new()) text.Append($"oil={oil[0]} {oil[1]}\n");
            // (the oil's drum, which the item box shows)
            if (track.OilIcon is { } icon) text.Append($"oil_icon={icon}\n");
        }
        if (ghost >= 0) text.Append($"beat={ghost + 1}{(storm ? $" {RaceTrackStory.BeatVar} 1" : "")}\n");
        // the story's gates and the town circuit's race: Mr. Paul lets no one race without racing gloves; the aliens' track is ready the day
        // after the storm, once Twinsen has slept; three laps, and a win is Mr. Paul's ferry ticket
        if (storm) text.Append($"gate={RaceTrackStory.GlovesSlot} 1 Mr. Paul: racing gloves first!\n");
        if (town)
        {
            text.Append($"gate={RaceTrackStory.DayVar} {RaceTrackStory.Rested} The new track opens tomorrow\n");
            text.Append($"race_laps={RaceTrackStory.RaceLaps}\nwin={RaceTrackStory.WonVar} 1\n");
        }
        // Celebration Island's lava lake: three laps against the souvenir seller; ahead of him he tells what he knows (RaceTrackStory)
        if (lava) text.Append($"race_laps={RaceTrackStory.RaceLaps}\nwin={RaceTrackStory.SellerBeaten} 1\nfinish={RaceTrackStory.SellerRaced} 1\n");
        // the cars of the opponents the setup doesn't race: hidden, so they don't stand where a racing car lines up
        // (and all of the other weather's track's: that isn't the island the game draws)
        var parked = car.Parked(track);
        if (info?.Twin is { } fine) parked.AddRange(AllCars(ReferenceEquals(track, fine) ? info : fine));
        foreach (var (scene, actor) in parked) text.Append($"hide_actor={scene} {actor}\n");
        return text.ToString();
    }

    // The car's handling, as the setup makes it.
    private static string CarKeys(this RaceCarSetup car)
    {
        string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var gears = Math.Clamp(car.Gears, 1, RaceCarSetup.MaxGears);
        var text = new StringBuilder($"gears={gears}\n");
        for (var g = 0; g < gears; g++) text.Append($"gear{g + 1}={RaceCarSetup.KmhToUnits(Math.Clamp(car.TopKmh(g), 3, 150))}\n");
        text.Append($"accel={N(RaceCarSetup.OriginalAccel * Math.Clamp(car.AccelerationPercent, 10, 1000) / 100.0)}\n");
        text.Append($"brake={N(RaceCarSetup.OriginalBrake * Math.Clamp(car.BrakingPercent, 10, 1000) / 100.0)}\n");
        text.Append($"coast={N(RaceCarSetup.OriginalCoast * Math.Clamp(car.CoastingPercent, 0, 1000) / 100.0)}\n");
        text.Append($"reverse={RaceCarSetup.KmhToUnits(Math.Clamp(car.ReverseKmh, 1, 150))}\n");
        text.Append($"steer={(int)Math.Round(RaceCarSetup.OriginalSteer * Math.Clamp(car.SteeringPercent, 10, 1000) / 100.0)}\n");
        text.Append($"automatic={(car.Automatic ? 1 : 0)}\n");
        text.Append($"hud={(car.ShowDisplay ? 1 : 0)}\n");
        return text.ToString();
    }

    // The scenes' copies of every opponent's car a track has.
    private static IEnumerable<(int Scene, int Actor)> AllCars(RaceTrackService.TrackInfo track) =>
        (track.Drivers ?? new()).SelectMany(d => d.Actors.Select(a => (a.Key, a.Value)))
            .Concat((track.Mushrooms ?? new()).Concat(track.Penguins ?? new()).Concat(track.Oil ?? new()).Select(m => (m[0], m[1])))
            .Concat((track.Opponent ?? new()).Select(a => (a.Key, a.Value)))
            .Concat((track.Rivals ?? new()).SelectMany(r => r.Actors.Select(a => (a.Key, a.Value))));

    // The scenes' copies of the cars of the opponents the track has and the setup doesn't race.
    internal static List<(int Scene, int Actor)> Parked(this RaceCarSetup car, RaceTrackService.TrackInfo? track)
    {
        if (track is null) return new();
        var raced = car.Opponents(track).SelectMany(o => o.Actors.Select(a => (a.Key, a.Value))).ToHashSet();
        // (the power-ups' mushrooms and penguins: the race-track mode's when they are on, out of sight when they are off)
        if (car.PowerUps) foreach (var m in (track.Mushrooms ?? new()).Concat(track.Penguins ?? new()).Concat(track.Oil ?? new())) raced.Add((m[0], m[1]));
        return AllCars(track).Where(a => !raced.Contains(a)).ToList();
    }

    // The opponents the car setup races, in the engine's order: the track's line-up (RaceDriver: each when the setup races opponents and
    // doesn't leave that one out; a time to beat always), each with its line, how many points before the start line it starts, its skill
    // (the setup's, a few points either way for its driver), its character and the scenes' copies of its car. A track built before the
    // line-ups: the retail track's racer, Baldino and the biker.
    internal static List<Racing> Opponents(this RaceCarSetup car, RaceTrackService.TrackInfo? track)
    {
        var list = new List<Racing>();
        // (the one to beat at the setup's skill; the others at random, RandomBelow under it to RandomAbove over)
        Racing Of(string name, List<int[]> path, int grid, double top, double grip, Dictionary<int, int> actors, bool bike, bool ghost, bool main, int body) =>
            main || ghost
                ? new(name, path, grid, Math.Clamp(car.MainSkill, 10, 300), top, grip, actors, bike, ghost, main, 0, body)
                : new(name, path, grid, Math.Clamp(car.MainSkill - RandomBelow, 10, 300), top, grip, actors, bike, ghost, main, Math.Clamp(car.MainSkill + RandomAbove, 10, 300), body);
        if (track?.Drivers is { Count: > 0 } drivers)
        {
            foreach (var d in drivers)
                if (d.Ghost || car.Opponent && !car.LeftOut.Contains(d.Name))
                    list.Add(Of(d.Name, d.Path, d.Grid, d.Top, d.Grip, d.Actors, d.Bike, d.Ghost, d.Main, d.Body));
            return list;
        }
        if (car.Opponent && track?.Path is { Count: > 0 } path && track.Opponent is { Count: > 0 } actors && !car.LeftOut.Contains(RaceDriver.Racer.Name))
            list.Add(Of(RaceDriver.Racer.Name, path, track.PathGrid, RaceTrackBuilder.RacerLine.Top, RaceTrackBuilder.RacerLine.Grip, actors, false, false, true, -1));
        foreach (var r in track?.Rivals ?? new())
        {
            if (!car.Opponent || r.Path.Count == 0 || r.Actors.Count == 0 || car.LeftOut.Contains(r.Name)) continue;
            var d = r.Name == BikerName ? RaceDriver.Biker : RaceDriver.Baldino;
            list.Add(Of(r.Name, r.Path, r.Grid, d.Line.Top, d.Line.Grip, r.Actors, d.Bike, false, false, -1));
        }
        return list;
    }

    public const string BikerName = "The biker";

    // The line to drive by when no opponent is raced: the one to beat's (a car's before a time to beat's), or the racer's of a track built
    // before the line-ups.
    internal static (string Name, List<int[]> Path, double Top, double Grip)? Guide(RaceTrackService.TrackInfo? track)
    {
        if (track?.Drivers?.Where(d => d.Path.Count > 2).OrderByDescending(d => d.Main && !d.Ghost).ThenBy(d => d.Ghost).FirstOrDefault() is { } d) return (d.Name, d.Path, d.Top, d.Grip);
        if (track?.Path is { Count: > 2 } path) return (RaceDriver.Racer.Name, path, RaceTrackBuilder.RacerLine.Top, RaceTrackBuilder.RacerLine.Grip);
        return null;
    }

    // The engine's car setup file, and beside it each opponent's line (racepath.txt, racepath2.txt ...; `prefix` before each name: a set's
    // tracks each have their own).
    internal static void WriteEngineFile(this RaceCarSetup car, string path, RaceTrackService.TrackInfo? info, bool story = false, string prefix = "")
    {
        var files = new List<string>();
        var track = info is null ? null : RaceTrackService.Raced(info);
        var folder = Path.GetDirectoryName(path) ?? ".";
        foreach (var (o, i) in car.Opponents(track).Select((o, i) => (o, i)))
        {
            var file = Path.Combine(folder, prefix + (i == 0 ? "racepath.txt" : $"racepath{i + 1}.txt"));
            File.WriteAllLines(file, o.Path.Select(p => string.Join(' ', p)));
            files.Add(file);
        }
        // (no opponent raced: the line to drive by)
        string? guide = null;
        if (files.Count == 0 && Guide(track) is { } g)
        {
            guide = Path.Combine(folder, prefix + "raceguide.txt");
            File.WriteAllLines(guide, g.Path.Select(p => string.Join(' ', p)));
        }
        string? raised = null;
        if (track?.Raised is { Count: > 1 } road)
        {
            raised = Path.Combine(folder, prefix + "raceraised.txt");
            File.WriteAllLines(raised, road.Select(p => string.Join(' ', p)));
        }
        File.WriteAllText(path, car.EngineFile(info, files, raised, story, guide));
    }

    // The game played as a game (RaceTrackService.CarFileWriter's story): every track of the folder in a car file of its own beside `path`,
    // and `path` the set (RACEMOD.CPP track=) -- the car's handling, which of them is raced where and when (the engine loads the one of the
    // island it enters: Citadel Island's storm track in the storm, its town circuit once the storm is over), and the story's tired line.
    internal static void WriteStorySet(this RaceCarSetup car, string path, RaceTrackService.TrackInfo info)
    {
        var folder = Path.GetDirectoryName(path) ?? ".";
        var text = new StringBuilder("# LBA Assembler race car setup: the folder's tracks, each raced where and when the game is (RACEMOD.CPP track=)\n");
        text.Append(car.CarKeys());
        var n = 0;
        foreach (var t in RaceTrackService.Tracks(info))
        {
            var island = RaceTrackIsland.ByName(t.Island);
            // (an island with a track in each file: each its own, the storm's while it rains, the other's once the storm is over)
            var members = t.Twin is not null
                ? new[] { (Info: t with { Island = RaceTrackIsland.CitadelStorm.Name }, When: 1), (Info: t with { Island = RaceTrackIsland.Citadel.Name }, When: 2) }
                : new[] { (Info: t, When: island.Statue ? 3 : island.OtherFile ? 4 : 0) };
            foreach (var (member, when) in members)
            {
                var file = Path.Combine(folder, $"racecar_track{++n}.txt");
                car.WriteEngineFile(file, member, story: true, prefix: $"track{n}_");
                // (the scenes it is raced in: an island's own outside scenes, or the copies a track of its own races in)
                var (first, last) = island.CopiesScene is not null || island.CopiesScenes is not null ? (island.FirstScene, island.LastScene) : (-1, -1);
                text.Append($"track={island.IslandByte} {when} {first} {last} {file}\n");
            }
            if (t.Story is { TiredText: >= 0 } s) text.Append($"tired={RaceTrackStory.DayVar} {RaceTrackStory.Tired} {s.TiredText}\n");
        }
        File.WriteAllText(path, text.ToString());
    }
}
