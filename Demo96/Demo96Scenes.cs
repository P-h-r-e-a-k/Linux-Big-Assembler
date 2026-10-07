using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Scenes;

namespace LBAAssembler.Demo96;

// The scenes of the 1996 LBA2 demo (LBA2DEMO.EXE's SCENE.HQR) in the retail game's layout. The record is the retail one but for two
// things, both found by lining demo scenes up against retail ones byte by byte (every one of the demo's 66 scenes parses this way to its
// last byte):
//
//   - an ambient sample slot is three words (sample, repeat, round): retail added a frequency and a volume (the retail scenes' unused
//     slots carry 4096 and 110, which the demo's are given);
//   - an actor carries 40 more bytes after its armour and life points, before its scripts (0xFF in most actors: what they meant is not
//     known; the retail engine has no use for them, so they are left out).
//
// The zones and the track points are the retail ones. The demo's records end with the track points: no patch table (the retail one is
// written fresh from the scripts, or left empty where they don't decode).
internal static class Demo96Scenes
{
    public const int AmbientFrequency = 4096, AmbientVolume = 110, ActorExtra = 40;

    public static SceneModel Parse(byte[] record)
    {
        var r = new Reader(record);
        var s = new SceneModel { Game = SceneGame.Lba2 };
        s.Island = r.U8(); s.CubeX = r.U8(); s.CubeY = r.U8();
        s.ShadowLevel = r.U8(); s.LabyrinthMode = r.U8(); s.CubeMode = r.U8(); s.HeaderSpare = r.U8();
        s.AlphaLight = r.S16(); s.BetaLight = r.S16();
        foreach (var a in s.Ambient) { a.Sample = r.S16(); a.Repeat = r.S16(); a.Round = r.S16(); a.Frequency = AmbientFrequency; a.Volume = AmbientVolume; }
        s.SecondMin = r.S16(); s.SecondEcart = r.S16();
        s.Music = r.S8();

        var hero = new SceneActorModel { X = r.S16(), Y = r.S16(), Z = r.S16() };
        hero.Track = r.Bytes(r.Length("hero track"));
        hero.Life = r.Bytes(r.Length("hero life"));
        s.Actors.Add(hero);

        var objects = r.S16();
        for (var n = 1; n < objects; n++)
        {
            var a = new SceneActorModel
            {
                Flags = r.U32(), Entity = r.S16(), Body = r.S8(), Anim = r.S16(), Sprite = r.S16(),
                X = r.S16(), Y = r.S16(), Z = r.S16(), HitForce = r.S8(), OptionFlags = r.S16(), Beta = r.S16(), SRot = r.S16(), Move = r.S8(),
            };
            for (var k = 0; k < 4; k++) a.Info[k] = r.S16();
            a.NbBonus = r.S16(); a.CoulObj = r.S8();
            a.Armor = r.S8(); a.LifePoints = r.S8();
            r.Bytes(ActorExtra);
            a.Track = r.Bytes(r.Length($"object {n} track"));
            a.Life = r.Bytes(r.Length($"object {n} life"));
            // (the retail ANIM_3DS flag would make the retail loader read two more fields the demo's record doesn't have)
            a.Flags &= ~Anim3dsFlag;
            s.Actors.Add(a);
        }

        s.Checksum = r.U32();
        var zones = r.S16();
        for (var i = 0; i < zones; i++)
        {
            var z = new SceneZoneModel { X0 = r.S32(), Y0 = r.S32(), Z0 = r.S32(), X1 = r.S32(), Y1 = r.S32(), Z1 = r.S32(), Info = new int[8] };
            for (var k = 0; k < 8; k++) z.Info[k] = r.S32();
            z.Type = r.S16(); z.Num = r.S16();
            s.Zones.Add(z);
        }
        var points = r.S16();
        for (var i = 0; i < points; i++) s.TrackPoints.Add(new SceneTrackPoint(r.S32(), r.S32(), r.S32()));
        if (r.Left != 0) throw new InvalidDataException($"The demo scene record has {r.Left} bytes past its track points.");
        return s;
    }

    // ANIM_3DS (COMMON.H): an actor drawn by a 3DS animation, whose retail record has two more fields
    private const uint Anim3dsFlag = 1u << 18;

    private sealed class Reader(byte[] b)
    {
        private int p;
        public int Left => b.Length - p;
        private void Need(int n) { if (p + n > b.Length) throw new InvalidDataException($"The demo scene record is truncated at byte {p}."); }
        public int U8() { Need(1); return b[p++]; }
        public int S8() { Need(1); return (sbyte)b[p++]; }
        public int S16() { Need(2); var v = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(p)); p += 2; return v; }
        public int S32() { Need(4); var v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p)); p += 4; return v; }
        public uint U32() { Need(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p)); p += 4; return v; }
        public byte[] Bytes(int n) { Need(n); var v = b[p..(p + n)]; p += n; return v; }
        public int Length(string what) { var n = S16(); return n >= 0 ? n : throw new InvalidDataException($"Negative {what} script length."); }
    }
}
