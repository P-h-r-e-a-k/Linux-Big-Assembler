using System.IO;

namespace LBAAssembler;

// A name for every body of an LBA2 game folder's BODY.HQR -- the retail ones from the reference descriptions (BODY2.HQD), and the ones
// added to the folder since: what the folder's own BODY.HQD sidecar says (HqdWriter: the editor's own new bodies), the race cars and their
// half-size copies (RACECARS.JSON: Terrain.RaceTrackCharacterCars), and any other as the body it is of its kind of actor (the entity
// table, RESS.HQR 44: "body 1 of the Mushroom's kind of actor" -- the oil slick). Null: nothing known (shown as the bare number).
internal static class Lba2BodyNames
{
    public sealed record Result(IReadOnlyList<string?> Names, string? Warning);

    public static Result For(string gameDirectory)
    {
        var count = 0;
        try { count = HqrArchive.CountEntries(Path.Combine(gameDirectory, "BODY.HQR")); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { }
        var reference = HqdDescriptions.Load("BODY2.HQD", count);
        var names = new string?[Math.Max(count, reference.Names.Count)];
        for (var i = 0; i < reference.Names.Count && i < names.Length; i++) names[i] = reference.Names[i];

        // the folder's own sidecar (seeded from the reference, so it names the retail bodies the same)
        var sidecar = Path.Combine(gameDirectory, HqdWriter.SidecarName("BODY.HQR"));
        if (File.Exists(sidecar))
        {
            try
            {
                var lines = File.ReadAllLines(sidecar, System.Text.Encoding.Latin1);
                for (var line = 1; line < lines.Length && line - 1 < names.Length; line++)
                    if (lines[line].Trim().Length > 0) names[line - 1] = lines[line].Trim();
            }
            catch (IOException) { }
        }

        // the race cars (a folder whose race tracks are built since 2026-10-05)
        foreach (var car in Terrain.RaceTrackCharacterCars.Catalogue(gameDirectory))
        {
            var who = car.Driver.Length > 0 ? $", driven by {car.Driver}" : "";
            if (car.Body >= 0 && car.Body < names.Length && names[car.Body] is null) names[car.Body] = $"Race car {car.Number}: {car.Name}{who}";
            if (car.Small >= 0 && car.Small < names.Length && names[car.Small] is null) names[car.Small] = $"Race car {car.Number} at half size: {car.Name}";
        }

        // the rest: the race tracks' other bodies by what they are, any other as a body of its kind of actor, named after that kind's first
        // named body
        if (names.Any(n => n is null) && Lba2EntityTable.Load(gameDirectory) is { } table)
        {
            var known = new Dictionary<(int Entity, int Generic), string>
            {
                [(Terrain.RaceTrackOil.Entity, Terrain.RaceTrackOil.Generic)] = "Oil slick (the race tracks' oil power-up)",
                [(Terrain.RaceTrackSmallCars.HeroEntity, Terrain.RaceTrackSmallCars.HeroSmall)] = "Twinsen's buggy at half size (an opponent's lightning on the race tracks)",
                [(Terrain.RaceTrackStory.DartsEntity, 1)] = "Racing gloves (the Citadel Island race track's story)",
            };
            // (a folder built before RACECARS.JSON: the racer entity's cars made by hand, as the program knows them)
            var racer = Terrain.RaceTrackBaldinoCar.RacerEntity;
            known.TryAdd((racer, 1), "Race car 1: Baldino's rocket car, driven by Jerome Baldino");
            known.TryAdd((racer, Terrain.RaceTrackSmallCars.SmallOf(0)), "Race car 0 at half size: The racer's car");
            known.TryAdd((racer, Terrain.RaceTrackSmallCars.SmallOf(1)), "Race car 1 at half size: Baldino's rocket car");
            foreach (var car in Terrain.RaceTrackCharacterCars.All)
            {
                known.TryAdd((racer, car.Generic), $"Race car {car.Generic}: {car.Name}, driven by {car.Driver}");
                known.TryAdd((racer, Terrain.RaceTrackSmallCars.SmallOf(car.Generic)), $"Race car {car.Generic} at half size: {car.Name}");
            }
            foreach (var entity in table.Entities)
            {
                var kind = entity.Bodies.OrderBy(b => b.Generic).Select(b => b.Body < names.Length ? names[b.Body] : null).FirstOrDefault(n => n is not null);
                foreach (var (generic, body) in entity.Bodies)
                {
                    if (body < 0 || body >= names.Length || names[body] is not null) continue;
                    names[body] = known.TryGetValue((entity.Id, generic), out var what) ? what
                        : kind is not null ? $"{Plain(kind)}: body {generic} of its kind of actor (entity {entity.Id})" : $"Body {generic} of entity {entity.Id}";
                }
            }
        }

        // (the reference check: its count against the archive's, past what the folder's own names account for)
        var unnamed = names.Skip(reference.Names.Count).Count(n => n is null);
        var warning = count > 0 && unnamed > Math.Max(5, count / 10) ? reference.ValidationWarning : null;
        return new Result(names, warning);
    }

    // A body's name as the name of its kind of actor: without what it was the first of ("Race car 0: ..." -> the car's name).
    private static string Plain(string name) => name.StartsWith("Race car ", StringComparison.Ordinal) && name.IndexOf(": ", StringComparison.Ordinal) is var colon and > 0 ? name[(colon + 2)..] : name;
}
