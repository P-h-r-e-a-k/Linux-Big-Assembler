using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's race in LBA1's own temple music (the user, 2026-10-18: "Our temple track currently uses the sound track from LBA2's Bu
// level, let's use the one from LBA1 instead"). LBA1's scenes 8 and 40 -- the temple, the crypt and the long corridor -- play its music 5
// (scene 41, the hall in front, its music 2: TempleScene says which scene's music is the race's). LBA1 plays music 1-9 off its CD's audio
// tracks, music N CD track N + 1 (the first track is the data: native/lba1-classic AMBIANCE.C PlayMusic, PlayCdTrack); the floppy release,
// and anything above 9, plays MIDI (MIDI_MI.HQR / MIDI_SB.HQR: a score, not a recording -- since 2026-10-20 the race plays it when no
// recording is found: TempleMusicMidi).
//
// The race track's build takes the CD track out of the player's own LBA1, trims the silence at its ends (the next track's two-second
// pregap: the race plays it again behind itself) and writes it into the game folder as a WAV (16-bit stereo 44.1 kHz, the CD's own format;
// the engine's STREAM.CPP streams WAV and OGG only), or copies an OGG, which the race file names (RaceTrackService.TrackInfo.Music; the
// engine's music=, RACEMOD.CPP). The music is never in the repo or an installer: it is made from the player's copy when the track is built.
// Without LBA1's audio (a floppy LBA1, a folder of its data with no disc image beside it) the race plays LBA1's score of it, MIDI_MI.HQR's
// (RACEMUSIC\LBA1_TEMPLE.MID: TempleMusicMidi, 2026-10-20) -- and with neither, LBA2's Temple of Bù's music, as before (the scenes' own:
// TempleTrack).
//
// How LBA1 is installed, and what each gives (2026-10-18, checked against the installs on the user's PC):
//   - GOG's DOS release ("Little Big Adventure", and "Little Big AdventureLBAWin"): LBA.DAT, a cue sheet, over LBA.GOG, a raw image of the
//     whole CD (2352-byte sectors; the cue lists no pregaps -- Cut finds them), which its DOSBox configs mount (dosboxLBA*.conf: imgmount d
//     "..\LBA.DAT", relative to DOSBox's working folder, DOSBOX). A copy named LBA.iso there is the same raw image; no cue names it.
//   - Steam's and GOG's "Twinsen's Little Big Adventure Classic" (2022): the data in Common (SCENE.HQR ...; TEXT.HQR in CommonClassic), the
//     CD's tracks as MP3s, Common\Music\Track_02.mp3 ... Track_10.mp3 (decoded here: TempleMusicAudio), and Common\Midi\LBA1-01 ... 33.mp3,
//     MIDI_MI.HQR's 33 scores as recordings (LBA1-NN is score NN - 1, i.e. music N is LBA1-(N+1); the CD's music among them is the CD's own
//     recording again, decoded the same: a last resort). GOG's Classic also has the DOS version in Speedrun\Windows: LBA.DAT over LBA.DOT,
//     the same raw image as LBA.GOG -- preferred to the MP3 (lossless).
//   - Rips: a .cue over .bin / .img / .wav / .mp3 / .ogg (one file or one per track), CloneCD's .ccd over its .img, loose tracks named
//     Track06 / Track 06 / Track_06 / LBA1_06 (anywhere looked) or "06 - <title>" / "LBA1 - 06" (in LBA1's own folders), in the folder, its
//     MUSIC or Common\Music folder. FLAC, Alcohol (.mds/.mdf), Nero (.nrg) and CHD aren't read (said in the log); a plain .iso (2048-byte
//     sectors) holds the data track only -- no audio, said too.
// The best of what is found wins: a CD image or WAV (lossless), then an OGG, then an MP3, then the MIDI folder's recording.
//
// Where it looks: LBA1_MUSIC_DIR (an installer that copies LBA1's data elsewhere names the player's LBA1 folder here), then the LBA1 folder
// the build is made from and the two above it (a disc's data folder inside its install: Steam's Common); and when none of those has it, the
// LBA1s GOG and Steam installed on this PC (InstalledLba1Folders, which LBRA's installer uses too).
internal static partial class TempleMusic
{
    // the LBA1 scene whose music the race plays (the temple: 8 and 40 share it)
    public const int TempleScene = 8;
    // the file in the game folder (as the race file names it: the game folder's, RACEMOD.CPP music=)
    public const string Folder = "RACEMUSIC";
    public const string Stem = "LBA1_TEMPLE";
    public const string EnvDirectory = "LBA1_MUSIC_DIR";
    private const int Sector = 2352, SectorsPerSecond = 75, FramesPerSector = 588, Rate = 44100, Channels = 2;
    // (a sample this quiet or quieter counts as silence at the track's ends: dither, the pregap)
    private const int Silence = 24;

    // The race's music, as the race file names it: the file a build put in the game folder (the WAV, or the OGG -- a recording -- else the
    // score, the MID: TempleMusicMidi), or null.
    public static string? Installed(string gameDirectory)
    {
        foreach (var ext in new[] { ".WAV", ".OGG", ".MID" })
            if (File.Exists(Path.Combine(gameDirectory, Folder, Stem + ext))) return $"{Folder}/{Stem}{ext}";
        return null;
    }

    // LBA1's music number for the temple (its scene's), from the LBA1 folder: 5 on every LBA1 we know of.
    public static int MusicNumber(string lba1Directory)
    {
        try { return Lba1Scene.Parse(TempleScene, HqrArchive.Open(Path.Combine(lba1Directory, "SCENE.HQR")).Read(TempleScene)).Music; }
        catch { return 5; }
    }

    // The music into the game folder from the player's LBA1. Returns lines for the build's log.
    public static List<string> Install(string gameDirectory, string lba1Directory)
    {
        var log = new List<string>();
        var music = MusicNumber(lba1Directory);
        var places = Places(lba1Directory);
        var folder = Path.Combine(gameDirectory, Folder);
        // (the LBA1s GOG and Steam installed on this PC, looked in when the build's own LBA1 hasn't the music -- not with LBA1_MUSIC_SKIP_INSTALLED)
        List<string>? installed = null;
        List<string> Installs() => installed ??= SkipInstalled ? new List<string>() : InstalledLba1Folders();
        if (music is < 1 or > 9)
        {
            // (no CD track: LBA1 plays it as MIDI wherever it is)
            var kept = Installed(gameDirectory);
            if (kept is not null && !kept.EndsWith(".MID", StringComparison.OrdinalIgnoreCase))
                log.Add($"The race's music: LBA1's temple plays music {music}, which LBA1 has only as MIDI: the {kept} an earlier build made plays");
            else if (!InstallMidi(folder, places.Concat(Installs()), music, $"LBA1's temple plays music {music}, which LBA1 has only as MIDI", log))
                log.Add($"The race's music: LBA1's temple plays music {music}, which LBA1 has only as MIDI, and no {MidiArchiveName} has it -- the race plays LBA2's Temple of Bù's music");
            return log;
        }
        var track = music + 1;
        var notes = new List<string>();
        var found = Find(places, track, notes);
        if (found is null)
        {
            // (a copy of LBA1's data files alone has no music -- the CD's music is its audio tracks, not files: the LBA1 GOG or Steam installed
            // on this PC gives it, the best of them)
            var others = Installs().SelectMany(Places).Distinct(StringComparer.OrdinalIgnoreCase).Where(d => !places.Contains(d, StringComparer.OrdinalIgnoreCase)).ToList();
            if (others.Count > 0 && Find(others, track) is { } elsewhere)
            {
                log.Add($"The race's music: LBA1's music {music} (CD track {track}) isn't with {lba1Directory} ({string.Join("; ", notes.DefaultIfEmpty("no CD image with its audio tracks, nor its track as a file"))}) " +
                        $"-- taken from the LBA1 installed on this PC");
                found = elsewhere;
            }
        }
        if (found is null)
        {
            var kept = Installed(gameDirectory);
            var why = $"LBA1's music {music} (CD track {track}, the temple's) isn't with this LBA1 -- no CD image with its audio tracks, nor its track as a file, " +
                      $"in {string.Join(", ", places)} ({EnvDirectory} names another folder to look in), nor in a GOG or Steam LBA1 on this PC" +
                      (SkipInstalled ? $" (not looked in: {EnvSkipInstalled})" : "");
            // (a recording an earlier build made still beats the score)
            if (kept is not null && !kept.EndsWith(".MID", StringComparison.OrdinalIgnoreCase))
                log.Add($"The race's music: {why}: the {kept} an earlier build made plays");
            else if (!InstallMidi(folder, places.Concat(Installs()), music, why, log))
                log.Add($"The race's music: {why}, nor its score ({MidiArchiveName}): the race plays LBA2's Temple of Bù's music");
            foreach (var n in notes) log.Add($"  ({n})");
            return log;
        }
        Directory.CreateDirectory(folder);
        // (a recording found: the score an earlier build wrote goes)
        Delete(Path.Combine(folder, Stem + ".MID"));
        var (source, pcm, ogg, lead) = found.Value;
        string written;
        if (ogg is not null)
        {
            written = Path.Combine(folder, Stem + ".OGG");
            File.Copy(ogg, written, true);
            Delete(Path.Combine(folder, Stem + ".WAV"));
            log.Add($"The race's music: LBA1's music {music} (the temple's, its scenes {TempleScene} and 40), CD track {track} -- {source} -- copied as {Folder}\\{Stem}.OGG");
            return log;
        }
        var (trimmed, note) = Cut(pcm!, lead);
        written = Path.Combine(folder, Stem + ".WAV");
        WriteWav(written, trimmed);
        Delete(Path.Combine(folder, Stem + ".OGG"));
        log.Add($"The race's music: LBA1's music {music} (the temple's, its scenes {TempleScene} and 40), CD track {track} -- {source} -- " +
                $"{Minutes(trimmed.Length / (Channels * 2) / (double)Rate)} ({note}): {Folder}\\{Stem}.WAV ({trimmed.Length / 1048576.0:0.0} MB)");
        return log;
    }

    // Where to look for LBA1's audio, each folder once: LBA1_MUSIC_DIR and the two folders above it, then the LBA1 folder and the two above
    // it (the Classic's Common: its install folder, with Common\Music and GOG's Speedrun\Windows image)
    public static List<string> Places(string lba1Directory)
    {
        var places = new List<string>();
        void Add(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            // (a drive's root keeps its backslash: "E:" alone is the drive's current folder)
            try { dir = Path.GetFullPath(dir).TrimEnd('\\', '/'); if (dir.EndsWith(':')) dir += Path.DirectorySeparatorChar; } catch { return; }
            if (Directory.Exists(dir) && !places.Contains(dir, StringComparer.OrdinalIgnoreCase)) places.Add(dir);
        }
        void AddWithParents(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
            Add(dir);
            try
            {
                var up = Directory.GetParent(Path.GetFullPath(dir).TrimEnd('\\', '/'));
                Add(up?.FullName);
                Add(up?.Parent?.FullName);
            }
            catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException) { }
        }
        AddWithParents(Environment.GetEnvironmentVariable(EnvDirectory));
        AddWithParents(lba1Directory);
        return places;
    }

    // (what a find gives, best first: a CD image's or a WAV's samples; an OGG; an MP3's; the MIDI folder's recording)
    private const int Lossless = 0, Vorbis = 1, Mp3 = 2, MidiRecording = 3;
    private sealed record Candidate(int Rank, int Place, string What, Func<(string, byte[]?, string?, int)?> Load);

    // CD track `track` from the best source in `places` (ties: the first place): where it came from, and its PCM (16-bit stereo 44.1 kHz,
    // little-endian) with the `Lead` frames before the cue's start of it (Cut finds the track's own ends in them) -- or an OGG file to copy as
    // it is. `notes`: what was found that can't be used, and why (an ISO with no audio, a FLAC, a DOSBox config's missing image ...).
    public static (string Source, byte[]? Pcm, string? Ogg, int Lead)? Find(IEnumerable<string> places, int track, List<string>? notes = null)
    {
        var candidates = new List<Candidate>();
        var sheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var images = new List<string>();
        var said = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Note(string what) { if (notes is not null && said.Add(what)) notes.Add(what); }
        var place = 0;
        foreach (var dir in places)
        {
            try { Gather(dir, place++, track, candidates, sheets, named, images, Note); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { DebugLog.Log($"TempleMusic: {dir}: {error.Message}"); }
        }
        // (an image no cue sheet names: a data-only ISO, or a raw image whose tracks nothing lists)
        foreach (var image in images.Where(i => !named.Contains(i)))
            if (DescribeImage(image) is { } said2) Note(said2);
        foreach (var c in candidates.OrderBy(c => c.Rank).ThenBy(c => c.Place))
        {
            try { if (c.Load() is { } got) return got; }
            catch (Exception error) { Note($"{c.What}: {error.Message}"); DebugLog.Log($"TempleMusic: {c.What}: {error}"); }
        }
        return null;
    }

    // What one folder has: cue sheets (GOG's LBA.DAT, *.cue), CloneCD sheets (*.ccd), DOSBox configs' imgmount lines (*.conf), the track as a
    // file (in the folder, MUSIC, Common\Music), the MIDI folder's recording (Common\Midi). A folder of LBA1's (its data in it, in Common or
    // in Speedrun\Windows -- or LBA1_MUSIC_DIR) is looked through more widely than one above it (a "Games" folder): its subfolders' sheets
    // and "06 - <title>" names count; elsewhere a CD image counts only when its data track is LBA1's.
    private static void Gather(string dir, int place, int track, List<Candidate> candidates, HashSet<string> sheets, HashSet<string> named, List<string> images, Action<string> note)
    {
        var own = HasLba1Data(dir) || SamePath(dir, Environment.GetEnvironmentVariable(EnvDirectory));
        var sheetDirs = new List<string> { dir };
        foreach (var sub in new[] { Path.Combine("Speedrun", "Windows") }) if (Directory.Exists(Path.Combine(dir, sub))) sheetDirs.Add(Path.Combine(dir, sub));
        if (own) foreach (var sub in Directory.EnumerateDirectories(dir).Take(64)) if (!sheetDirs.Contains(sub, StringComparer.OrdinalIgnoreCase)) sheetDirs.Add(sub);
        void Sheet(string sheet, bool trusted)
        {
            var full = Path.GetFullPath(sheet);
            if (!sheets.Add(full)) return;
            var tracks = Path.GetExtension(full).Equals(".ccd", StringComparison.OrdinalIgnoreCase) ? ParseCcd(full) : ParseCue(full);
            if (tracks is null) return;
            foreach (var t in tracks) if (Resolve(full, t.File, tracks) is { } file) named.Add(Path.GetFullPath(file));
            var it = tracks.FirstOrDefault(x => x.Number == track);
            if (it is null) { note($"{full}: no track {track} in it"); return; }
            if (!it.Audio) { note($"{full}: its track {track} isn't audio"); return; }
            var path = Resolve(full, it.File, tracks);
            if (path is null) { note($"{full}: its track {track}'s file {it.File} isn't there"); return; }
            if (!trusted && !IsLba1Disc(full, tracks)) { DebugLog.Log($"TempleMusic: {full}: not LBA1's CD (its data track has no LBA folder) -- passed over"); return; }
            var kind = it.FileType is "BINARY" or "MOTOROLA" ? "raw" : Sniff(path);
            var rank = kind switch { "raw" or "wav" => Lossless, "ogg" => Vorbis, "mp3" => Mp3, _ => -1 };
            if (rank < 0) { note($"{full}: its track {track} is in {Path.GetFileName(path)}, {(kind == "flac" ? "a FLAC file -- FLAC isn't read here (convert it to WAV or OGG)" : "which isn't WAV, OGG, MP3 or a raw image")}"); return; }
            candidates.Add(new Candidate(rank, place, $"{full} (track {track})", () => FromCue(full, tracks, track)));
        }
        foreach (var where in sheetDirs)
            foreach (var file in Directory.EnumerateFiles(where).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var ext = Path.GetExtension(file).ToUpperInvariant();
                switch (ext)
                {
                    case ".CUE" or ".DAT" or ".CCD":
                        try { Sheet(file, own); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException) { DebugLog.Log($"TempleMusic: {file}: {error.Message}"); }
                        break;
                    case ".CONF":
                        foreach (var (image, missing) in ConfImages(file))
                        {
                            if (missing) { note($"{file} mounts {image}, which isn't there"); continue; }
                            var iext = Path.GetExtension(image).ToUpperInvariant();
                            if (iext is ".CUE" or ".DAT" or ".CCD") { Sheet(image, own); continue; }
                            // (an image mounted on its own: its sheet beside it, if it has one -- else it is described with the others)
                            foreach (var s in new[] { ".cue", ".ccd" }.Select(e => Path.ChangeExtension(image, e)).Where(File.Exists)) Sheet(s, own);
                            if (!images.Contains(image, StringComparer.OrdinalIgnoreCase)) images.Add(image);
                        }
                        break;
                    case ".ISO" or ".BIN" or ".IMG" or ".GOG" or ".DOT":
                        if (own && !images.Contains(file, StringComparer.OrdinalIgnoreCase)) images.Add(file);
                        break;
                    case ".MDS" or ".MDF" or ".NRG" or ".CHD":
                        if (own) note($"{file}: {(ext is ".MDS" or ".MDF" ? "Alcohol" : ext == ".NRG" ? "Nero" : "CHD")} images aren't read here (a .cue/.bin of the disc is)");
                        break;
                }
            }
        // the track as a file of its own
        var musicDirs = new List<string?> { dir, Sub(dir, "Music"), Sub(Sub(dir, "Common"), "Music") };
        foreach (var where in musicDirs.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var file in Directory.EnumerateFiles(where).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var ext = Path.GetExtension(file).ToUpperInvariant();
                if (ext is not (".WAV" or ".OGG" or ".MP3" or ".FLAC") || !IsTrackName(Path.GetFileNameWithoutExtension(file), track, own)) continue;
                var kind = Sniff(file);
                switch (kind)
                {
                    case "wav": candidates.Add(new Candidate(Lossless, place, file, () => ReadWav(file) is { } pcm ? (file, pcm, null, 0) : null)); break;
                    case "ogg": candidates.Add(new Candidate(Vorbis, place, file, () => (file, null, file, 0))); break;
                    case "mp3": candidates.Add(new Candidate(Mp3, place, file, () => (file + " (MP3, decoded)", DecodeMp3(file), null, 0))); break;
                    case "flac": note($"{file}: FLAC isn't read here (convert it to WAV or OGG)"); break;
                    default: note($"{file}: not a WAV, OGG or MP3 file"); break;
                }
            }
        // the MIDI folder's recording (Steam's and GOG's Classic: Common\Midi\LBA1-NN.mp3, MIDI_MI.HQR's score NN - 1 -- music N is LBA1-(N+1),
        // i.e. LBA1-<the CD track>)
        if (own)
            foreach (var where in new[] { Sub(dir, "Midi"), Sub(Sub(dir, "Common"), "Midi") }.OfType<string>())
            {
                var file = Path.Combine(where, $"LBA1-{track:00}.mp3");
                if (File.Exists(file) && Sniff(file) == "mp3")
                    candidates.Add(new Candidate(MidiRecording, place, file, () => (file + " (the MIDI music's recording, MP3, decoded)", DecodeMp3(file), null, 0)));
            }
    }

    // Whether a file's name (without its extension) says it is CD track `track`: Track06, Track 06, Track_06, track6, LBA1_06 anywhere; and in
    // LBA1's own folders also "06 - <title>", "06", "LBA1 - 06", "<disc> (Track 06)"
    private static bool IsTrackName(string stem, int track, bool own)
    {
        var n = $"0*{track}";
        if (Regex.IsMatch(stem, $@"^(track|trk|lba1)[\s_\-.]*{n}$", RegexOptions.IgnoreCase)) return true;
        if (!own) return false;
        return Regex.IsMatch(stem, $@"^{n}([\s_\-.].*)?$", RegexOptions.IgnoreCase) || Regex.IsMatch(stem, $@"[\s_\-.(]{n}\)?$", RegexOptions.IgnoreCase);
    }

    // A folder's subfolder of that name in any case, as the disk spells it (null: none)
    private static string? Sub(string? dir, string name)
    {
        if (dir is null) return null;
        try { return Directory.EnumerateDirectories(dir).FirstOrDefault(d => Path.GetFileName(d).Equals(name, StringComparison.OrdinalIgnoreCase)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    // A folder with LBA1's data in it, in its Common folder (Steam's / GOG's Classic) or in Speedrun\Windows (GOG's Classic's DOS version)
    private static bool HasLba1Data(string dir)
    {
        foreach (var sub in new[] { "", "Common", Path.Combine("Speedrun", "Windows") })
        {
            var d = Path.Combine(dir, sub);
            if (File.Exists(Path.Combine(d, "SCENE.HQR")) && File.Exists(Path.Combine(d, "LBA_GRI.HQR"))) return true;
        }
        return false;
    }

    private static bool SamePath(string a, string? b)
    {
        if (string.IsNullOrWhiteSpace(b)) return false;
        try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private sealed record CueTrack(int Number, bool Audio, string Mode, string File, string FileType, int Index0, int Index1);

    private static List<CueTrack>? ParseCue(string sheet)
    {
        if (new FileInfo(sheet).Length > 65536) return null;
        var text = File.ReadAllText(sheet, Encoding.Latin1);
        if (!text.Contains("TRACK", StringComparison.OrdinalIgnoreCase) || !text.Contains("FILE", StringComparison.OrdinalIgnoreCase)) return null;
        var tracks = new List<CueTrack>();
        string file = "", type = "";
        CueTrack? current = null;
        static int Frames(string mmssff)
        {
            var p = mmssff.Split(':');
            return p.Length == 3 && int.TryParse(p[0], out var m) && int.TryParse(p[1], out var s) && int.TryParse(p[2], out var f) ? (m * 60 + s) * SectorsPerSecond + f : -1;
        }
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null) { tracks.Add(current); current = null; }
                var q0 = line.IndexOf('"'); var q1 = q0 >= 0 ? line.IndexOf('"', q0 + 1) : -1;
                if (q0 >= 0 && q1 > q0) { file = line[(q0 + 1)..q1]; type = line[(q1 + 1)..].Trim().ToUpperInvariant(); }
                else { var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); file = parts.Length > 1 ? parts[1] : ""; type = parts.Length > 2 ? parts[^1].ToUpperInvariant() : ""; }
            }
            else if (line.StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null) tracks.Add(current);
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var mode = parts.Length > 2 ? parts[2].ToUpperInvariant() : "";
                current = new CueTrack(parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : -1, mode == "AUDIO", mode, file, type, -1, -1);
            }
            else if (line.StartsWith("INDEX ", StringComparison.OrdinalIgnoreCase) && current is not null)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && int.TryParse(parts[1], out var which))
                    current = which == 0 ? current with { Index0 = Frames(parts[2]) } : which == 1 ? current with { Index1 = Frames(parts[2]) } : current;
            }
        }
        if (current is not null) tracks.Add(current);
        return tracks.Count > 0 ? tracks : null;
    }

    // CloneCD's sheet (.ccd: [TRACK n] MODE=0 is audio, INDEX 0=/INDEX 1= sectors) over its image (the sheet's name, .img: raw 2352-byte sectors)
    private static List<CueTrack>? ParseCcd(string sheet)
    {
        if (new FileInfo(sheet).Length > 262144) return null;
        var image = Path.GetFileName(Path.ChangeExtension(sheet, ".img"));
        var tracks = new List<CueTrack>();
        CueTrack? current = null;
        foreach (var raw in File.ReadAllLines(sheet, Encoding.Latin1))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                if (current is not null) tracks.Add(current);
                var m = Regex.Match(line, @"^\[TRACK\s+(\d+)\]$", RegexOptions.IgnoreCase);
                current = m.Success ? new CueTrack(int.Parse(m.Groups[1].Value), false, "", image, "BINARY", -1, -1) : null;
                continue;
            }
            if (current is null) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim().ToUpperInvariant();
            var ok = int.TryParse(line[(eq + 1)..].Trim(), out var value);
            if (key == "MODE" && ok) current = current with { Audio = value == 0, Mode = value == 0 ? "AUDIO" : value == 1 ? "MODE1/2352" : "MODE2/2352" };
            else if (key == "INDEX 0" && ok) current = current with { Index0 = value };
            else if (key == "INDEX 1" && ok) current = current with { Index1 = value };
        }
        if (current is not null) tracks.Add(current);
        return tracks.Count > 0 ? tracks : null;
    }

    // A sheet's file, beside it: by its name, by its name in another case, or -- the image renamed since, a sheet of one file -- the image
    // with the sheet's own name
    private static string? Resolve(string sheet, string name, List<CueTrack> tracks)
    {
        var dir = Path.GetDirectoryName(sheet)!;
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var path = Path.IsPathRooted(name) ? name : Path.Combine(dir, name);
            if (File.Exists(path)) return path;
            var file = Path.GetFileName(name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            var same = Directory.EnumerateFiles(dir).FirstOrDefault(f => Path.GetFileName(f).Equals(file, StringComparison.OrdinalIgnoreCase));
            if (same is not null) return same;
            if (tracks.Select(t => t.File).Distinct().Count() == 1)
                foreach (var ext in new[] { ".bin", ".img", ".gog" })
                    if (Path.ChangeExtension(sheet, ext) is var image && File.Exists(image)) return image;
        }
        catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException) { }
        return null;
    }

    private static (string, byte[]?, string?, int)? FromCue(string sheet, List<CueTrack> tracks, int number)
    {
        var t = tracks.FirstOrDefault(x => x.Number == number);
        if (t is null || !t.Audio || t.Index1 < 0) return null;
        var path = Resolve(sheet, t.File, tracks);
        if (path is null) return null;
        // (it ends where the next track in its file starts: its pregap, else its first sector; or at the file's end)
        var next = tracks.SkipWhile(x => x != t).Skip(1).FirstOrDefault(x => x.File == t.File);
        var end = next is null ? -1 : next.Index0 >= 0 ? next.Index0 : next.Index1;
        // ... and it is read from a little before the cue says it starts, when the track before it in its file is music too: GOG's LBA.DAT
        // lists no pregaps, and every track of its image starts 2 s before its INDEX 01 (Cut finds where). A sheet that lists the track's
        // pregap (INDEX 00: a rip's per-track file starts with it) has it read too, its silence trimmed -- LBA1's music begins in it.
        var previous = tracks.TakeWhile(x => x != t).LastOrDefault(x => x.File == t.File);
        var start = t.Index0 >= 0 && t.Index0 < t.Index1 ? t.Index0
            : previous is { Audio: true, Index1: >= 0 } ? Math.Max(previous.Index1, Math.Max(0, t.Index1 - LeadSeconds * SectorsPerSecond)) : t.Index1;
        var source = $"{sheet} over {Path.GetFileName(path)}";
        var lead = t.Index0 >= 0 && t.Index0 < t.Index1 ? 0 : (t.Index1 - start) * FramesPerSector;
        if (t.FileType is "BINARY" or "MOTOROLA")
        {
            using var f = File.OpenRead(path);
            var sectors = (int)(f.Length / Sector);
            var last = end >= 0 ? Math.Min(end, sectors) : sectors;
            if (last <= t.Index1) return null;
            var pcm = new byte[(long)(last - start) * Sector];
            f.Position = (long)start * Sector;
            f.ReadExactly(pcm);
            // (Red Book's samples are big-endian on the disc and a ripper is meant to swap them; not every one does: the order is measured)
            if (BigEndian(pcm)) Swap(pcm);
            return (source, pcm, null, lead);
        }
        switch (Sniff(path))
        {
            case "wav":
                return ReadWav(path, start, end) is { Length: > 0 } wav ? (source, wav, null, Math.Min(lead, wav.Length / 4)) : null;
            case "mp3":
            {
                var whole = DecodeMp3(path);
                var from = Math.Min(whole.Length, (long)start * Sector);
                var to = end >= 0 ? Math.Min(whole.Length, (long)end * Sector) : whole.Length;
                return to > from ? (source + " (MP3, decoded)", whole[(int)from..(int)to], null, (int)(Math.Min(whole.Length, (long)t.Index1 * Sector) - from) / 4) : null;
            }
            // (an OGG -- GOG's cue sheets call theirs MP3 -- only when the track is the whole file)
            case "ogg":
                return t.Index1 == 0 && next is null ? (source, null, path, 0) : null;
        }
        return null;
    }

    // Whether a sheet's data track is LBA1's CD: its ISO 9660 volume's root has LBA1's folder (LBA) or its CD_LBA file (the CD's root:
    // CD_LBA., INSTALL.BAT, LBA\; its volume ADELINE). Asked of the CD images found in folders above LBA1's own (any game's could be there).
    private static bool IsLba1Disc(string sheet, List<CueTrack> tracks)
    {
        try
        {
            var data = tracks.FirstOrDefault(t => !t.Audio && t.Index1 >= 0);
            if (data is null || Resolve(sheet, data.File, tracks) is not { } path) return false;
            int size, skip;
            if (data.Mode.EndsWith("/2048")) { size = 2048; skip = 0; }
            else if (data.Mode.StartsWith("MODE2")) { size = Sector; skip = 24; }
            else { size = Sector; skip = 16; }
            using var f = File.OpenRead(path);
            byte[] Read(long sector)
            {
                var b = new byte[2048];
                f.Position = (data.Index1 + sector) * size + skip;
                f.ReadExactly(b);
                return b;
            }
            var pvd = Read(16);
            if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001") return false;
            var extent = BinaryPrimitives.ReadInt32LittleEndian(pvd.AsSpan(156 + 2));
            var length = Math.Min(BinaryPrimitives.ReadInt32LittleEndian(pvd.AsSpan(156 + 10)), 8 * 2048);
            for (var s = 0; s * 2048 < length; s++)
            {
                var block = Read(extent + s);
                for (var p = 0; p < 2048 && block[p] > 0; p += block[p])
                {
                    var name = Encoding.ASCII.GetString(block, p + 33, Math.Min(block[p + 32], 2048 - p - 33)).ToUpperInvariant();
                    if (name is "LBA" || name.StartsWith("CD_LBA")) return true;
                }
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or EndOfStreamException or UnauthorizedAccessException) { }
        return false;
    }

    // An image no sheet names, said: a data-only ISO, a raw image with no sheet, or nothing to say
    private static string? DescribeImage(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            var head = new byte[16];
            if (f.Read(head, 0, 16) == 16 && head[0] == 0 && head.AsSpan(1, 10).ToArray().All(b => b == 0xFF) && head[11] == 0)
                return $"{path} is a raw CD image (2352-byte sectors) that no cue sheet names -- its tracks can't be told apart without one";
            if (f.Length > 0x8006)
            {
                f.Position = 0x8001;
                var id = new byte[5];
                f.ReadExactly(id);
                if (Encoding.ASCII.GetString(id) == "CD001")
                    return $"{path} is a data-only CD image (an ISO, 2048-byte sectors): it has no audio tracks, and LBA1's music is the CD's audio tracks " +
                           "(an image of the whole CD is a .cue over a .bin, as GOG's LBA.DAT over LBA.GOG)";
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or EndOfStreamException) { }
        return null;
    }

    // The images a DOSBox config mounts (imgmount <drive> <file> [<file> ...] [-t iso|cdrom ...]; floppies and hard disks left out): each
    // with whether it is missing. A relative path is DOSBox's working folder's -- GOG's is the DOSBOX folder beside the config (its launcher:
    // DOSBOX\dosbox.exe -conf "..\dosboxLBA.conf") -- or the config's own folder.
    private static IEnumerable<(string Image, bool Missing)> ConfImages(string conf)
    {
        if (new FileInfo(conf).Length > 1 << 20) yield break;
        var dir = Path.GetDirectoryName(Path.GetFullPath(conf))!;
        var bases = new List<string> { dir };
        try { bases.InsertRange(0, Directory.EnumerateDirectories(dir).Where(d => File.Exists(Path.Combine(d, "dosbox.exe")) || File.Exists(Path.Combine(d, "DOSBox.exe")))); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        foreach (var raw in File.ReadAllLines(conf, Encoding.Latin1))
        {
            var line = raw.Trim().TrimStart('@');
            if (!line.StartsWith("imgmount ", StringComparison.OrdinalIgnoreCase)) continue;
            var words = Words(line);
            var type = "";
            for (var i = 0; i + 1 < words.Count; i++) if (words[i].Equals("-t", StringComparison.OrdinalIgnoreCase)) type = words[i + 1].ToLowerInvariant();
            if (type is "floppy" or "hdd") continue;
            foreach (var file in words.Skip(2).TakeWhile(w => !w.StartsWith('-')))
            {
                string? found = null;
                foreach (var b in Path.IsPathRooted(file) ? new[] { "" } : bases.ToArray())
                {
                    try { var p = Path.GetFullPath(Path.Combine(b, file)); if (File.Exists(p)) { found = p; break; } }
                    catch (Exception error) when (error is ArgumentException or NotSupportedException) { }
                }
                yield return found is not null ? (found, false) : (file, true);
            }
        }
    }

    // A config line's words, "quoted" ones whole
    private static List<string> Words(string line)
    {
        var words = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            if (i >= line.Length) break;
            if (line[i] == '"')
            {
                var close = line.IndexOf('"', i + 1);
                if (close < 0) close = line.Length;
                words.Add(line[(i + 1)..close]);
                i = close + 1;
            }
            else
            {
                var start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                words.Add(line[start..i]);
            }
        }
        return words;
    }

    // (how far before a track's INDEX 01 it is read from; a run of silence this long or longer is a gap between tracks)
    private const int LeadSeconds = 4;
    private const double GapSeconds = 1.0;

    // A track read with `lead` frames before its cue's start: from the end of the last gap in those (its pregap: the music starts after
    // it) to the start of the last gap in its last LeadSeconds (the next track's pregap, with the next track's start after it when the cue
    // is off), its silent ends trimmed. Returns the samples and what was cut, said.
    public static (byte[] Pcm, string Note) Cut(byte[] pcm, int lead)
    {
        var frames = pcm.Length / 4;
        var gap = (int)(GapSeconds * Rate);
        bool Quiet(int frame) => Math.Abs((short)(pcm[4 * frame] | pcm[4 * frame + 1] << 8)) <= Silence && Math.Abs((short)(pcm[4 * frame + 2] | pcm[4 * frame + 3] << 8)) <= Silence;
        // the runs of silence (first frame, frames) in [from, to)
        List<(int At, int Length)> Gaps(int from, int to)
        {
            var runs = new List<(int, int)>();
            var run = 0;
            for (var f = Math.Max(0, from); f < Math.Min(frames, to); f++)
            {
                if (Quiet(f)) { run++; continue; }
                if (run >= gap) runs.Add((f - run, run));
                run = 0;
            }
            if (run >= gap) runs.Add((Math.Min(frames, to) - run, run));
            return runs;
        }
        var first = lead;
        var head = Gaps(0, lead + 1).LastOrDefault();
        if (head.Length > 0) first = head.At + head.Length;
        var tailFrom = Math.Max(first, frames - LeadSeconds * Rate);
        var last = frames;
        var tail = Gaps(tailFrom, frames).LastOrDefault();
        if (tail.Length > 0) last = tail.At;
        var cut = Trim(pcm[(first * 4)..(last * 4)]);
        var before = (lead - first) / (double)Rate;
        var note = $"{(before > 0.01 ? $"its music starts {before:0.00} s before the cue's INDEX 01 -- its pregap isn't in the cue sheet; " : before < -0.01 ? $"{-before:0.00} s of silence after the cue's INDEX 01 left out; " : "")}" +
                   $"{(last < frames ? $"{(frames - last) / (double)Rate:0.00} s after its end left out (the gap{(tail.At + tail.Length < frames ? " and the next track's start" : "")}); " : "")}" +
                   $"silent ends trimmed: {(pcm.Length - cut.Length) / 4 / (double)Rate:0.00} s off in all";
        return (cut, note);
    }

    // Whether 16-bit samples read smoother big-endian than little-endian (music changes little from one sample to the next; the wrong
    // order makes noise of it)
    private static bool BigEndian(byte[] pcm)
    {
        long le = 0, be = 0;
        var n = Math.Min(pcm.Length / 2, 2_000_000);
        for (var i = 2; i < n; i++)
        {
            int a = (short)(pcm[2 * i] | pcm[2 * i + 1] << 8), b = (short)(pcm[2 * i - 4] | pcm[2 * i - 3] << 8);
            int c = (short)(pcm[2 * i] << 8 | pcm[2 * i + 1]), e = (short)(pcm[2 * i - 4] << 8 | pcm[2 * i - 3]);
            le += Math.Abs(a - b); be += Math.Abs(c - e);
        }
        return be < le;
    }

    private static void Swap(byte[] pcm)
    {
        for (var i = 0; i + 1 < pcm.Length; i += 2) (pcm[i], pcm[i + 1]) = (pcm[i + 1], pcm[i]);
    }

    // The samples without the silence at their ends (whole stereo frames)
    public static byte[] Trim(byte[] pcm)
    {
        var frames = pcm.Length / 4;
        bool Loud(int frame) => Math.Abs((short)(pcm[4 * frame] | pcm[4 * frame + 1] << 8)) > Silence || Math.Abs((short)(pcm[4 * frame + 2] | pcm[4 * frame + 3] << 8)) > Silence;
        var first = 0;
        while (first < frames && !Loud(first)) first++;
        var last = frames - 1;
        while (last > first && !Loud(last)) last--;
        return last <= first ? pcm : pcm[(first * 4)..((last + 1) * 4)];
    }

    public static void WriteWav(string path, byte[] pcm)
    {
        var header = new byte[44];
        void Text(int at, string s) => Encoding.ASCII.GetBytes(s).CopyTo(header, at);
        Text(0, "RIFF"); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 36 + pcm.Length); Text(8, "WAVE");
        Text(12, "fmt "); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(20), 1); BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(22), Channels);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), Rate); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), Rate * Channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(32), Channels * 2); BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(34), 16);
        Text(36, "data"); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(40), pcm.Length);
        using var f = File.Create(path);
        f.Write(header);
        f.Write(pcm);
    }

    private static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static string Minutes(double seconds) => $"{(int)(seconds / 60)}:{seconds % 60:00.0}";
}
