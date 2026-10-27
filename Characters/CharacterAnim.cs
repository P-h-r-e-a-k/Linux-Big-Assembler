using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Characters;

// A character's animation in the Character maker: keyframes of the body's bones -- each keyframe how long it takes to reach (milliseconds:
// LBA2's keyframe time -- its Twinsen walks a keyframe in 200), how far the character moves over it, and each bone's turn from its rest (degrees about x, y and z: LBA2's alpha, beta and
// gamma) or, for a bone that shifts instead, its shift. Read from and written as ANIM.HQR entries (LbaBodyStudio.Anim: turns, 4096 a turn
// in LBA2), posed for the preview the way the game poses a body (Lba1Pose: about x, then z, then y, about each bone's pivot).
internal sealed class CharacterAnim
{
    public sealed class Key
    {
        public int Milliseconds = 200;
        public Vector3 Step;
        // a bone slot each: slot 0 the whole character's own turn (as the game, a master record), 1 on the body's bones 1 on
        public List<(int Type, Vector3 Value)> Bones = new();
        public Key Copy() => new() { Milliseconds = Milliseconds, Step = Step, Bones = new List<(int, Vector3)>(Bones) };
    }

    public int Generic;
    public string Name = "";
    public int LoopFrame;
    public List<Key> Keys = new();

    // A still animation: one keyframe of the body at rest.
    public static CharacterAnim Still(int bones, int generic, string name) => new()
    {
        Generic = generic, Name = name,
        Keys = { new Key { Milliseconds = 1000, Bones = Enumerable.Range(0, bones).Select(_ => (0, Vector3.Zero)).ToList() } },
    };

    // An ANIM.HQR entry (LBA2: angles 4096 a turn, shown here in degrees), its bone slots made `bones` (a slot it lacks keeps still; one
    // past them is dropped).
    public static CharacterAnim Read(byte[] data, int bones, int generic, string name)
    {
        if (data.Length < 8) throw new InvalidDataException("Not an animation.");
        int frames = BinaryPrimitives.ReadUInt16LittleEndian(data), slots = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
        var anim = new CharacterAnim { Generic = generic, Name = name, LoopFrame = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4)) };
        var p = 8;
        static double Degrees(short a) => ((a & 0xFFF) - ((a & 0x800) != 0 ? 4096 : 0)) * 360.0 / 4096;
        for (var f = 0; f < frames && p + 8 + slots * 8 <= data.Length; f++)
        {
            var key = new Key
            {
                Milliseconds = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p)),
                Step = new Vector3(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p + 2)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p + 4)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p + 6))),
            };
            p += 8;
            for (var b = 0; b < slots; b++, p += 8)
            {
                if (b >= bones) continue;
                int type = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p));
                short x = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p + 2)), y = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p + 4)), z = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p + 6));
                key.Bones.Add(b > 0 && (type & 1) == 0 ? (type, new Vector3((float)Degrees(x), (float)Degrees(y), (float)Degrees(z))) : (type, new Vector3(x, y, z)));
            }
            while (key.Bones.Count < bones) key.Bones.Add((0, Vector3.Zero));
            anim.Keys.Add(key);
        }
        if (anim.Keys.Count == 0) throw new InvalidDataException("The animation has no keyframes.");
        anim.LoopFrame = Math.Clamp(anim.LoopFrame, 0, anim.Keys.Count - 1);
        return anim;
    }

    // As an ANIM.HQR entry for a body of `bones` bones.
    public byte[] Write(int bones)
    {
        var anim = new Anim { Game = 2, LoopFrame = Math.Clamp(LoopFrame, 0, Keys.Count - 1) };
        foreach (var k in Keys)
        {
            var slots = new AnimBone[bones];
            for (var b = 0; b < bones; b++)
            {
                var (type, v) = b < k.Bones.Count ? k.Bones[b] : (0, Vector3.Zero);
                // (a shifting bone's values are raw units; slot 0, the master record, keeps the game's raw values -- its angles 4096 a turn;
                // a turning bone's are degrees, made turns)
                slots[b] = (type & 1) != 0 ? new AnimBone(type, v.X, v.Y, v.Z)
                    : b == 0 ? new AnimBone(type, v.X / 4096f, v.Y / 4096f, v.Z / 4096f)
                    : new AnimBone(0, v.X / 360f, v.Y / 360f, v.Z / 360f);
            }
            anim.Frames.Add(new AnimFrame(Math.Max(1, k.Milliseconds), k.Step.X, k.Step.Y, k.Step.Z, slots));
        }
        return anim.Write();
    }

    public void FitBones(int bones)
    {
        foreach (var k in Keys)
        {
            while (k.Bones.Count < bones) k.Bones.Add((0, Vector3.Zero));
            if (k.Bones.Count > bones) k.Bones.RemoveRange(bones, k.Bones.Count - bones);
        }
    }

    public int TotalMilliseconds => Keys.Sum(k => Math.Max(1, k.Milliseconds));

    // The pose `ms` milliseconds in (playing from keyframe 0, looping back to LoopFrame after the last): each bone's turn between the keyframe
    // before and the one it is going to, as the game blends them -- for Lba1Pose (1024ths of a turn, LBA1's unit).
    public List<(int Type, double X, double Y, double Z)> PoseAt(double ms)
    {
        if (Keys.Count == 1) return Lba1Bones(Keys[0], Keys[0], 0);
        // (from keyframe 0 on, each keyframe reached in its own time; after the last, LoopFrame again, and on from there)
        var i = 0;
        var t = Math.Max(0, ms);
        for (var guard = 0; guard < 100000; guard++)
        {
            var next = i + 1 < Keys.Count ? i + 1 : LoopFrame;
            var span = Math.Max(1, Keys[next].Milliseconds);
            if (t < span) return Lba1Bones(Keys[i], Keys[next], t / span);
            t -= span;
            i = next;
        }
        return Lba1Bones(Keys[i], Keys[i], 0);
    }

    // The pose of one keyframe (Lba1Pose's).
    public List<(int Type, double X, double Y, double Z)> PoseOf(int key) => Lba1Bones(Keys[key], Keys[key], 0);

    private static List<(int Type, double X, double Y, double Z)> Lba1Bones(Key a, Key b, double f)
    {
        f = Math.Clamp(f, 0, 1);
        var result = new List<(int, double, double, double)>();
        for (var i = 0; i < a.Bones.Count; i++)
        {
            var (type, va) = a.Bones[i];
            var vb = i < b.Bones.Count ? b.Bones[i].Value : va;
            double L(float x, float y) { var d = y - x; if ((type & 1) == 0) { d = (float)((d + 540) % 360 - 180); } return x + d * f; }
            double x = L(va.X, vb.X), y = L(va.Y, vb.Y), z = L(va.Z, vb.Z);
            result.Add((type & 1) != 0 || i == 0 ? (type & 1, x, y, z) : (0, x * 1024 / 360, y * 1024 / 360, z * 1024 / 360));
        }
        return result;
    }
}
