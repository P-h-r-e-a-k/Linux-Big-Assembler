using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain.Polar;

// Polar Island on the holomap. HOLOMAP.HQR's island maps are a picture and a camera for each island from entry 18 (HQR_BEGIN_MAP), the
// retail file's last pair (12, 13) the fine-weather Citadel's and the Celebration's with its statue -- so island 12's are a pair appended
// after them, entries 46 and 47, which the engine reads for it (HOLOPLAN.CPP). The picture is the island's ground drawn through its camera
// (HolomapPicture) over a calm sea in the colours of the Citadel's picture. On the globe the island is record 12 of the positions
// (PolarScenes), its label a text of the game's file 2 (where the retail islands' are: 500, 510 .. 610) added for every language.
internal static class PolarHolomap
{
    public const int PictureEntry = RaceTrackHolomap.FirstMap + 2 * 14, CameraEntry = PictureEntry + 1;
    public const int LabelText = 620, GameTextFile = 2;
    private const int CitadelMap = RaceTrackHolomap.FirstMap + 2 * 12;
    // the camera: the middle of the island's cubes (5..9 x 4..10: x 7.5 cubes, z 7.5), looking down as the retail maps do, as far off as
    // the island is big (at LBA1's size, in cubes 6..8 x 5..8, it was 150,000 away)
    public static readonly int[] Camera = { 7, 7, 16384, 16384, 453, 1600, 280000, 309, 3368 };

    // `solids`: what the ground doesn't show, drawn as boxes (the rocky peak's columns: PolarScenes)
    public static List<string> Install(string gameDirectory, IslandFile island,
        IEnumerable<(double X0, double Z0, double X1, double Z1, double Y0, double Y1, (double R, double G, double B) Colour)>? solids = null)
    {
        var log = new List<string>();
        var holoPath = Path.Combine(gameDirectory, RaceTrackHolomap.File);
        var holo = HqrArchive.Open(holoPath);
        var ress0 = HqrArchive.Open(Path.Combine(gameDirectory, "RESS.HQR")).Read(0);
        var camera = new byte[Camera.Length * 4];
        for (var i = 0; i < Camera.Length; i++) BinaryPrimitives.WriteInt32LittleEndian(camera.AsSpan(i * 4), Camera[i]);
        var sea = HolomapPicture.SeaBackground(holo.Read(CitadelMap), ress0, 300, 435, 639, 479);
        var picture = HolomapPicture.Draw(island, IslandMapRenderer.LoadPalette(gameDirectory, "POLAR"), ress0[..768], camera, sea, solids);
        var file = HqrFile.Parse(File.ReadAllBytes(holoPath));
        foreach (var (entry, data) in new[] { (PictureEntry, picture), (CameraEntry, camera) })
        {
            while (file.Count < entry) file.Slots.Add(new HqrFile.Slot());
            if (file.Count == entry) file.Add(data); else file.SetStored(entry, data);
        }
        File.WriteAllBytes(holoPath, file.ToBytes());
        log.Add($"HOLOMAP.HQR: the island's map, entries {PictureEntry} (its picture) and {CameraEntry} (its camera)");

        // the label: text 620 of file 2 in every language (one the game doesn't have: the retail labels stop at 610)
        var textPath = Path.Combine(gameDirectory, "TEXT.HQR");
        var bytes = File.ReadAllBytes(textPath);
        for (var lang = 0; lang < PolarScenes.Languages; lang++)
        {
            var bank = Lba2TextBank.Load(HqrArchive.FromBytes(bytes), lang, GameTextFile);
            var text = bank.Find(LabelText);
            if (text is null) { text = new Lba2TextBank.Text { Id = LabelText }; bank.Texts.Add(text); }
            text.Attribute = bank.Find(LabelText - 10)?.Attribute ?? Lba2TextBank.NormalAttribute;
            text.Bytes = System.Text.Encoding.Latin1.GetBytes(Label);
            bytes = bank.WriteInto(bytes);
        }
        File.WriteAllBytes(textPath, bytes);
        log.Add($"TEXT.HQR: the island's label on the globe, text {LabelText} of file {GameTextFile}, \"{Label}\"");
        return log;
    }

    public const string Label = "Polar Island";
}
