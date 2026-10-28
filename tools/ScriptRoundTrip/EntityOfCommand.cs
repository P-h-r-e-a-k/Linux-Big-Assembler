using LBAAssembler;

namespace ScriptRoundTrip;

// entityof <LBA2 folder> <body>...: the entities (RESS.HQR 44) using each BODY.HQR body, with the generic it has there and the entity's anims
internal static class EntityOfCommand
{
    public static int Run(string[] args)
    {
        var table = Lba2EntityTable.Load(args[1]) ?? throw new InvalidDataException("no entity table");
        foreach (var b in args.Skip(2).Select(int.Parse))
            foreach (var e in table.Entities.Where(e => e.Bodies.Any(x => x.Body == b)))
                Console.WriteLine($"body {b}: entity {e.Id} (generic {string.Join(",", e.Bodies.Where(x => x.Body == b).Select(x => x.Generic))}); its bodies {string.Join(" ", e.Bodies.Select(x => $"{x.Generic}={x.Body}"))}; anims {e.Anims.Count}");
        return 0;
    }
}
