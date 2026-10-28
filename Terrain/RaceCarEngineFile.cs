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
        bool Main = false, int PaceHi = 0, int Body = -1)
    {
        // (a car that flies, bobbing over its line this far: RaceDriver.Hover; 0 none)
        public int Hover { get; init; }
    }

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
        // (the track's best setup, when the setup says so: its top speed, acceleration and steering in place of the sliders' -- a dreamt
        // sprint's in place of the dream's own top speed too)
        var best = car.UseTrackSetups ? BestSetup(info) : null;
        if (best is not null) text.Append($"# the track's best setup (docs/LBA2_RACE_TRACK_STRESS_TEST.md): {best}\n");
        // (a dreamt sprint: the car's gears scaled to its own top speed -- Twinsen's car is a lot faster in his dream)
        text.Append((best is null ? car : car.With(best)).CarKeys(best is null && track?.Dream is { TopKmh: > 0 } scaled ? scaled.TopKmh : null,
            info is null ? null : RaceTrackIsland.ByName(info.Island).Name));
        // (his own car on a track that has one for him, when the setup leaves him his own: the Temple of Bú's pumpkin F1)
        if ((car.DriveAsKey is null || car.DriveAs is RaceCarSetup.LaserGreen or RaceCarSetup.LaserRed) && info is not null &&
            car.TrackCarKeys?.GetValueOrDefault(RaceTrackIsland.ByName(info.Island).Name) is { } own) text.Append(own);
        // (a line's height, when it has one: a lap that passes over itself crosses the line's place at other heights too)
        static string Height(RaceTrackService.StartLineInfo line) => line.Y is { } y ? $" {y}" : "";
        if (track?.StartLine is { } l) text.Append($"startline={l.CubeX} {l.CubeZ} {l.X0} {l.Z0} {l.X1} {l.Z1} {l.DirX} {l.DirZ}{Height(l)}\n");
        foreach (var c in track?.Checkpoints ?? new()) text.Append($"checkpoint={c.CubeX} {c.CubeZ} {c.X0} {c.Z0} {c.X1} {c.Z1} {c.DirX} {c.DirZ}{Height(c)}\n");
        // (the checkpoints seen: red lines across the road, for testing)
        if (track?.Checkpoints is { Count: > 0 }) text.Append($"checkpoint_lines={(car.ShowCheckpoints ? 1 : 0)}\n");
        // a raised road: the file with its middle, point by point (the engine's floor there)
        if (raisedFile is not null) text.Append($"raised={raisedFile}\n");
        // ... the lap's own points in it, and its branches after them (a shortcut off the lap and back onto it)
        if (raisedFile is not null && track?.RaisedLap is { } lapPoints && track.RaisedBranches is { Count: > 0 } branches)
        {
            text.Append($"raised_lap={lapPoints}\n");
            foreach (var b in branches) text.Append($"raised_branch={string.Join(' ', b)}\n");
        }
        // ... and how much its grade changes a car's speed (a rollercoaster of a lap)
        if (raisedFile is not null && track?.Gravity is { } gravity and > 0) text.Append($"gravity={N(gravity)}\n");
        // ... and the camera that rides the road behind the car: how far behind (units), how high, how far ahead it looks (and whether it goes
        // across with the car off the road onto a floor beside it: the Temple of Bú's)
        if (raisedFile is not null && track?.RailCamera is { Length: >= 3 and <= 6 } cam)
            text.Append($"railcam={N(cam[0] * 512)} {N(cam[1])} {N(cam[2] * 512)}{string.Concat(cam.Skip(3).Select(v => " " + N(v)))}\n");
        // Celebration Island's file with the statue, or (the lava lake's track) the one before it rises, whatever the game's own variable says
        if (info is not null && RaceTrackIsland.ByName(info.Island) is { } raced && (raced.Statue || raced.OtherFile)) text.Append($"statue={(raced.Statue ? 1 : 0)}\n");
        // an island drawn from a file the game never loads (the old moon's MOON.ILE, as island 3), and the lap's vertical loops
        if (info is not null && RaceTrackIsland.ByName(info.Island) is { RaceFile: { } raceFile } fileIsland)
        {
            text.Append($"island_file={fileIsland.IslandByte} {raceFile}\n");
            // (and its own palette and sky: the Temple of Bú's)
            if (fileIsland.Look is { } look) text.Append($"island_palette={look.Palette}\nisland_sky={look.Sky}\n");
        }
        // the race's own music in place of its scenes' (the Temple of Bú's: LBA1's temple music), a file in the game folder
        if (track?.Music is { } music) text.Append($"music={music}\n");
        foreach (var loop in track?.Loops ?? new()) text.Append($"loop={string.Join(' ', loop)}\n");
        // (the rail holding the car on the road even where it lies on the ground: the Emperor's palace's)
        if (raisedFile is not null && info is not null && RaceTrackIsland.ByName(info.Island) is { RailHeld: true }) text.Append("rail_held=1\n");
        // (the Island of the Francos: every car on Gazogem fuel all race, none from a mushroom)
        if (info is not null && RaceTrackIsland.ByName(info.Island) is { FuelAlways: true }) text.Append("fuel_always=1\n");
        // (game variables held while it races: travel cutscenes kept from playing -- Otringal's Dino-Fly landing)
        if (info is not null) foreach (var (n, value) in RaceTrackIsland.ByName(info.Island).HeldVars) text.Append($"vargame={n} {value}\n");
        // the pipes over the road: their steam and the oil they drip onto it
        foreach (var steam in track?.Steam ?? new()) text.Append($"steam={string.Join(' ', steam)}\n");
        foreach (var drip in track?.Drips ?? new()) text.Append($"drip={string.Join(' ', drip)}\n");
        foreach (var jet in track?.Jets ?? new()) text.Append($"jet={string.Join(' ', jet)}\n");
        foreach (var tunnel in track?.Tunnels ?? new()) text.Append($"tunnel={string.Join(' ', tunnel)}\n");
        // the spaceships it flies over their pads on the road (Otringal's square island)
        foreach (var shuttle in track?.Shuttles ?? new()) text.Append($"shuttle={string.Join(' ', shuttle)}\n");
        // the lava's balls of fire (Volcano Island)
        foreach (var ball in track?.LavaBalls ?? new()) text.Append($"lavaball={string.Join(' ', ball)}\n");
        // (the game's lava ball, bigger, that they and the lava fall throw: RaceTrackOil.InstallLavaBall)
        if (track?.LavaBallModel is { } lavaBall && (track.LavaBalls is { Count: > 0 } || track.LavaFalls is { Count: > 0 } || track.LavaGeysers is { Count: > 0 }))
            text.Append($"lava_ball_model={lavaBall}\n");
        // ... its lava fall, across its face, and its boost panels (Volcano Island's, 2026-10-10)
        foreach (var fall in track?.LavaFalls ?? new()) text.Append($"lavafall={string.Join(' ', fall)}\n");
        foreach (var geyser in track?.LavaGeysers ?? new()) text.Append($"lavageyser={string.Join(' ', geyser)}\n");
        foreach (var shard in track?.CrystalShards ?? new()) text.Append($"crystalshards={string.Join(' ', shard)}\n");
        if (track?.CrystalShardModels is { Length: 2 } shardModels && track.CrystalShards is { Count: > 0 }) text.Append($"crystal_shard_model={shardModels[0]} {shardModels[1]}\n");
        foreach (var panel in track?.BoostPanels ?? new()) text.Append($"boostpanel={string.Join(' ', panel)}\n");
        // the machine guns on their knolls, each followed by where its bullets land (Otringal's)
        foreach (var g in track?.Gunners ?? new())
        {
            if (g.Length < 6) continue;
            text.Append($"gunner={string.Join(' ', g.Take(6))}\n");
            for (var k = 6; k + 3 < g.Length; k += 4) text.Append($"gunner_target={g[k]} {g[k + 1]} {g[k + 2]} {g[k + 3]}\n");
        }
        // the island's own guns, each burst at a car outside that it picks (Island CX's cannons)
        foreach (var h in track?.Hunters ?? new())
        {
            if (h.Length < 6) continue;
            text.Append($"gunner={h[0]} {h[1]} 0 {h[2]} {h[3]} {h[4]}\n");
            text.Append($"gunner_hunt={h[5]}\n");
        }
        // the roulette wheel the lap runs through, its head turning and its ball rolling (Otringal's)
        if (raisedFile is not null && track?.Roulette is { Length: >= 15 } wheel)
        {
            text.Append($"roulette={string.Join(' ', wheel)}\n");
            if (track.RouletteBalls is > 1 and var balls) text.Append($"roulette_balls={balls}\n");
            if (track.RoulettePockets is { } pockets) text.Append($"roulette_pockets={pockets}\n");
        }
        // the gas monsters that rise out of the gas beside the road to bite cars (Otringal's)
        foreach (var monster in track?.GasMonsters ?? new()) text.Append($"gasmonster={string.Join(' ', monster)}\n");
        // the flying gas monsters, to and fro beside the road, spitting fire balls (Island CX's hall)
        foreach (var flyer in track?.Flyers ?? new()) text.Append($"flyer={string.Join(' ', flyer)}\n");
        foreach (var spook in track?.Spooks ?? new()) text.Append($"spook={string.Join(' ', spook)}\n");
        // the witches diving at Twinsen's car, and what jumps up beside the road (the Temple of Bú's)
        foreach (var dive in track?.Dives ?? new()) text.Append($"dive={string.Join(' ', dive)}\n");
        foreach (var scare in track?.Scares ?? new()) text.Append($"scare={string.Join(' ', scare)}\n");
        // the giant web the lap drives through, each followed by its parts (the Temple of Bú's)
        for (var i = 0; i < (track?.GiantWebs?.Count ?? 0); i++)
        {
            text.Append($"web={string.Join(' ', track!.GiantWebs![i])}\n");
            foreach (var part in (track.WebParts ?? new()).Where(p => p.Length >= 6 && p[0] == i)) text.Append($"web_part={string.Join(' ', part.Skip(1))}\n");
        }
        // the flag marshal by the start line: the start flag dropped at GO, the chequered flag waved at the finish -- on her plinth beside the
        // road, and for the start on the road between the grid's columns (the Temple of Bú's)
        if (track?.Marshal is { Length: >= 10 } marshal)
        {
            text.Append($"marshal={string.Join(' ', marshal.Take(10))}\n");
            if (marshal.Length >= 11) text.Append($"marshal_plinth={marshal[10]}\n");
            if (marshal.Length >= 15) text.Append($"marshal_start={string.Join(' ', marshal.Skip(11).Take(4))}\n");
        }
        // Twinsen may get out of his car (Space, stopped) and explore while the race goes on (the Temple of Bú's secret party)
        if (track?.OnFoot == true) text.Append("on_foot=1\n");
        // the people in the cars' way, stepping out at them, striking and throwing (the Emperor's palace's)
        foreach (var guard in track?.Guards ?? new()) text.Append($"guard={string.Join(' ', guard)}\n");
        if (track?.Guards is { Count: > 0 }) text.Append($"guard_size={(int)Math.Round(RaceTrackGuards.Size * 100)}\n");
        foreach (var teleport in track?.Teleports ?? new()) text.Append($"teleport={string.Join(' ', teleport)}\n");
        // where the raised road's rail lets a car go off its edge, down to the road under it (the Temple of Bú's spiral drop)
        foreach (var open in track?.RailOpens ?? new()) text.Append($"rail_open={string.Join(' ', open)}\n");
        // the pit lanes on the ground, for the mini-map: 20 points a line (the engine reads lines of up to 512 characters)
        for (var r = 0; r < (track?.MapRoads?.Count ?? 0); r++)
            foreach (var chunk in track!.MapRoads![r].Chunk(40)) text.Append($"map_road={r} {string.Join(' ', chunk)}\n");
        // the giant frog the lap drives through, its mouth opening and shutting (Wannies Island's)
        foreach (var frog in track?.Frogs ?? new()) text.Append($"frog={string.Join(' ', frog)}\n");
        // where the camera stands while the car flies a drop
        foreach (var dropCam in track?.JumpCameras ?? new()) text.Append($"jumpcam={string.Join(' ', dropCam)}\n");
        // the jumps the engine carries the car over (places in the raised road's file), and the island's scenes by cube for the cube
        // changes their flights make
        if (raisedFile is not null) foreach (var arc in track?.ArcJumps ?? new()) text.Append($"arcjump={string.Join(' ', arc)}\n");
        // (and the teleporters' into another cube: the Temple of Bú's hidden passage)
        if (raisedFile is not null && (track?.ArcJumps is { Count: > 0 } || track?.Teleports is { Count: > 0 })) foreach (var c in track!.CubeScenes ?? new()) text.Append($"cube_scene={string.Join(' ', c)}\n");
        // where the cars drive where they like: its floor, the camera on the car's trail (the Emperor's palace's rooms)
        foreach (var area in track?.OpenAreas ?? new()) text.Append($"open_area={string.Join(' ', area)}\n");
        // the grid spots, and whether a qualifying lap sets the order the cars line up in (RACEMOD.CPP)
        foreach (var g in track?.Grid ?? new()) text.Append($"grid={string.Join(' ', g)}\n");
        foreach (var g in track?.Pits ?? new()) text.Append($"pit={string.Join(' ', g)}\n");
        if (track?.Grid is { Count: > 0 }) text.Append($"qualifying={(car.Qualifying && track.Dream is null ? 1 : 0)}\n");
        // a dreamt sprint (Polar Island's): start line to finish line once, its intro, and a win's waking up (a loss runs it again)
        if (track?.Dream is { } dream)
        {
            var f = dream.Finish;
            text.Append($"sprint=1\nrace_laps=1\nfinishline={f.CubeX} {f.CubeZ} {f.X0} {f.Z0} {f.X1} {f.Z1} {f.DirX} {f.DirZ}{Height(f)}\n");
            // (Polar Island's: the story's start in place of the intro -- FunFrock drives off, the grobo gives Twinsen the key -- and the end
            // of the race the end of the dream whoever gets there first: PolarDream's chase)
            if (info is not null && RaceTrackIsland.ByName(info.Island) == RaceTrackIsland.Polar)
                text.Append($"chase={LBAAssembler.Terrain.Polar.PolarDream.ChaseFunFrockText} {LBAAssembler.Terrain.Polar.PolarDream.ChaseTwinsenText} " +
                            $"{LBAAssembler.Terrain.Polar.PolarGrobo.Entity} {LBAAssembler.Terrain.Polar.PolarDream.ChaseGroboText} {LBAAssembler.Terrain.Polar.PolarGrobo.KeyVar}\n" +
                            $"chase_lead={LBAAssembler.Terrain.Polar.PolarDream.ChaseLeadCells}\ndream_any_end=1\nwake={dream.WakeScene} {dream.WakeText} {dream.WakeActor}\n" +
                            (info.ChaseCar is [var cb, var ca, var ce] ? $"chase_car={cb} {ca} {ce}\n" : ""));
            else text.Append($"intro={dream.IntroText}\nlose={dream.LoseText}\nwake={dream.WakeScene} {dream.WakeText} {dream.WakeActor}\n");
            // (won: scene 0's opening wakes Twinsen up in his bed)
            if (dream.WinVar >= 0) text.Append($"win={dream.WinVar} 1\n");
            // (and the finish line a jump's lip: he wakes up in mid-flight)
            if (dream.WakeFlight > 0) text.Append($"wake_flight={dream.WakeFlight}\n");
            // (and awake, the car's own gears again: the dream's are scaled to its top speed)
            var gears = Math.Clamp(car.Gears, 1, RaceCarSetup.MaxGears);
            text.Append($"after_gears={string.Join(' ', Enumerable.Range(0, gears).Select(g => RaceCarSetup.KmhToUnits(Math.Clamp(car.TopKmh(g), 3, 150))))}\n");
        }
        // (in the story the weather is the game's own: the set picks the track that goes with it)
        // (and not for a dreamt race, which picks up the story: Twinsen wakes up in the game's own weather)
        if (!story && track?.Dream is null && RaceTrackService.FineWeather(info, car.FineWeather)) text.Append("weather=fine\n");
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
            // (a car that flies -- the Temple of Bú's witch on her broomstick, no wheels --: bobbing gently over its line as it goes, drawn
            // only: its place along the line and what meets it are any car's)
            if (o.Hover > 0 && !o.Ghost) text.Append($"{key}_hover={o.Hover} {RaceDriver.HoverMs}\n");
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
        // the race's level, on a track with levels raced against opponents (the Temple of Bú's): the one the setup races and every level's
        // numbers -- the race game's menu adds its own pick after them (RACEMOD.CPP difficulty=, RaceDifficulty)
        if (opponents.Any(o => !o.Ghost) && info is not null && RaceTrackIsland.ByName(info.Island) is { Levels: { Length: 3 } levels })
        {
            var level = Math.Clamp(car.Difficulty, RaceDifficulty.Easy, RaceDifficulty.Hard);
            text.Append($"# the race's level: {RaceDifficulty.Name(level)} (0 easy, 1 medium, 2 hard)\ndifficulty={level}\n");
            for (var n = 0; n < levels.Length; n++) text.Append(levels[n].Line(n, car.OpponentsFightBack)).Append('\n');
        }
        // the power-ups: the mushrooms along the lap and the penguin of each scene
        if (car.PowerUps && track?.Mushrooms is { Count: > 0 } mushrooms)
        {
            text.Append("powerups=1\n");
            text.Append($"penguin_mode={(car.PenguinsWalk ? "walk" : "fuse")}\n");
            // (and the height each stands at on the road: built out of sight since 2026-10-05, older builds stand them there)
            foreach (var m in mushrooms) text.Append($"mushroom={string.Join(' ', m)}\n");
            foreach (var pg in track.Penguins ?? new()) text.Append($"penguin={pg[0]} {pg[1]}\n");
            foreach (var oil in track.Oil ?? new()) text.Append($"oil={oil[0]} {oil[1]}\n");
            foreach (var fake in track.Fakes ?? new()) text.Append($"fake={fake[0]} {fake[1]}\n");
            // (the laser cars' bolts: the game's own, whatever a track built before them noted -- RaceTrackLasers)
            text.Append($"laser_models={RaceTrackLasers.Green} {RaceTrackLasers.Red}\n");
            // (the oil's drum, which the item box shows)
            if (track.OilIcon is { } icon) text.Append($"oil_icon={icon}\n");
            // (the golden Gazogem, the roulette wheel's super jackpot)
            if (track.GoldIcon is { } gold) text.Append($"gold_icon={gold}\n");
            // (the super jet-pack the car turns into while it drives it)
            if (track.SuperJetModel is { } jetModel) text.Append($"superjet_model={jetModel}\n");
            // (Twinsen's buggy at half its size, for an opponent's lightning)
            if (track.TwinsenSmall is { } small) text.Append($"twinsen_small={small}\n");
            // (the ghost power-up, the Temple of Bú's: its ghost and its icon)
            if (track.Haunt is [var hauntGhost, var hauntIcon]) text.Append($"haunt={hauntGhost} {hauntIcon}\n");
        }
        if (ghost >= 0) text.Append($"beat={ghost + 1}{(storm ? $" {RaceTrackStory.BeatVar} 1" : "")}\n");
        // the story's gates and the town circuit's race: Mr. Paul lets no one race without racing gloves; the aliens' track is ready the day
        // after the storm, once Twinsen has slept; three laps, and a win is Mr. Paul's ferry ticket
        if (storm) text.Append($"gate={RaceTrackStory.GlovesSlot} 1 Mr. Paul: driving gloves first!\n");
        // the storm track's story: Raph laps it in his car -- the town circuit's copies of it, out of sight in the storm -- until Twinsen comes
        // to talk to him, then it stands parked by the start line; and his line that tells the time to beat has it in
        if (storm && ghost >= 0 && info!.Story is { } st)
        {
            if (st.RaphTime >= 0) text.Append($"beat_text={st.RaphTime}\n");
            var laps = info.Twin?.Drivers?.FirstOrDefault(d => d.Name == opponents[ghost].Name && !d.Ghost && d.Actors.Count > 0);
            if (laps is not null)
            {
                text.Append($"parade={ghost + 1} {RaceTrackStory.RaphHere} 1\n");
                foreach (var (scene, actor) in laps.Actors) text.Append($"parade_actor={scene} {actor}\n");
                if (st.RaphPark is [var ps, var px, var py, var pz, var pb]) text.Append($"parade_park={ps} {px} {py} {pz} {pb}\n");
                // (he pulls up beside Twinsen on the start line, tyres screeching, and yells at him to get off the track)
                // (and stays there, beside him on the road, Raph to be talked to at his car, until Twinsen drives with his gloves on: then to
                // his park)
                if (st.RaphYell >= 0) text.Append($"parade_stop={st.RaphYell} {st.RaphEntity} {RaceTrackStory.GlovesSlot}\n");
            }
        }
        if (town)
        {
            text.Append($"gate={RaceTrackStory.DayVar} {RaceTrackStory.Rested} The new track opens tomorrow\n");
            text.Append($"race_laps={RaceTrackStory.RaceLaps}\nwin={RaceTrackStory.WonVar} 1\n");
        }
        // Celebration Island's lava lake: three laps against the souvenir seller; ahead of him he tells what he knows (RaceTrackStory)
        if (lava) text.Append($"race_laps={RaceTrackStory.RaceLaps}\nwin={RaceTrackStory.SellerBeaten} 1\nfinish={RaceTrackStory.SellerRaced} 1\n");
        // a race over laps of its own, when no story or dream has set them: the Temple of Bú's five
        if (track?.Dream is null && !town && !lava && info is not null && RaceTrackIsland.ByName(info.Island).RaceLaps is int ownLaps and > 0) text.Append($"race_laps={ownLaps}\n");
        // the cars of the opponents the setup doesn't race: hidden, so they don't stand where a racing car lines up
        // (and all of the other weather's track's: that isn't the island the game draws)
        var parked = car.Parked(track);
        if (info?.Twin is { } fine) parked.AddRange(AllCars(ReferenceEquals(track, fine) ? info : fine));
        foreach (var (scene, actor) in parked) text.Append($"hide_actor={scene} {actor}\n");
        // the doors one of the island's files has lost, shut while it is shown (RaceTrackScenes Result.DoorsOff)
        // (and the ones that bring Twinsen out elsewhere in one file: nine numbers -- RACEMOD.CPP door_to=)
        foreach (var d in info?.DoorsOff ?? new()) text.Append(d.Split(' ').Length == 9 ? $"door_to={d}\n" : $"door_off={d}\n");
        // ... and the actors put aside while it is shown (Result.ActorsAt)
        foreach (var a in info?.ActorsAt ?? new()) text.Append($"actor_at={a}\n");
        // ... and the lifts on the raised road (Result.DeckLifts)
        foreach (var lift in info?.DeckLifts ?? new()) text.Append($"deck_lift={lift}\n");
        return text.ToString();
    }

    // The best setup (RaceCarSetup.TrackSetups) of the track a record races: its island's entry's -- of an island with a track in each
    // file, the entry of the one raced (Citadel Island's town circuit or its storm track, as Raced picks) -- null for a track with none.
    internal static RaceCarSetup.TrackSetup? BestSetup(RaceTrackService.TrackInfo? info)
    {
        // (not ByName: that takes a name it doesn't know for the Desert island's)
        if (info is null || RaceTrackIsland.All.FirstOrDefault(i => string.Equals(i.Name, info.Island, StringComparison.OrdinalIgnoreCase)) is not { } island) return null;
        if (island.TwinIleFile is not null)
        {
            var twin = info.Twin is not null && ReferenceEquals(RaceTrackService.Raced(info), info.Twin);
            island = RaceTrackIsland.All.FirstOrDefault(i => i.IleFile == island.IleFile && i.RacesTwin == twin) ?? island;
        }
        return RaceCarSetup.TrackSetups.GetValueOrDefault(island.Name);
    }

    // The car's handling, as the setup makes it -- with `topKmh`, every gear's top speed scaled so the top gear's is that (Polar Island's
    // dream race: 120 km/h); `island`: the island raced, for a car drawn as another copy there (DriveAsIslandKeys).
    private static string CarKeys(this RaceCarSetup car, int? topKmh = null, string? island = null)
    {
        string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var gears = Math.Clamp(car.Gears, 1, RaceCarSetup.MaxGears);
        var text = new StringBuilder($"gears={gears}\n");
        var scale = topKmh is { } top && car.TopKmh(gears - 1) > 0 ? (double)top / car.TopKmh(gears - 1) : 1;
        for (var g = 0; g < gears; g++) text.Append($"gear{g + 1}={RaceCarSetup.KmhToUnits(Math.Clamp(car.TopKmh(g) * scale, 3, RaceCarSetup.MaxKmh))}\n");
        text.Append($"accel={N(RaceCarSetup.OriginalAccel * Math.Clamp(car.AccelerationPercent, 10, 1000) / 100.0)}\n");
        text.Append($"brake={N(RaceCarSetup.OriginalBrake * Math.Clamp(car.BrakingPercent, 10, 1000) / 100.0)}\n");
        text.Append($"coast={N(RaceCarSetup.OriginalCoast * Math.Clamp(car.CoastingPercent, 0, 1000) / 100.0)}\n");
        text.Append($"reverse={RaceCarSetup.KmhToUnits(Math.Clamp(car.ReverseKmh, 1, RaceCarSetup.MaxKmh))}\n");
        text.Append($"steer={(int)Math.Round(RaceCarSetup.OriginalSteer * Math.Clamp(car.SteeringPercent, 10, 1000) / 100.0)}\n");
        text.Append($"automatic={(car.Automatic ? 1 : 0)}\n");
        text.Append($"hud={(car.ShowDisplay ? 1 : 0)}\n");
        // (driving as another car -- on the island raced, its copy for there when it has one)
        if (((island is null ? null : car.DriveAsIslandKeys?.GetValueOrDefault(island)) ?? car.DriveAsKey) is { } drive) text.Append(drive);
        return text.ToString();
    }

    // The engine's drive_as= line for driving as race car `number` (RaceCarSetup.DriveAs) in a game folder: its BODY.HQR body, the racer
    // entity's driving animation (generic 1, as the opponents drive: RaceTrackScenes) and its shrunk body for an opponent's lightning
    // (RaceTrackSmallCars) where the folder has one. The number is the racer entity's body for the cars made by hand, on from
    // RaceTrackCharacterCars.CastFirst the cast's, as the folder's RACECARS.JSON lists them (a folder built before it: the entity table).
    // Null when the folder hasn't that car (built before it was made).
    // `violet`: the car's copy for the islands whose palette draws the violet, when it has one (DriveAsIslandLines).
    internal static string? DriveAsLine(string gameDirectory, int number, bool violet = false)
    {
        // (Twinsen's laser cars: his own buggy, its gun firing laser bolts instead of magic balls -- RACEMOD.CPP drive_laser=)
        if (number == RaceCarSetup.LaserGreen) return "# Twinsen's buggy firing green lasers\ndrive_laser=1\n";
        if (number == RaceCarSetup.LaserRed) return "# Twinsen's buggy firing red lasers\ndrive_laser=2\n";
        if (number < 0) return null;
        var racer = Lba2EntityTable.Load(gameDirectory)?.Entities.FirstOrDefault(e => e.Id == RaceTrackScenes.RacerEntity);
        if (racer is null) return null;
        var anim = racer.Anims.Where(a => a.Generic == 1).Select(a => (int?)a.Anim).FirstOrDefault();
        if (anim is null) return null;
        if (RaceTrackCharacterCars.Catalogue(gameDirectory).FirstOrDefault(e => e.Number == number) is { } listed)
        {
            // (the laser cars as a folder built on 2026-10-08 lists them: his buggy firing lasers now)
            if (listed.Name == RaceTrackCharacterCars.LaserGreenName) return DriveAsLine(gameDirectory, RaceCarSetup.LaserGreen);
            if (listed.Name == RaceTrackCharacterCars.LaserRedName) return DriveAsLine(gameDirectory, RaceCarSetup.LaserRed);
            if (violet && listed.Violet is [var body1, var small1])
                return $"# driving as car {number}, {listed.Name}: its violet-wheeled copy (the island's palette draws the violet)\ndrive_as={body1} {anim} {small1}\n";
            return $"# driving as car {number}, {listed.Name}\ndrive_as={listed.Body} {anim} {listed.Small}\n";
        }
        var body = racer.Bodies.Where(b => b.Generic == number).Select(b => (int?)b.Body).FirstOrDefault();
        if (body is null) return null;
        var small = racer.Bodies.Where(b => b.Generic == RaceTrackSmallCars.SmallOf(number)).Select(b => b.Body).DefaultIfEmpty(-1).First();
        return $"# driving as the racer's car body {number}\ndrive_as={body} {anim} {small}\n";
    }

    // The drive_as= lines for race car `number` that are not DriveAsLine's on some islands, by the island's name: a car with a copy for the
    // islands whose palette draws the violet (RACECARS.JSON's Violet: the toy truck's violet-wheeled copy) has that copy's on those islands
    // (RaceTrackCharacterCars.VioletIslands, from the folder's palettes: the Temple of Bú's). Null for any other car, or where none does.
    internal static Dictionary<string, string>? DriveAsIslandLines(string gameDirectory, int number)
    {
        if (number < 0 || RaceTrackCharacterCars.Catalogue(gameDirectory).FirstOrDefault(e => e.Number == number) is not { Violet: [_, _] }) return null;
        if (DriveAsLine(gameDirectory, number, violet: true) is not { } line) return null;
        var islands = RaceTrackCharacterCars.VioletIslands(gameDirectory);
        return islands.Count == 0 ? null : islands.ToDictionary(n => n, _ => line, StringComparer.OrdinalIgnoreCase);
    }

    // The cars the folder's tracks have for Twinsen of their own (RaceTrackCharacterCars.TrackCar: the Temple of Bú's pumpkin F1), each as
    // its drive_as= line (DriveAsLine) by its track's name, for a setup that leaves him his own car (RaceCarSetup.OwnBuggy); null for any
    // other car, or a folder with none (built before them).
    internal static Dictionary<string, string>? TrackCarLines(string gameDirectory, int driveAs)
    {
        var lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var island in RaceTrackIsland.All)
            if (RaceTrackCharacterCars.TrackCarFor(island.Name, driveAs) && RaceTrackCharacterCars.TrackCar(island.Name) is { } number && DriveAsLine(gameDirectory, number) is { } line)
                lines[island.Name] = $"# {island.Name}'s own car for Twinsen\n" + line;
        return lines.Count > 0 ? lines : null;
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
                    list.Add(Of(d.Name, d.Path, d.Grid, d.Top, d.Grip, d.Actors, d.Bike, d.Ghost, d.Main, d.Body) with { Hover = d.Hover });
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
    // (each track's file with its best setup, when the setup says so; the set's own handling, for an island with no track, the sliders')
    // (`fine`: the story in the weather once the storm is over, whatever the game's chapter -- Play from Citadel Island's town circuit file)
    internal static void WriteStorySet(this RaceCarSetup car, string path, RaceTrackService.TrackInfo info, bool fine = false)
    {
        var folder = Path.GetDirectoryName(path) ?? ".";
        var text = new StringBuilder("# LBA Assembler race car setup: the folder's tracks, each raced where and when the game is (RACEMOD.CPP track=)\n");
        text.Append(car.CarKeys());
        if (fine) text.Append("set_weather=fine\n");
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
                var (first, last) = island.CopiesScene is not null || island.CopiesScenes is not null || island.OwnScenes ? (island.FirstScene, island.LastScene) : (-1, -1);
                text.Append($"track={island.IslandByte} {when} {first} {last} {file}\n");
            }
            if (t.Story is { TiredText: >= 0 } s) text.Append($"tired={RaceTrackStory.DayVar} {RaceTrackStory.Tired} {s.TiredText}\n");
            // (a dreamt sprint: a new game starts in it, where its race starts -- Polar Island's dock -- and wakes up at home once it is won)
            if (t.Dream is not null && island.Dream && t.StartScene >= 0) text.Append($"dream={t.StartScene}\n");
        }
        File.WriteAllText(path, text.ToString());
    }
}
