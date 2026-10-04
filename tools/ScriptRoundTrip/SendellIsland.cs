using LBAAssembler;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// Sendell's Well: island number 1 in the engine's island list ("sendell"), an outside island the game was to have and that was cut --
// no SENDELL.ILE ships and no scene has island 1. A proof that the game takes a new one.
//   sendell build <pristine folder> <game folder>            SENDELL.ILE/OBL, island 1's sky, palette and holomap picture, its scene (224:
//                                                            Terrain/SendellWell.cs, as the race track build makes them), and a way there
//                                                            and back from Citadel Island's Sendell's sign (the game folder's SCENE.HQR,
//                                                            RESS.HQR and HOLOMAP.HQR must be the originals)
//   sendell tiles <pristine folder> [ISLAND]                  the ground atlas rectangles an island uses most, and their colours
//   sendell inspect <pristine folder> <cube x> <cube z>     heights, ground classes and decors of one cube of CITADEL.ILE
internal static class SendellIsland
{
    public static int Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "build") return Build(args[2], args[3]);
        if (args.Length > 1 && args[1] == "tiles") return Tiles(args[2], args.Length > 3 ? args[3] : "CITABAU");
        return args.Length > 1 && args[1] == "inspect" ? Inspect(args[2], int.Parse(args[3]), int.Parse(args[4])) : Usage();
    }

    // The atlas rectangles an island's textured ground triangles use most, with their colour and class.
    private static int Tiles(string pristine, string name)
    {
        var island = IslandFile.Load(Path.Combine(pristine, name + ".ILE"));
        var palette = IslandMapRenderer.LoadPalette(pristine, name);
        var counts = new Dictionary<(int X, int Y, int W, int H), (int N, long R, long G, long B)>();
        foreach (var cube in island.Cubes.Values)
            for (var z = 0; z < 64; z++)
            for (var x = 0; x < 64; x++)
            for (var half = 0; half < 2; half++)
            {
                var p = new IslandPolygon(cube.Polygon(x, z, half));
                if (p.TexFlag == 0 || p.CodeJeu != 0) continue;
                var i = p.TextureIndex;
                if (i * 6 + 6 > cube.TextureDefs.Length) continue;
                var us = new[] { cube.TextureDefs[i * 6], cube.TextureDefs[i * 6 + 2], cube.TextureDefs[i * 6 + 4] };
                var vs = new[] { cube.TextureDefs[i * 6 + 1], cube.TextureDefs[i * 6 + 3], cube.TextureDefs[i * 6 + 5] };
                var rect = (us.Min() / 256, vs.Min() / 256, (us.Max() + 128) / 256 - us.Min() / 256, (vs.Max() + 128) / 256 - vs.Min() / 256);
                var c = TriangleColour(island, cube, x, z, half, palette) ?? (0, 0, 0);
                counts.TryGetValue(rect, out var e);
                counts[rect] = (e.N + 1, e.R + c.R, e.G + c.G, e.B + c.B);
            }
        foreach (var (rect, e) in counts.OrderByDescending(p => p.Value.N).Take(40))
        {
            var colour = ((int)(e.R / e.N), (int)(e.G / e.N), (int)(e.B / e.N));
            Console.WriteLine($"{rect.X,3},{rect.Y,3} {rect.W,2}x{rect.H,-2} used {e.N,6}  colour {colour}  {Class(colour)}");
        }
        return 0;
    }

    private static int Usage()
    {
        Console.WriteLine("sendell build <pristine folder> <game folder> | tiles <pristine folder> [ISLAND] | inspect <pristine folder> <cube x> <cube z>");
        return 1;
    }

    // ---- the island: Terrain/SendellWell.cs (the race track build makes it too), with a way there and back for the test ----
    // Citadel Island's Sendell's sign: scene 47, cube (8, 7), the circle's middle (cube-local world units)
    private const int SignScene = 47, SignCubeX = 8, SignCubeZ = 7, SignX = 7168, SignZ = 16000;

    private static int Build(string pristine, string game)
    {
        // the island's files from the folder's originals (Citadel's fine-weather island, RESS.HQR, HOLOMAP.HQR, SCENE.HQR)
        foreach (var f in new[] { "CITABAU.ILE", "CITABAU.OBL" })
            if (!File.Exists(Path.Combine(game, f))) RaceTrackService.CopyWritable(Path.Combine(pristine, f), Path.Combine(game, f));
        foreach (var l in SendellWell.Install(game)) Console.WriteLine(l);
        var model = SendellWell.AddScene(game);
        Console.WriteLine($"SCENE.HQR: scene {SendellWell.Scene} (island 1, cube {SendellWell.CubeX},{SendellWell.CubeZ}), Twinsen at ({model.Hero.X},{model.Hero.Y},{model.Hero.Z})");

        // A way there and back for the test: the Sendell's sign on Citadel Island (scene 47, a circle of flowers with an S in it) sends
        // Twinsen to the rim of the well, and a patch of the new island's south shore sends him back beside the sign. Cube-change zones:
        // the engine carries his place in the zone over (OBJECT.CPP GereZoneChangeCube: arrival = Info0..2 + where he is in the zone).
        var citadel = IslandFile.Load(Path.Combine(pristine, "CITADEL.ILE"));
        int CitadelHeight(double lx, double lz) => (int)Math.Round(IslandOps.Altitude(citadel, SignCubeX * 32768.0 + lx, SignCubeZ * 32768.0 + lz) ?? 0);
        LBAAssembler.Scenes.SceneZoneModel Portal(double cx, double cz, int ground, int toScene, double ax, double ay, double az)
        {
            var zone = new LBAAssembler.Scenes.SceneZoneModel
            {
                Type = 0, Num = toScene, Info = new int[8],
                X0 = (int)cx - 512, X1 = (int)cx + 511, Z0 = (int)cz - 512, Z1 = (int)cz + 511, Y0 = ground - 256, Y1 = ground + 2048,
            };
            zone.Info[0] = (int)ax - 512; zone.Info[1] = (int)ay - 256; zone.Info[2] = (int)az - 512; zone.Info[7] = 1;
            return zone;
        }
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, game);
        var sign = store.Load(SignScene);
        var signGround = CitadelHeight(SignX, SignZ);
        LBAAssembler.Scenes.SceneOps.AddZone(sign, Portal(SignX, SignZ, signGround, SendellWell.Scene, model.Hero.X, model.Hero.Y, model.Hero.Z));
        // (the way back: south of the rim, on the slope above the beach; arriving east of the sign's circle)
        double backX = 32 * 512, backZ = (32 + 20) * 512;
        var backGround = (int)Math.Round(SendellWell.HeightAt(0, 20));
        double arriveX = SignX + 8.5 * 512, arriveZ = SignZ;
        var well = store.Load(SendellWell.Scene);
        well.Zones.Add(Portal(backX, backZ, backGround, SignScene, arriveX, CitadelHeight(arriveX, arriveZ), arriveZ));
        store.Save(SignScene, sign);
        store.Save(SendellWell.Scene, well);
        Console.WriteLine($"the way there: a cube-change zone to scene {SendellWell.Scene} on the Sendell's sign (scene {SignScene}, at {SignX},{signGround},{SignZ}); the way back: one to scene {SignScene} on the island's south slope (at {backX},{backGround},{backZ}), arriving east of the sign");
        return 0;
    }

    // The colour a ground triangle shows: the mean of the atlas pixels its texture corners span, in the island's palette.
    internal static (int R, int G, int B)? TriangleColour(IslandFile island, IslandCube cube, int x, int z, int half, byte[] palette)
    {
        var polygon = new IslandPolygon(cube.Polygon(x, z, half));
        if (polygon.TexFlag == 0) return null;
        var i = polygon.TextureIndex;
        if (i * 6 + 6 > cube.TextureDefs.Length) return null;
        var u = new double[3]; var v = new double[3];
        for (var k = 0; k < 3; k++) { u[k] = cube.TextureDefs[i * 6 + k * 2] / 256.0; v[k] = cube.TextureDefs[i * 6 + k * 2 + 1] / 256.0; }
        long r = 0, g = 0, b = 0; var n = 0;
        // (sample the triangle on a small barycentric grid)
        for (var a = 0; a <= 6; a++)
        for (var c = 0; c <= 6 - a; c++)
        {
            double wa = a / 6.0, wc = c / 6.0, wb = 1 - wa - wc;
            var px = (int)Math.Clamp(u[0] * wa + u[1] * wb + u[2] * wc, 0, 255);
            var py = (int)Math.Clamp(v[0] * wa + v[1] * wb + v[2] * wc, 0, 255);
            var index = island.GroundTexture[py * 256 + px];
            r += palette[index * 3]; g += palette[index * 3 + 1]; b += palette[index * 3 + 2]; n++;
        }
        return ((int)(r / n), (int)(g / n), (int)(b / n));
    }

    // grass, rock, sand, earth or water, from a colour
    internal static char Class((int R, int G, int B)? colour)
    {
        if (colour is not { } c) return '.';
        var (r, g, b) = c;
        if (b > r + 20 && b > g) return '~';
        if (g > r + 8 && g > b + 8) return 'g';
        if (r > 150 && g > 120 && b < 110 && r - b > 50) return 's';
        if (r > g + 15 && g > b) return 'e';
        return 'r';
    }

    private static int Inspect(string pristine, int cubeX, int cubeZ)
    {
        var island = IslandFile.Load(Path.Combine(pristine, "CITADEL.ILE"));
        var palette = IslandMapRenderer.LoadPalette(pristine, "CITADEL");
        var cube = island.CubeAt(cubeX, cubeZ) ?? throw new InvalidDataException("no cube there");
        Console.WriteLine($"cube ({cubeX},{cubeZ}) id {cube.Id}: {cube.Decors.Count} decors, {cube.TextureDefs.Length / 6} texture definitions, heights {cube.Heights.Min()}..{cube.Heights.Max()}");
        foreach (var d in cube.Decors) Console.WriteLine($"  decor body {d.Body} at ({d.X},{d.Y},{d.Z}) box x {d.XMin}..{d.XMax} y {d.YMin}..{d.YMax} z {d.ZMin}..{d.ZMax}");
        Console.WriteLine("heights / 100, every other vertex:");
        for (var z = 0; z <= 64; z += 2)
        {
            var line = "";
            for (var x = 0; x <= 64; x += 2) line += Math.Clamp(cube.Height(x, z) / 100, -9, 99).ToString().PadLeft(3);
            Console.WriteLine(line);
        }
        Console.WriteLine("ground classes (g grass, r rock, s sand, e earth, ~ water, . untextured), code jeu where not 0 as a digit:");
        for (var z = 0; z < 64; z++)
        {
            var line = "";
            for (var x = 0; x < 64; x++)
            {
                var p = new IslandPolygon(cube.Polygon(x, z, 0));
                line += p.CodeJeu != 0 ? (char)('0' + Math.Min(9, p.CodeJeu)) : Class(TriangleColour(island, cube, x, z, 0, palette));
            }
            Console.WriteLine(line);
        }
        return 0;
    }
}
