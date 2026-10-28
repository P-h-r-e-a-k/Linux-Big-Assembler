using LBAAssembler.Lba1;

namespace ScriptRoundTrip;

// lba1sceneactors <LBA1 folder> <scene>: a scene's actors by name -- the entity's (FILE3D.HQD), the body's (BODY1.HQD), the animation's
// (ANIM1.HQD) or the sprite's -- and where each stands in cells; what a recreation has to make.
internal static class Lba1SceneActors
{
    public static int Run(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var scene = game.LoadScene(int.Parse(args[2]));
        foreach (var a in scene.Actors)
        {
            var what = a.IsSprite ? $"sprite {a.Sprite}" : a.HasEntity ? $"entity {a.Entity} \"{game.EntityName(a.Entity)}\" body {a.Body} anim {a.Anim}" : "none";
            Console.WriteLine($"actor {a.Index,2}: {what}; cell ({a.X / 512.0:0.0}, layer {a.Y / 256.0:0.0}, {a.Z / 512.0:0.0}) flags 0x{a.StaticFlags:X4}{(a.IsInvisible ? " invisible" : "")}");
        }
        return 0;
    }
}
