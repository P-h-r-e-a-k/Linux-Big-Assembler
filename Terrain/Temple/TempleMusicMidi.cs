using System.IO;
using LBAAssembler.Lba1.Runtime;

namespace LBAAssembler.Terrain.Temple;

// LBA1's temple music as MIDI (the user, 2026-10-20: "We should add support for midi ... The original music files in LBA1 are stored in
// XMIDI (.xmi) format inside compressed archive files (MIDI_MI.HQR and MIDI_SB.HQR)"). Every LBA1 has its music as scores: MIDI_MI.HQR
// (General MIDI, for a MIDI device) and MIDI_SB.HQR (for a SoundBlaster's FM chip). LBA1 plays a score where it has no CD to play
// (native/lba1-classic AMBIANCE.C PlayMusic: PlayMidiFile(num) -- HQR_Get(HQR_Midi, num), the archive's entry `num` itself: music 5, the
// temple's, is MIDI_MI.HQR entry 5; Steam's recordings of the scores, Common\Midi\LBA1-06.mp3 for it, say the same). A copy of LBA1's data
// files alone -- the common case in players' reports of no music -- has no CD music, but has the score: the race's build writes it into the
// game folder as a Standard MIDI File (Lba1Xmi.ToMidi), RACEMUSIC\LBA1_TEMPLE.MID, which the race file names (music=) and the engine plays
// through the PC's MIDI synth (LIB386/AIL/SDL/MIDI.CPP: on Windows, its General MIDI synth -- as LBA1 Classic plays its MIDI music). A
// recording of the CD's music, found anywhere, always comes first (TempleMusic.Install).
internal static partial class TempleMusic
{
    public const string MidiArchiveName = "MIDI_MI.HQR";
    // (set: the LBA1s GOG and Steam installed on this PC aren't looked in -- a test of a copy of LBA1's data alone on a PC that has them)
    public const string EnvSkipInstalled = "LBA1_MUSIC_SKIP_INSTALLED";

    public static bool SkipInstalled => Environment.GetEnvironmentVariable(EnvSkipInstalled) is { Length: > 0 } v && v != "0";

    // The folders a MIDI_MI.HQR may be in, from a list of LBA1 folders: each, its Common (the Classic's data) and its Speedrun\Windows (GOG's
    // Classic's DOS version, whose Common has no scores), each once
    public static List<string> MidiPlaces(IEnumerable<string> dirs)
    {
        var places = new List<string>();
        foreach (var dir in dirs)
            foreach (var sub in new[] { "", "Common", Path.Combine("Speedrun", "Windows") })
            {
                var d = Path.Combine(dir, sub);
                if (Directory.Exists(d) && !places.Contains(Path.GetFullPath(d).TrimEnd('\\', '/'), StringComparer.OrdinalIgnoreCase)) places.Add(Path.GetFullPath(d).TrimEnd('\\', '/'));
            }
        return places;
    }

    // LBA1's music `music` as a Standard MIDI File: MIDI_MI.HQR's entry `music` from the first of `dirs` that has it. Null: none of them has
    // it (or it doesn't read). `notes`: what was found that can't be used.
    public static (byte[] Midi, string Archive)? MidiScore(IEnumerable<string> dirs, int music, List<string>? notes = null)
    {
        foreach (var dir in dirs)
        {
            var path = Path.Combine(dir, MidiArchiveName);
            if (!File.Exists(path)) continue;
            try
            {
                var archive = HqrArchive.Open(path);
                if (!archive.IsValid(music)) { notes?.Add($"{path}: no entry {music} in it"); continue; }
                if (Lba1Xmi.ToMidi(archive.Read(music)) is { Length: > 0 } midi) return (midi, path);
                notes?.Add($"{path}: its entry {music} isn't an XMIDI score");
            }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or UnauthorizedAccessException)
            {
                notes?.Add($"{path}: {error.Message}");
            }
        }
        return null;
    }

    // The score into the game folder (RACEMUSIC\LBA1_TEMPLE.MID) from the first of `dirs` (MidiPlaces) with it; `why`: why it is the score
    // and not a recording, for the log. False: none has it (what was found that can't be used, logged).
    private static bool InstallMidi(string folder, IEnumerable<string> dirs, int music, string why, List<string> log)
    {
        var notes = new List<string>();
        if (MidiScore(MidiPlaces(dirs), music, notes) is not { } score)
        {
            foreach (var n in notes) log.Add($"  ({n})");
            return false;
        }
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, Stem + ".MID"), score.Midi);
        var (seconds, count, _) = MidiFacts(score.Midi);
        log.Add($"The race's music: LBA1's music {music} as MIDI, from {score.Archive} (its entry {music}, the score LBA1 plays without its CD) -- {Minutes(seconds)}, " +
                $"{count} notes: {Folder}\\{Stem}.MID, played through the PC's MIDI synth. ({why}.)");
        foreach (var n in notes) log.Add($"  ({n})");
        return true;
    }

    // A Standard MIDI File's length and notes (its one track, as Lba1Xmi writes it: 120 ticks a second), for the log
    public static (double Seconds, int Notes, int Events) MidiFacts(byte[] smf)
    {
        try
        {
            if (smf.Length < 22 || smf[0] != 'M' || smf[1] != 'T' || smf[2] != 'h' || smf[3] != 'd') return (0, 0, 0);
            var division = smf[12] << 8 | smf[13];
            var p = 14;
            var notes = 0;
            var events = 0;
            var tempo = 500000.0;
            var seconds = 0.0;
            while (p + 8 <= smf.Length)
            {
                var length = smf[p + 4] << 24 | smf[p + 5] << 16 | smf[p + 6] << 8 | smf[p + 7];
                var q = p + 8;
                var stop = Math.Min(smf.Length, q + length);
                if (smf[p] != 'M' || smf[p + 1] != 'T' || smf[p + 2] != 'r' || smf[p + 3] != 'k') { p = stop; continue; }
                byte status = 0;
                int Vlq() { var v = 0; for (var i = 0; i < 4 && q < stop; i++) { var b = smf[q++]; v = v << 7 | (b & 0x7F); if ((b & 0x80) == 0) break; } return v; }
                while (q < stop)
                {
                    var delta = Vlq();
                    seconds += delta * tempo / 1e6 / Math.Max(1, division);
                    var b = smf[q];
                    if (b >= 0x80) { status = b; q++; }
                    if (status == 0xFF) { var type = smf[q++]; var len = Vlq(); if (type == 0x51 && len == 3) tempo = smf[q] << 16 | smf[q + 1] << 8 | smf[q + 2]; q += len; if (type == 0x2F) break; continue; }
                    if (status is 0xF0 or 0xF7) { q += Vlq(); continue; }
                    events++;
                    var kind = status & 0xF0;
                    if (kind == 0x90 && q + 1 < stop && smf[q + 1] > 0) notes++;
                    q += kind is 0xC0 or 0xD0 ? 1 : 2;
                }
                p = stop;
            }
            return (seconds, notes, events);
        }
        catch (IndexOutOfRangeException) { return (0, 0, 0); }
    }
}
