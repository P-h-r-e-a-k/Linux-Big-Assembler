using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text.Json;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// A car for every other character of the game (2026-10-05, the user's: "Generate a vehicle for every unique character in LBA2"): the
// cast below -- one body for each character (a character's variants with something in its hand, or in another pose, are one), less
// Twinsen (his buggy) and the characters that have a car made by hand (All) -- each made by the same steps rather than by hand: a car of
// the style its kind drives (Sups roadsters, Francos jeeps, grobos trucks, quetches bubble cars, spheros ball cars, Wannies mine carts,
// Mosquibees gliders, glooms swamp boats, ...), in the colours its own body wears most, with the character sat in it as its body is --
// cut at its waist, made the size of the cockpit, its hands on the wheel where it has arms (CarDriver.FindArms), or sat in it whole where it
// hasn't (an animal, a creature). Where the steps sat one wrongly (seen on the sheet: tools/BodyPipeline castsheet), its Seat says how.
//
// They are bodies of entities of their own, copies of the racer's (157: its animations drive them), a hundred to an entity -- an entity's
// body numbers are one byte, and each car has its half-size copy beside it (RaceTrackSmallCars) -- and are numbered on from the cars made by
// hand (CastFirst on). RACECARS.JSON in the game folder lists every car by its number, with its BODY.HQR bodies (CatalogueFile): the car
// setup's "drive as" and the sheet go by it.
internal static partial class RaceTrackCharacterCars
{
    public enum Kind { Sup, Franco, Grobo, Rabbibunny, Quetch, Sphero, Wannie, Mosquibee, Gloom, Pighead, Dog, Robot, Monster, Creature, Ghost, Child }
    public enum Style { Roadster, Jeep, Truck, Kart, Bubble, Ball, Cart, Glider, Boat, Monster, Robot, Basket, Ghost }

    // A character of the cast: its body (BODY.HQR), the car's name ("<Short>'s <its style>"), who drives, its island and kind -- and where the
    // automatic seat went wrong, its own (Seat) or another style.
    public sealed record CastMember(int Character, string Short, string Driver, string Island, Kind Kind, Style? Style = null, CastSeat? Seat = null);
    // A driver's seat put right by hand: its waist (in its own body), its size in the car, its arms (none: NoArms), bones left out.
    public sealed record CastSeat(Vector3? Origin = null, float? Scale = null, bool NoArms = false, bool? Whole = null, int[]? Drop = null, (int, int[])? Right = null, (int, int[])? Left = null,
        float? Reach = null, float? GripHeight = null, float? GripX = null);

    public const int CastFirst = 64, CastPerEntity = 100;
    public const string CatalogueFile = "RACECARS.JSON";

    public static CastMember[] Cast => new CastMember[]
    {
        // Citadel Island
        new(34, "The snake", "a snake of Citadel Island", Citadel, Kind.Creature),
        new(35, "The skeleton", "the rabbibunny skeleton of the Tralu's cave", Citadel, Kind.Rabbibunny, Style.Ghost),
        new(36, "The flying rat", "the flying rat of the sewers", Citadel, Kind.Creature, Style.Glider),
        new(37, "The stranger quetch", "the quetch who isn't from Citadel Island", Citadel, Kind.Quetch),
        new(38, "The drunk rabbibunny", "the drunk rabbibunny in Luc's bar", Citadel, Kind.Rabbibunny),
        new(42, "The second sphero", "another sphero in Luc's bar", Citadel, Kind.Sphero),
        new(43, "The third sphero", "a third sphero in Luc's bar", Citadel, Kind.Sphero),
        new(45, "The quetch girl", "a quetch girl, Raph's friend", Citadel, Kind.Quetch),
        new(46, "The green Sup", "a Sup in light green", Citadel, Kind.Sup),
        new(49, "The yellow Franco", "a yellow Franco with a pistol", Citadel, Kind.Franco),
        new(51, "The grey dog", "a grey Esmer dog", Citadel, Kind.Dog),
        new(53, "The brown dog", "a brown Esmer dog", Citadel, Kind.Dog),
        new(54, "The baggage grobo", "the grobo in the baggage claim", Citadel, Kind.Grobo),
        new(56, "The baggage rabbibunny", "the rabbibunny in the baggage claim", Citadel, Kind.Rabbibunny),
        new(58, "The baggage quetch", "the quetch in the baggage claim", Citadel, Kind.Quetch),
        new(60, "The temple grobo", "a grobo in the Temple of Bu", Citadel, Kind.Grobo),
        new(61, "The ferry quetch", "the quetch on the Inter-Islands Ferry", Citadel, Kind.Quetch),
        new(63, "The neighbour", "Twinsen's neighbour", Citadel, Kind.Quetch),
        new(67, "The sewer painter", "the painter in the sewers", Citadel, Kind.Rabbibunny),
        new(68, "The blue-and-white rabbibunny", "a rabbibunny in blue and white", Citadel, Kind.Rabbibunny),
        new(84, "The dark green Sup", "a Sup in dark green", Citadel, Kind.Sup),
        new(85, "The red Sup", "a Sup in red", Citadel, Kind.Sup),
        new(100, "The museum clerk", "the clerk of Mrs. Bloop's museum", Citadel, Kind.Quetch),
        new(101, "The park guard", "a quetch guard of Temple Park", Citadel, Kind.Quetch, Style.Jeep),
        new(102, "The museum visitor", "a quetch visitor of the museum", Citadel, Kind.Quetch),
        new(108, "The bird", "a bird of Citadel Island", Citadel, Kind.Creature, Style.Glider),
        new(109, "The little Tralu", "the little Tralu", Citadel, Kind.Monster),
        new(112, "The stove creature", "the creature in the weather wizard's stove", Citadel, Kind.Creature),
        new(113, "The pharmacist", "the pharmacist", Citadel, Kind.Rabbibunny, Style.Bubble),
        new(114, "The pharmacy customer", "a sphero customer of the pharmacy", Citadel, Kind.Sphero),
        new(120, "The quetch", "a quetch of Citadel Island", Citadel, Kind.Quetch),
        new(134, "The rabbibunny child", "a rabbibunny child", Citadel, Kind.Child),
        new(135, "The teacher", "the kindergarten's teacher", Citadel, Kind.Quetch),
        new(136, "The grobo child", "a grobo child", Citadel, Kind.Child),
        new(137, "The first quetch child", "a quetch child", Citadel, Kind.Child),
        new(138, "The second quetch child", "the quetch child with the rope", Citadel, Kind.Child),
        new(159, "Citadel's robot", "the robot of Citadel Island", Citadel, Kind.Robot, Seat: new CastSeat(NoArms: true)),
        new(162, "The crab", "a crab", Citadel, Kind.Creature, Style.Boat),
        new(170, "The pharmacist's admirer", "the quetch in love with the pharmacist", Citadel, Kind.Quetch),
        new(172, "Raph's fiancee", "Raph's fiancee", Citadel, Kind.Quetch, Style.Roadster),
        new(178, "The third quetch child", "the quetch child on the pharmacy's roof", Citadel, Kind.Child),
        new(180, "The big brother", "the quetch in black, the first child's big brother", Citadel, Kind.Quetch),
        new(191, "The drunk Franco", "the Franco who is usually drunk", Citadel, Kind.Franco),
        new(211, "The giant spider", "the giant spider of the cliffs", Citadel, Kind.Creature, Style.Monster),
        new(25, "The nitro penguin", "a nitro meca-penguin", Citadel, Kind.Robot),
        // Desert Island
        new(119, "The nomad grobo", "a nomad grobo of the desert", Desert, Kind.Grobo),
        new(121, "The bather", "a rabbibunny in the Hacienda's bath", Desert, Kind.Rabbibunny, Style.Boat),
        new(122, "The grobo bather", "a grobo lady in the Hacienda's bath", Desert, Kind.Grobo, Style.Boat),
        new(128, "The grobo bather's husband", "a grobo in the Hacienda's bath", Desert, Kind.Grobo, Style.Boat),
        new(124, "The magic student", "a rabbibunny student of the School of Magic", Desert, Kind.Rabbibunny),
        new(125, "The ghost", "the ghost of the School of Magic", Desert, Kind.Ghost),
        new(126, "The sad student", "the sad grobo student", Desert, Kind.Grobo),
        new(127, "The black sphero", "a black nomad sphero", Desert, Kind.Sphero),
        new(133, "The desert shopkeeper", "the rabbibunny of the Desert Island shop", Desert, Kind.Rabbibunny),
        new(142, "The hotel grobo", "the grobo of the Desert Island hotel", Desert, Kind.Grobo),
        new(148, "The biker", "Rabbibunny, the biker", Desert, Kind.Rabbibunny, Style.Monster),
        new(158, "The desert robot", "the robot of Desert Island", Desert, Kind.Robot, Seat: new CastSeat(NoArms: true)),
        new(190, "The grobo wizard", "a grobo wizard", Desert, Kind.Grobo),
        new(222, "The desert sphero", "a sphero of the desert", Desert, Kind.Sphero),
        new(224, "The second desert sphero", "another sphero of the desert", Desert, Kind.Sphero),
        new(225, "The camel", "a camel", Desert, Kind.Creature, Style.Monster),
        new(228, "The sphero bowler", "a sphero who plays bowls", Desert, Kind.Sphero),
        new(229, "The quetch bowler", "a quetch who plays bowls", Desert, Kind.Quetch),
        new(233, "The bearded bowler", "a bearded Franco who plays bowls", Desert, Kind.Franco),
        new(232, "The reader", "a quetch girl with a book on the beach", Desert, Kind.Quetch, Style.Boat),
        new(236, "The swimmer", "a Sup in his swimming costume", Otringal, Kind.Sup, Style.Boat),
        new(238, "The Eye", "the Eye of the School of Magic", Desert, Kind.Creature, Style.Ghost),
        new(243, "The Port-Ludo sphero", "the sphero near Port-Ludo", Desert, Kind.Sphero),
        new(244, "The grobo policeman", "a grobo policeman of Temple Park", Desert, Kind.Grobo, Style.Jeep),
        new(245, "The wandering rabbibunny", "a rabbibunny of Desert Island", Desert, Kind.Rabbibunny),
        new(246, "The duck", "a small yellow duck", Desert, Kind.Creature, Style.Boat),
        new(250, "The desert grobo", "a grobo of the desert", Desert, Kind.Grobo),
        new(251, "The old musician", "the old rabbibunny with the guitar and the flute", Desert, Kind.Rabbibunny),
        // the Emerald Moon and the Esmers' saucer
        new(143, "The saucer's Sup", "a Sup in blue aboard the saucer", Moon, Kind.Sup, Style.Glider),
        new(144, "The saucer's stewardess", "a Sup lady aboard the saucer", Moon, Kind.Sup, Style.Glider),
        new(147, "The translator", "the translator", Moon, Kind.Robot),
        new(181, "The hidden Esmer", "an Esmer hiding in a trash can", Moon, Kind.Creature, Style.Robot),
        new(379, "The orange Sup", "a Sup in orange", Moon, Kind.Sup),
        new(91, "The base guard", "a guard of the Esmers' secret base", Moon, Kind.Franco),
        new(96, "The Franco dissident", "a Franco dissident", Moon, Kind.Franco),
        // Otringal and Zeelich
        new(87, "The black-hat policeman", "a Franco policeman in a black hat", Otringal, Kind.Franco),
        new(157, "The prison robot", "the robot guarding Otringal's prison", Otringal, Kind.Robot, Seat: new CastSeat(NoArms: true)),
        new(200, "The Sup sailor", "a Sup sailor with a glass", Otringal, Kind.Sup, Style.Boat),
        new(256, "The giraffe", "the giraffe with the camera, from Zeelich", Otringal, Kind.Creature, Style.Monster),
        new(257, "The ant", "the ant with the backpack, from Zeelich", Otringal, Kind.Creature),
        new(259, "The two-headed monster", "the two-headed monster of the palace", Otringal, Kind.Monster),
        new(262, "The bathing Sup", "a Sup in the water", Otringal, Kind.Sup, Style.Boat),
        new(263, "The old Sup lady", "the old Sup lady of the Imperial Hotel", Otringal, Kind.Sup, Style.Bubble),
        new(264, "The hotel guard", "a Franco guard of the Imperial Hotel", Otringal, Kind.Franco),
        new(265, "The stick policeman", "a Franco policeman with a stick", Otringal, Kind.Franco),
        new(267, "The Franco lady", "a Franco lady", Otringal, Kind.Franco, Style.Roadster),
        new(271, "The palace guard", "a Sup guard of the Emperor's palace", Otringal, Kind.Sup, Style.Jeep),
        new(277, "The first flyer pilot", "the first driver of the motor-flyer", Otringal, Kind.Franco, Style.Glider),
        new(278, "The second flyer pilot", "the second driver of the motor-flyer", Otringal, Kind.Franco, Style.Glider),
        new(293, "The gas station Franco", "the Franco at Otringal's gas station", Otringal, Kind.Franco, Style.Truck),
        new(295, "The green gloom", "a green gloom", Otringal, Kind.Gloom),
        new(297, "The grey gloom", "a grey gloom", Otringal, Kind.Gloom),
        new(298, "The little gloom", "the little gloom at the casino's door", Otringal, Kind.Gloom),
        new(300, "The big gloom", "the big grey gloom", Otringal, Kind.Gloom, Style.Monster),
        new(301, "The drummer", "a Franco with a drum", Otringal, Kind.Franco),
        new(402, "The dancer", "a Franco dancer", Otringal, Kind.Franco, Style.Roadster),
        new(403, "The fireman", "a Franco fireman", Otringal, Kind.Franco, Style.Truck),
        new(406, "The casino reptile", "the reptile of the casino", Otringal, Kind.Monster, Style.Boat),
        new(407, "The brown racing dog", "the casino's brown racing dog", Otringal, Kind.Dog),
        new(408, "The orange racing dog", "the casino's orange racing dog", Otringal, Kind.Dog),
        new(409, "The cyan racing dog", "the casino's cyan racing dog", Otringal, Kind.Dog),
        new(410, "The yellow racing dog", "the casino's yellow racing dog", Otringal, Kind.Dog),
        new(411, "The casino Sup", "a Sup in the casino", Otringal, Kind.Sup),
        new(412, "The second casino Sup", "another Sup in the casino", Otringal, Kind.Sup),
        new(413, "The casino gloom", "a green gloom in the casino", Otringal, Kind.Gloom),
        new(415, "The pink sailor", "a pink sailor", Otringal, Kind.Wannie, Style.Boat),
        new(420, "The brown sailor", "a brown sailor", Otringal, Kind.Wannie, Style.Boat),
        new(424, "The Franco in the grey hat", "a Franco in a grey hat with a glass", Otringal, Kind.Franco),
        new(431, "The other shop Sup", "the shop's Sup who hates Twinsun", Otringal, Kind.Sup),
        new(436, "The pighead with the hammer", "the pighead in red with the hammer", Otringal, Kind.Pighead),
        new(437, "The pighead gardener", "the pighead in brown with the garden tool", Otringal, Kind.Pighead),
        new(440, "The shy Sup girl", "the shy Sup girl of the Imperial Hotel", Otringal, Kind.Sup, Style.Bubble),
        new(441, "The music lover", "a grey Franco guard with his tape player", Otringal, Kind.Franco),
        new(393, "The agency Sup", "the Sup with the cigar in the undergas agency", Otringal, Kind.Sup),
        new(394, "The agency clerk", "the Sup with the papers in the undergas agency", Otringal, Kind.Sup),
        new(428, "The second Franco lady", "another Franco lady", Otringal, Kind.Franco, Style.Roadster),
        // Celebration Island and under it
        new(309, "The second kangaroo", "another kangaroo", Celebration, Kind.Creature, Style.Jeep),
        new(310, "The Franco priest", "a Franco priest", Celebration, Kind.Franco, Style.Roadster),
        new(456, "The guardian monster", "the monster guarding the protection spell", Celebration, Kind.Monster),
        new(320, "The gas monster", "a gas monster", Wannies, Kind.Monster, Style.Ghost),
        new(340, "The little gas monster", "a little gas monster", Wannies, Kind.Monster, Style.Ghost),
        // the Island of the Wannies
        new(195, "The second Wannie", "another Wannie", Wannies, Kind.Wannie),
        new(319, "The Wannie priest", "the Wannie priest", Wannies, Kind.Wannie),
        new(327, "The Wannie child", "a Wannie child", Wannies, Kind.Child),
        new(329, "The Wannie grandma", "the Wannie grandma", Wannies, Kind.Wannie),
        new(378, "The one-gloved Wannie", "the Wannie with one glove", Wannies, Kind.Wannie),
        new(382, "The digging Wannie", "a Wannie digging", Wannies, Kind.Wannie),
        new(422, "The young Wannie", "a young Wannie with a stone", Wannies, Kind.Wannie),
        new(332, "The firefly", "a firefly", Wannies, Kind.Creature, Style.Glider),
        new(333, "The frog", "a frog", Wannies, Kind.Creature, Style.Boat),
        new(346, "The yellow pig", "a little yellow pig", Wannies, Kind.Creature),
        // the Island of the Mosquibees
        new(202, "The Mosquibee carrier", "a Mosquibee with a jar", Mosquibees, Kind.Mosquibee),
        new(318, "The bearded Mosquibee", "a Mosquibee with a beard", Mosquibees, Kind.Mosquibee),
        new(359, "The green Mosquibee", "a green Mosquibee", Mosquibees, Kind.Mosquibee),
        new(396, "The fat Mosquibee", "the fat yellow-and-black Mosquibee", Mosquibees, Kind.Mosquibee),
        // the Island of the Francos
        new(186, "The waiter", "a Franco with plates", Francos, Kind.Franco),
        new(206, "The Franco child", "a Franco child", Francos, Kind.Child),
        new(448, "The rapper", "a Franco child in a red rapper's hat", Francos, Kind.Child),
        new(371, "The Franco dog", "a dog of the Island of the Francos", Francos, Kind.Dog),
        new(372, "The Franco worker", "a Franco worker", Francos, Kind.Franco, Style.Truck),
        new(373, "The treasure hunter", "a Franco with a metal detector", Francos, Kind.Franco),
        new(374, "The red-hat worker", "a Franco worker in a red hat", Francos, Kind.Franco, Style.Truck),
        new(375, "The mechanic owl", "the mechanic owl", Francos, Kind.Creature, Style.Glider),
        new(443, "The shop lady", "the Franco lady of the shop", Francos, Kind.Franco, Style.Bubble),
        new(445, "The window cleaner", "a Franco cleaning the glass", Francos, Kind.Franco),
        // Zeelich's end
        new(435, "The small spider", "a small spider", Otringal, Kind.Creature, Style.Monster),
        new(459, "The grey mechanic", "the grey mechanic grobo", Otringal, Kind.Grobo, Style.Robot),
        new(460, "The green mechanic", "the green mechanic grobo with the missiles", Otringal, Kind.Grobo, Style.Robot),
        new(468, "The mechanic rabbibunny", "the mechanic rabbibunny", Otringal, Kind.Rabbibunny, Style.Robot),
    };

    public static Style StyleOf(CastMember c) => c.Style ?? c.Kind switch
    {
        Kind.Sup => Style.Roadster, Kind.Franco => Style.Jeep, Kind.Grobo => Style.Truck, Kind.Rabbibunny => Style.Kart, Kind.Quetch => Style.Bubble,
        Kind.Sphero => Style.Ball, Kind.Wannie => Style.Cart, Kind.Mosquibee => Style.Glider, Kind.Gloom => Style.Boat, Kind.Pighead => Style.Monster,
        Kind.Dog => Style.Basket, Kind.Robot => Style.Robot, Kind.Monster => Style.Monster, Kind.Ghost => Style.Ghost, Kind.Child => Style.Kart,
        _ => Style.Basket,
    };

    private static string Noun(Style s) => s switch
    {
        Style.Roadster => "roadster", Style.Jeep => "jeep", Style.Truck => "truck", Style.Kart => "kart", Style.Bubble => "bubble car", Style.Ball => "ball car",
        Style.Cart => "mine cart", Style.Glider => "glider", Style.Boat => "swamp boat", Style.Monster => "monster truck", Style.Robot => "robot car",
        Style.Basket => "basket car", _ => "ghost car",
    };

    public static string CastName(CastMember c) => $"{c.Short}'s {Noun(StyleOf(c))}";

    // The cast's cars, numbered on from the cars made by hand.
    public static Car[] CastCars => Cast.Select((c, i) => new Car(CastName(c), c.Driver, c.Character, CastFirst + i, (ch, racer) => BuildCast(c, i, ch, racer), c.Island)).ToArray();

    // ---------------------------------------------------------------------------------------------------------------- the colours

    // A car's colours from its driver's body: the palette ramps its polygons cover most (by area: the clothes, the skin, the fur), each as
    // a start the light keeps inside its ramp (CarMesh.Light 1: up to 9.4 steps on, so no more than the ramp's 6th), and as a colour drawn
    // as it is (spheres, lines). Not the purple placeholders (0-15) nor the colours drawn unlit (240 on).
    private sealed record Paints(int Main, int Second, int Trim, int MainFlat, int SecondFlat, int TrimFlat);

    private static Paints PaintsOf(Body character)
    {
        var world = character.World();
        var ramps = new Dictionary<int, (double Area, double Shade)>();
        foreach (var f in character.Faces)
        {
            if (f.Colour < 16 || f.Colour >= 240) continue;
            var p = f.Points.Select(i => world[i]).ToArray();
            var area = 0.0;
            for (var k = 1; k + 1 < p.Length; k++) area += Vector3.Cross(p[k] - p[0], p[k + 1] - p[0]).Length() / 2;
            var r = f.Colour >> 4;
            ramps.TryGetValue(r, out var was);
            ramps[r] = (was.Area + area, was.Shade + area * (f.Colour & 15));
        }
        var order = ramps.Where(r => r.Value.Area > 0).OrderByDescending(r => r.Value.Area).Select(r => (Ramp: r.Key, Shade: r.Value.Shade / r.Value.Area)).ToList();
        while (order.Count < 3) order.Add(order.Count switch { 0 => (Ramp: Steel >> 4, Shade: 4.0), 1 => (Ramp: Gold >> 4, Shade: 4.0), _ => (Ramp: Grey >> 4, Shade: 3.0) });
        int Lit((int Ramp, double Shade) r) => r.Ramp * 16 + Math.Clamp((int)Math.Round(r.Shade), 1, 5);
        int Flat((int Ramp, double Shade) r) => r.Ramp * 16 + Math.Clamp((int)Math.Round(r.Shade) + 4, 4, 11);
        return new Paints(Lit(order[0]), Lit(order[1]), Lit(order[2]), Flat(order[0]), Flat(order[1]), Flat(order[2]));
    }

    // ---------------------------------------------------------------------------------------------------------------- the seat

    // How the driver sits: its waist in its own body (Origin), its size in the car, its arms (none: it is sat in whole, keeping its hands to
    // itself), the bones left out, the grip; and how wide and long the cockpit is to take it.
    private sealed record CastFit(Driver Driver, float Width, bool Whole);

    private static CastFit FitOf(Body character, CastMember c)
    {
        var world = character.World();
        var driver = new CarDriver(character);
        float minY = world.Min(v => v.Y), maxY = world.Max(v => v.Y), height = Math.Max(1, maxY - minY);
        var seat = c.Seat;
        // (both arms or none: an animal, a creature, a Mosquibee and a gas cloud keep their hands -- or paws -- to themselves)
        (int, int[])? right = null, left = null;
        if (seat?.NoArms == true) { }
        else if (seat?.Right is not null || seat?.Left is not null) { right = seat.Right; left = seat.Left; }
        else if (c.Kind is not (Kind.Creature or Kind.Dog or Kind.Mosquibee) && !(StyleOf(c) == Style.Ghost && c.Kind is Kind.Monster))
        {
            var (fr, fl) = driver.FindArms();
            if (fr is { } r && fl is { } l) { right = (r.Upper, r.Fore); left = (l.Upper, l.Fore); }
        }
        var arms = (Right: right, Left: left);
        // (sat in whole -- on the seat rather than in it -- an animal, a creature, a robot on legs, a cloud: anything without arms but the
        // Mosquibees, the Francos and the rest, who sit in to the waist)
        var whole = arms.Right is null && arms.Left is null && (seat?.Whole ?? c.Kind is Kind.Creature or Kind.Dog or Kind.Robot or Kind.Monster or Kind.Ghost);
        // the grobos: the arms the hand-made grobo cars use (9 and 11, hanging from the chest, 3 -- FindArms takes their ears), their waist and
        // size as those cars' (RaceTrackCharacterCars.Named: Grobo()), for a grobo of its height
        if (seat is null && c.Kind is Kind.Grobo or Kind.Child && character.Bones.Count > 12 && character.Bones[9].Parent == 3 && character.Bones[11].Parent == 3
            && world[character.Bones[9].Pivot].X * world[character.Bones[11].Pivot].X < 0)
        {
            var k = height / 1132f;
            return new CastFit(new Driver(new Vector3(0, 470 * k, 60 * k), 0.62f / k, (9, Array.Empty<int>()), (11, Array.Empty<int>()), Reach: 180, GripHeight: 30, GripX: 150),
                world.Where(v => v.Y >= 470 * k).Select(v => MathF.Abs(v.X)).DefaultIfEmpty(200).Max() * 0.62f / k * 0.8f, false);
        }
        var armBones = new HashSet<int>();
        foreach (var arm in new[] { arms.Right, arms.Left })
            if (arm is { } a) { armBones.Add(a.Item1); foreach (var b in driver.Below(a.Item1)) armBones.Add(b); }
        // the waist: the hips the legs hang from (a bone outside the arms whose own points reach the floor, its pivot low down)
        Vector3 origin;
        if (seat?.Origin is { } given) origin = given;
        else if (whole) origin = new Vector3(0, minY, (world.Min(v => v.Z) + world.Max(v => v.Z)) / 2);
        else
        {
            var hips = new List<Vector3>();
            for (var b = 1; b < character.Bones.Count; b++)
            {
                if (armBones.Contains(b)) continue;
                var bone = character.Bones[b];
                var pivot = world[bone.Pivot];
                if (pivot.Y > minY + height * 0.62f || pivot.Y < minY + height * 0.18f) continue;
                var low = driver.Below(b).Append(b).SelectMany(x => Enumerable.Range(character.Bones[x].Start, character.Bones[x].Count)).Select(i => world[i].Y).DefaultIfEmpty(maxY).Min();
                if (low > minY + height * 0.1f) continue;
                // (a leg's top: its parent is not itself a leg)
                var parent = bone.Parent;
                var parentPivot = parent >= 0 ? world[character.Bones[parent].Pivot] : Vector3.Zero;
                if (parent > 0 && parentPivot.Y < pivot.Y - 1) continue;
                hips.Add(pivot);
            }
            origin = hips.Count > 0 ? new Vector3(0, hips.Max(h => h.Y) + height * 0.02f, hips.Average(h => h.Z)) : new Vector3(0, minY + height * 0.42f, 0);
        }
        // the size: what shows above the waist about as tall as a Sup's in the hand-made cars, no wider than the cockpit takes
        // (the arms not counted: they go to the wheel)
        var armPoints = new HashSet<int>(armBones.SelectMany(b => Enumerable.Range(character.Bones[b].Start, character.Bones[b].Count)));
        var above = world.Where((v, i) => v.Y >= origin.Y && !armPoints.Contains(i)).ToList();
        float upper = Math.Max(1, maxY - origin.Y), width = above.Count > 0 ? above.Max(v => MathF.Abs(v.X)) : 200;
        var scale = seat?.Scale ?? (whole
            ? Math.Clamp(MathF.Min(MathF.Min(560 / height, 480 / Math.Max(1, world.Max(v => MathF.Abs(v.X)) * 2)), 820 / Math.Max(1, world.Max(v => v.Z) - world.Min(v => v.Z))), 0.18f, 1.8f)
            : Math.Clamp(MathF.Min(600 / upper, 235 / Math.Max(1, width)), 0.3f, 1.2f));
        // the grip: as far out as the arm reaches, at the height of its elbow
        float reach = 215, gripHeight = 95, gripX = 64;
        if (arms.Right is { } ra)
        {
            var shoulder = driver.PivotOf(ra.Item1);
            var fore = ra.Item2.Length > 0 ? ra.Item2 : driver.Below(ra.Item1);
            var hand = driver.Far(fore.Length > 0 ? fore : new[] { ra.Item1 }, shoulder);
            var length = Vector3.Distance(shoulder, hand) * scale;
            reach = Math.Clamp(length * 0.8f, 120, 240);
            gripHeight = Math.Clamp((shoulder.Y - origin.Y) * scale * 0.45f, 20, 120);
            gripX = Math.Clamp(MathF.Abs(shoulder.X) * scale * 0.7f, 50, 150);
        }
        return new CastFit(new Driver(origin, scale, arms.Right, arms.Left, seat?.Drop, seat?.Reach ?? reach, seat?.GripHeight ?? gripHeight, seat?.GripX ?? gripX),
            width * scale, whole);
    }

    // ---------------------------------------------------------------------------------------------------------------- the cars

    // One of the cast's cars. If the car and its driver come to more than the engine takes (550 points, 550 polygons, lines and spheres),
    // a leaner one (fewer sides, plainer wheels, no trimmings), and at the last the driver's smallest polygons left out until it fits.
    public static Body BuildCast(CastMember c, int index, Body character, Body racer)
    {
        Exception? last = null;
        for (var lean = 0; lean <= 4; lean++)
        {
            try { return BuildCastAt(c, index, character, racer, lean); }
            catch (InvalidDataException e) { last = e; }
        }
        throw new InvalidDataException($"{CastName(c)}: no car small enough for the engine ({last?.Message})");
    }

    private static Body BuildCastAt(CastMember c, int index, Body character, Body racer, int lean)
    {
        var m = new CarMesh(CastName(c));
        Roots(m);
        var paint = PaintsOf(character);
        var fit = FitOf(character, c);
        var style = StyleOf(c);
        var variant = index % 3;
        var sides = lean >= 1 ? 8 : 12;
        var parts = StyleBody(m, style, paint, variant, sides, lean, c, fit);
        // (at the last, the driver's smallest polygons left out: those under a share of the body's area)
        CarDriver.Painter? thin = null;
        if (lean >= 3)
        {
            var world = character.World();
            float Area(Face f) { var p = f.Points.Select(i => world[i]).ToArray(); var a = 0f; for (var k = 1; k + 1 < p.Length; k++) a += Vector3.Cross(p[k] - p[0], p[k + 1] - p[0]).Length() / 2; return a; }
            var areas = character.Faces.Select(Area).OrderBy(a => a).ToList();
            var cut = areas[Math.Min(areas.Count - 1, areas.Count * (lean == 3 ? 35 : 60) / 100)];
            thin = (f, _) => Area(f) < cut ? null : (f.Colour, CarDriver.Own, f.Material == 0 ? 0 : -1);
        }
        return FinishCast(m, parts, character, racer, fit, thin, lean);
    }

    // What a style makes of the car before its driver: the hull, the cockpit, the wheels.
    private sealed record Parts(Hull Hull, Cabin Cabin, WheelLook Look, Axle Front, Axle Rear);

    private static Parts StyleBody(CarMesh m, Style style, Paints p, int variant, int sides, int lean, CastMember c, CastFit fit)
    {
        var trim = lean < 2;
        var rx = Math.Clamp(fit.Width + 60, 190, 265);
        WheelLook Look(int strut, int tyre, int rim, int cap) => new(strut, 22, tyre, 1, rim, 1, cap, lean >= 1 ? 5 : 6, Lean: lean >= 1);
        switch (style)
        {
            case Style.Roadster:
            {
                // long and low: a long bonnet, the cockpit far back, fins on the tail
                var hull = new Hull(m, 285, sides, 3, 0.5f, (650, 140, 66, 82), (570, 225, 104, 112), (300, 262, 126, 128), (0, 270, 140, 130), (-300, 272, 150, 130),
                    (-560, 245, 128, 120), (-650, 175, 88, 95));
                hull.Skin(m, (band, k) => k == 0 || (sides == 12 && k == 11) ? (variant == 1 ? p.Second : p.Main) : band == 2 && variant == 2 ? p.Trim : k >= sides / 2 - 1 && k <= sides / 2 ? p.Second : p.Main);
                hull.Nose(m, 668, p.Second);
                hull.Tail(m, -664, p.Main);
                if (trim)
                {
                    foreach (var side in Sides)
                    {
                        m.Sphere(m.P(2, new(side * 165, hull.Cy + 38, 612)), 44, Lamp);
                        m.Sphere(m.P(2, new(side * 180, hull.Cy + 30, -646)), 28, RedFlat);
                        m.Plate(2, p.Second, hull.OnTop(side * 205, -380, -4), hull.OnTop(side * 205, -600, -4), new Vector3(side * 210, hull.Cy + 300, -640));
                    }
                    for (var i = 0; i + 1 < hull.Rings.Count; i++) m.Line(hull.Rings[i][0], hull.Rings[i + 1][0], p.TrimFlat);
                }
                return new Parts(hull, new Cabin(-150, rx, 190, Rim: p.Second, Screen: 190, ScreenHalf: rx - 15, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Second, p.TrimFlat), new Axle(new Vector3(245, 262, 420), new Vector3(440, 160, 420), 160, 110), new Axle(new Vector3(255, 268, -420), new Vector3(470, 178, -420), 178, 130));
            }
            case Style.Jeep:
            {
                // square-shouldered and high, a roll bar over the seats, a spare wheel on the back; police and guards a light on the bar
                var hull = new Hull(m, 320, sides, 6, 0.5f, (600, 245, 120, 120), (470, 265, 160, 130), (0, 272, 175, 135), (-420, 272, 175, 135), (-560, 255, 150, 125));
                hull.Skin(m, (band, k) => k >= sides / 2 - 1 && k <= sides / 2 ? p.Second : variant == 2 && band == 1 ? p.Trim : p.Main);
                hull.Nose(m, 610, p.Second);
                hull.Tail(m, -572, p.Main);
                if (trim)
                {
                    var bar = -260f;
                    foreach (var side in Sides) m.Strut(2, hull.OnTop(side * 230, bar, -6), new Vector3(side * 210, hull.Top(0, bar) + 330, bar), 26, Steel);
                    m.Strut(2, new Vector3(-210, hull.Top(0, bar) + 330, bar), new Vector3(210, hull.Top(0, bar) + 330, bar), 26, Steel);
                    if (c.Driver.Contains("police") || c.Driver.Contains("guard"))
                    {
                        m.Ball(2, new Vector3(-80, hull.Top(0, bar) + 360, bar), 34, RedFlat + 2);
                        m.Ball(2, new Vector3(80, hull.Top(0, bar) + 360, bar), 34, 199);
                    }
                    var back = new Vector3(0, hull.Cy + 40, -600);
                    var tyre = m.Loop(2, back, new Vector3(130, 0, 0), new Vector3(0, 130, 0), 8);
                    var hub = m.P(2, back + new Vector3(0, 0, -30));
                    for (var k = 0; k < 8; k++) m.Out(new[] { tyre[k], tyre[(k + 1) % 8], hub }, Dark, back + new Vector3(0, 0, 40));
                    foreach (var side in Sides) m.Sphere(m.P(2, new(side * 170, hull.Cy + 70, 612)), 42, Lamp);
                    m.Line(m.P(2, new(-200, hull.Cy - 40, 640)), m.P(2, new(200, hull.Cy - 40, 640)), Steel + 6);
                }
                return new Parts(hull, new Cabin(-60, rx, 200, Rim: Dark, Screen: 180, ScreenHalf: rx - 10, Rail: Steel + 6),
                    Look(Steel, Dark, p.Second, p.TrimFlat), new Axle(new Vector3(250, 280, 400), new Vector3(450, 185, 400), 185, 130), new Axle(new Vector3(250, 280, -400), new Vector3(450, 185, -400), 185, 130));
            }
            case Style.Truck:
            {
                // a cab at the front, the driver in it, a load bed behind with crates
                var hull = new Hull(m, 320, sides, 6, 0.5f, (640, 240, 150, 120), (560, 262, 200, 130), (200, 270, 210, 135), (100, 270, 120, 135), (-560, 270, 110, 135), (-650, 255, 100, 125));
                hull.Skin(m, (band, k) => band <= 1 ? (k >= sides / 2 - 1 && k <= sides / 2 ? p.Second : p.Main) : variant == 1 ? p.Trim : p.Second);
                hull.Nose(m, 652, p.Main);
                hull.Tail(m, -662, p.Second);
                if (trim)
                {
                    m.Box(2, new Vector3(-110, hull.Top(0, -300) + 90, -300), new Vector3(170, 170, 170), Wood, Wood + 2);
                    m.Box(2, new Vector3(120, hull.Top(0, -420) + 70, -430), new Vector3(140, 140, 140), Wood + 1, Wood + 3);
                    foreach (var side in Sides) m.Sphere(m.P(2, new(side * 180, hull.Cy + 90, 650)), 46, Lamp);
                    m.Rod(2, hull.OnTop(200, 420, 0), hull.OnTop(200, 420, 260), Steel + 6);
                    m.Sphere(m.P(2, hull.OnTop(200, 420, 290)), 36, GreySoft);
                }
                return new Parts(hull, new Cabin(330, rx, 175, Rim: p.Second, Screen: 500, ScreenHalf: rx - 10, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Main, p.TrimFlat), new Axle(new Vector3(255, 280, 430), new Vector3(455, 190, 430), 190, 130), new Axle(new Vector3(255, 280, -430), new Vector3(470, 205, -430), 205, 150));
            }
            case Style.Kart:
            {
                // low and small, the driver sitting up out of it, big wheels behind, two exhausts
                var hull = new Hull(m, 245, sides, 2.5f, 0.5f, (560, 110, 60, 70), (430, 170, 80, 85), (100, 205, 92, 95), (-250, 215, 100, 100), (-470, 190, 90, 95), (-540, 130, 70, 80));
                hull.Skin(m, (band, k) => variant == 0 && band % 2 == 1 ? p.Second : k == 0 ? p.Trim : p.Main);
                hull.Nose(m, 590, p.Second);
                hull.Tail(m, -560, p.Main);
                if (trim)
                {
                    foreach (var side in Sides)
                    {
                        m.Strut(2, new Vector3(side * 80, hull.Cy + 40, -520), new Vector3(side * 95, hull.Cy + 140, -640), 30, Steel);
                        m.Sphere(m.P(2, new(side * 95, hull.Cy + 150, -650)), 24, Dark);
                    }
                    m.Plate(2, p.Second, new Vector3(-230, hull.Cy + 30, 600), new Vector3(230, hull.Cy + 30, 600), new Vector3(230, hull.Cy + 10, 520), new Vector3(-230, hull.Cy + 10, 520));
                }
                return new Parts(hull, new Cabin(-100, Math.Min(rx, 220), 170, Up: 18, Rim: p.Second, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Trim, p.TrimFlat), new Axle(new Vector3(180, 230, 380), new Vector3(360, 135, 380), 135, 100), new Axle(new Vector3(190, 240, -360), new Vector3(405, 185, -360), 185, 150));
            }
            case Style.Bubble:
            {
                // an egg on small wheels, a big see-through screen round the front
                var hull = Lens(m, 320, sides, 560, 210, 140, new[] { 540f, 470, 330, 120, -120, -330, -470, -540 }, 2.2f, 0.5f);
                hull.Skin(m, (band, k) => k >= sides / 2 - 1 && k <= sides / 2 ? p.Second : variant == 1 && band is 1 or 5 ? p.Trim : p.Main);
                hull.Nose(m, 562, p.Main);
                hull.Tail(m, -562, p.Main);
                if (trim)
                {
                    foreach (var side in Sides) m.Sphere(m.P(2, new(side * 150, hull.Cy + 60, 520)), 48, Lamp);
                    m.Sphere(m.P(2, hull.OnTop(0, -400, 12)), 46, p.TrimFlat);
                }
                return new Parts(hull, new Cabin(-60, rx, 220, Rim: p.Second, Screen: 220, ScreenHalf: rx, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Second, p.TrimFlat), new Axle(new Vector3(230, 250, 330), new Vector3(400, 135, 330), 135, 95), new Axle(new Vector3(230, 250, -330), new Vector3(400, 135, -330), 135, 95));
            }
            case Style.Ball:
            {
                // a ball on four wheels, spotted, the driver looking out of its top
                var hull = Lens(m, 330, sides, 430, 230, 200, new[] { 410f, 330, 190, 0, -190, -330, -410 }, 2, 0.5f);
                hull.Skin(m, (band, k) => (band + k) % 2 == 0 && variant != 1 ? p.Second : p.Main);
                hull.Nose(m, 432, p.Main);
                hull.Tail(m, -432, p.Main);
                if (trim)
                    foreach (var (x, z) in new[] { (230f, 200f), (-230f, 200f), (250f, -150f), (-250f, -150f) })
                        m.Sphere(m.P(2, new(x * 1.18f, hull.Cy + 40, z)), 46, p.TrimFlat);
                return new Parts(hull, new Cabin(0, Math.Min(rx + 20, 250), 210, Up: 50, Rim: p.Trim, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Trim, p.TrimFlat), new Axle(new Vector3(260, 300, 300), new Vector3(450, 160, 300), 160, 110), new Axle(new Vector3(260, 300, -300), new Vector3(450, 160, -300), 160, 110));
            }
            case Style.Cart:
            {
                // a mine cart: a square tub on big wheels, rivets round its rim, a lamp on a pole
                var hull = new Hull(m, 330, sides, 8, 0.5f, (520, 230, 170, 100), (430, 260, 200, 120), (-430, 260, 200, 120), (-520, 230, 170, 100));
                hull.Skin(m, (band, k) => band == 1 ? (k >= sides / 2 - 1 && k <= sides / 2 ? Dark : variant == 2 ? p.Second : p.Main) : p.Second);
                hull.Nose(m, 525, p.Second);
                hull.Tail(m, -525, p.Second);
                if (trim)
                {
                    for (var i = 0; i < 6; i++) foreach (var side in Sides) m.Sphere(m.P(2, new(side * 265, hull.Cy + 150, 400 - i * 160)), 18, GoldFlat);
                    m.Rod(2, hull.OnTop(-180, -380, 0), hull.OnTop(-180, -380, 380), Steel + 6);
                    m.Sphere(m.P(2, hull.OnTop(-180, -380, 400)), 44, Lamp);
                }
                return new Parts(hull, new Cabin(40, rx, 210, Up: 20, Rim: Dark, Rail: p.TrimFlat),
                    Look(Steel, Dark, Steel, GoldFlat), new Axle(new Vector3(250, 290, 350), new Vector3(440, 200, 350), 200, 120), new Axle(new Vector3(250, 290, -350), new Vector3(440, 200, -350), 200, 120));
            }
            case Style.Glider:
            {
                // a slim body with see-through wings out either side and a tail fin
                var hull = Lens(m, 300, sides, 600, 160, 120, new[] { 590f, 500, 330, 100, -150, -380, -520, -590 }, 2.2f, 0.5f);
                hull.Skin(m, (band, k) => k >= sides / 2 - 1 && k <= sides / 2 ? p.Second : variant == 0 && band == 3 ? p.Trim : p.Main);
                hull.Nose(m, 612, p.Second);
                hull.Tail(m, -600, p.Main);
                foreach (var side in Sides)
                {
                    var a = m.P(2, new(side * 210, hull.Cy + 40, 150)); var b = m.P(2, new(side * 820, hull.Cy + 170, -180)); var d = m.P(2, new(side * 780, hull.Cy + 160, -360)); var e = m.P(2, new(side * 210, hull.Cy + 40, -320));
                    m.Both(new[] { a, b, d, e }, Wing, CarMesh.SeeThrough);
                    if (trim) { m.Line(a, b, p.TrimFlat); m.Line(b, d, p.TrimFlat); }
                }
                if (trim) m.Plate(2, p.Second, hull.OnTop(0, -380, -4), hull.OnTop(0, -580, -4), new Vector3(0, hull.Cy + 330, -620));
                return new Parts(hull, new Cabin(60, Math.Min(rx, 220), 190, Rim: p.Second, Screen: 300, ScreenHalf: Math.Min(rx, 220) - 10, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Second, p.TrimFlat), new Axle(new Vector3(200, 240, 380), new Vector3(380, 140, 380), 140, 95), new Axle(new Vector3(200, 240, -380), new Vector3(380, 140, -380), 140, 95));
            }
            case Style.Boat:
            {
                // a flat boat on wheels, pointed at the bow, a big fan in a cage at the stern
                var hull = new Hull(m, 290, sides, 3, 0.5f, (640, 60, 90, 40), (520, 200, 120, 90), (200, 270, 130, 110), (-300, 275, 130, 110), (-520, 260, 120, 100), (-580, 230, 110, 95));
                hull.Skin(m, (band, k) => k >= sides / 2 - 1 && k <= sides / 2 + 1 ? p.Second : band == 0 && variant == 1 ? p.Trim : p.Main);
                hull.Nose(m, 660, p.Main, 30);
                hull.Tail(m, -590, p.Second);
                if (trim)
                {
                    var hub = new Vector3(0, hull.Cy + 330, -560);
                    var cage = m.Loop(2, hub, new Vector3(220, 0, 0), new Vector3(0, 220, 0), 8);
                    for (var k = 0; k < 8; k++) m.Line(cage[k], cage[(k + 1) % 8], Steel + 6);
                    var centre = m.P(2, hub);
                    foreach (var a in new[] { 0.4f, 2.5f, 4.6f }) m.Both(new[] { centre, m.P(2, hub + new Vector3(190 * MathF.Cos(a), 190 * MathF.Sin(a), 0)), m.P(2, hub + new Vector3(190 * MathF.Cos(a + 0.5f), 190 * MathF.Sin(a + 0.5f), 0)) }, p.Trim);
                    m.Strut(2, hull.OnTop(0, -480, -4), hub - new Vector3(0, 0, -20), 30, Steel);
                }
                return new Parts(hull, new Cabin(40, rx, 200, Up: 18, Rim: p.Second, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Second, p.TrimFlat), new Axle(new Vector3(220, 230, 360), new Vector3(400, 140, 360), 140, 95), new Axle(new Vector3(230, 230, -380), new Vector3(420, 150, -380), 150, 105));
            }
            case Style.Monster:
            {
                // a short body high up on huge wheels, a cage over the seat
                var hull = new Hull(m, 460, sides, 5, 0.5f, (520, 230, 100, 90), (420, 262, 130, 110), (-380, 262, 140, 110), (-500, 235, 110, 95));
                hull.Skin(m, (band, k) => k >= sides / 2 - 1 && k <= sides / 2 ? Dark : variant == 1 && band == 0 ? p.Trim : p.Main);
                hull.Nose(m, 532, p.Second);
                hull.Tail(m, -512, p.Second);
                if (trim)
                {
                    foreach (var side in Sides) m.Sphere(m.P(2, new(side * 160, hull.Cy + 50, 528)), 44, Lamp);
                    var top = hull.Top(0, -60) + 360;
                    foreach (var side in Sides)
                    {
                        m.Rod(2, hull.OnTop(side * 230, 200, 0), new Vector3(side * 200, top, 120), Steel + 6);
                        m.Rod(2, hull.OnTop(side * 230, -300, 0), new Vector3(side * 200, top, -260), Steel + 6);
                    }
                    m.Rod(2, new Vector3(-200, top, 120), new Vector3(200, top, 120), Steel + 6);
                    m.Rod(2, new Vector3(-200, top, -260), new Vector3(200, top, -260), Steel + 6);
                }
                return new Parts(hull, new Cabin(-60, rx, 200, Rim: Dark, Rail: p.TrimFlat),
                    Look(p.Second, Dark, p.Main, p.TrimFlat), new Axle(new Vector3(240, 400, 360), new Vector3(470, 255, 360), 255, 170), new Axle(new Vector3(240, 400, -360), new Vector3(470, 255, -360), 255, 170));
            }
            case Style.Robot:
            {
                // square and metal, a grille of lines, round lamps, an aerial with a light on it
                var hull = new Hull(m, 310, sides, 9, 0.5f, (600, 230, 150, 120), (520, 260, 175, 130), (-500, 260, 175, 130), (-590, 235, 150, 120));
                hull.Skin(m, (band, k) => band == 1 ? (k >= sides / 2 - 1 && k <= sides / 2 ? Dark : k % 3 == 0 ? p.Main : Steel + 2) : p.Main);
                hull.Nose(m, 605, Steel + 2);
                hull.Tail(m, -595, Steel + 2);
                if (trim)
                {
                    for (var i = -3; i <= 3; i++) m.Line(m.P(2, new(i * 50, hull.Cy - 60, 604)), m.P(2, new(i * 50, hull.Cy + 110, 604)), Dark + 2);
                    foreach (var side in Sides) m.Sphere(m.P(2, new(side * 175, hull.Cy + 40, 608)), 46, Cyan);
                    m.Rod(2, hull.OnTop(-170, -420, 0), hull.OnTop(-170, -420, 420), Steel + 6);
                    m.Sphere(m.P(2, hull.OnTop(-170, -420, 440)), 30, RedFlat + 2);
                }
                return new Parts(hull, new Cabin(-40, rx, 200, Rim: Steel + 2, Screen: 230, ScreenHalf: rx - 10, Rail: Steel + 6),
                    Look(Steel, Dark, Steel + 2, Cyan), new Axle(new Vector3(250, 270, 380), new Vector3(450, 175, 380), 175, 125), new Axle(new Vector3(250, 270, -380), new Vector3(450, 175, -380), 175, 125));
            }
            case Style.Basket:
            {
                // a round tub like a basket, woven bands round it, the passenger looking over its rim
                var hull = Lens(m, 330, sides, 480, 240, 150, new[] { 460f, 380, 220, 0, -220, -380, -460 }, 3, 0.5f);
                hull.Skin(m, (band, k) => (band + k) % 2 == 0 ? p.Main : variant == 2 ? p.Trim : p.Second);
                hull.Nose(m, 482, p.Main);
                hull.Tail(m, -482, p.Main);
                if (trim) foreach (var side in Sides) m.Sphere(m.P(2, new(side * 150, hull.Cy + 60, 470)), 40, Lamp);
                return new Parts(hull, new Cabin(0, Math.Max(rx, 230), 230, Up: 14, Rim: p.Second, Rail: p.TrimFlat),
                    Look(Steel, Dark, p.Second, p.TrimFlat), new Axle(new Vector3(240, 280, 300), new Vector3(420, 150, 300), 150, 100), new Axle(new Vector3(240, 280, -300), new Vector3(420, 150, -300), 150, 100));
            }
            default:
            {
                // a car you can see through: pale and glassy, the wheels too
                var hull = new Hull(m, 300, sides, 2.6f, 0.5f, (620, 130, 70, 80), (520, 225, 110, 110), (150, 262, 140, 125), (-250, 262, 150, 125), (-520, 230, 120, 110), (-610, 150, 80, 85));
                for (var i = 0; i + 1 < hull.Rings.Count; i++)
                {
                    var a = hull.Rings[i]; var b = hull.Rings[i + 1];
                    for (var k = 0; k < a.Length; k++) m.Both(new[] { a[k], a[(k + 1) % a.Length], b[(k + 1) % a.Length], b[k] }, Pane, CarMesh.SeeThrough);
                    if (trim) m.Line(a[0], b[0], p.MainFlat);
                }
                return new Parts(hull, new Cabin(-60, rx, 200, Rim: p.Main, Rail: p.MainFlat),
                    Look(p.Main, p.Second, p.Main, p.MainFlat), new Axle(new Vector3(240, 262, 400), new Vector3(430, 160, 400), 160, 110), new Axle(new Vector3(250, 268, -400), new Vector3(450, 170, -400), 170, 120));
            }
        }
    }

    // The car finished with its driver: as Finish, for a driver sat in whole too (no arms: its hands kept to itself, no wheel).
    private static Body FinishCast(CarMesh m, Parts parts, Body character, Body racer, CastFit fit, CarDriver.Painter? paint, int lean)
    {
        var (hull, c, look, front, rear) = parts;
        var d = fit.Driver;
        m.Light = 1;
        var rimTop = hull.Top(0, c.Z) + c.Up;
        Cockpit(m, hull, c.Z, c.Rx, c.Rz, rimTop, lean >= 2 ? 8 : c.Sides, c.Rim);
        if (!float.IsNaN(c.Screen) && lean < 2) Windscreen(m, hull, c.Screen, c.ScreenHalf, 100, c.Rail);
        Wheels(m, look, front, rear);
        m.Light = 1;

        var driver = new CarDriver(character);
        // (one sat in whole sits on the cockpit's floor, which lies at the rim's top: a flat one -- the flying rat -- was under it)
        var seat = new Vector3(0, rimTop - 6 + (fit.Whole ? 30 : 0), c.Z);
        Vector3 Place(Vector3 p) => (p - d.Origin) * d.Scale + seat;
        Vector3 Unplace(Vector3 p) => (p - seat) / d.Scale + d.Origin;
        var grip = new Vector3(d.GripX, rimTop - 6 + d.GripHeight, c.Z + d.Reach);
        int[] Fore((int, int[]) arm) => arm.Item2.Length > 0 ? arm.Item2 : driver.Below(arm.Item1).Where(b => d.Drop is null || !d.Drop.Contains(b)).ToArray();
        Vector3? right = d.Right is { } r && Fore(r).Length > 0 ? Place(driver.Reach(r.Item1, Fore(r), Unplace(grip), 1).Hand) : null;
        Vector3? left = d.Left is { } l && Fore(l).Length > 0 ? Place(driver.Reach(l.Item1, Fore(l), Unplace(grip with { X = -grip.X }), -1).Hand) : null;
        m.Pivot(13, m.P(2, seat));
        // (a point of its own for the driver's bone, whatever of it shows over the rim)
        m.P(13, seat + new Vector3(0, 10, 0));
        var point = driver.Seat(m, driver.AllBut(d.Drop), Place, rimTop, d.Scale, paint, cut: true);
        Arms(m, d.Right is { } ra ? point(character.Bones[ra.Item1].Pivot) : m.P(13, seat + new Vector3(60, 120, 0)),
                d.Left is { } la ? point(character.Bones[la.Item1].Pivot) : m.P(13, seat + new Vector3(-60, 120, 0)));
        if (right is not null || left is not null)
        {
            var rh = right ?? left!.Value with { X = -left.Value.X }; var lh = left ?? right!.Value with { X = -right.Value.X };
            SteeringWheel(m, hull, rh, lh, Steel, Dark);
        }
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- installing

    // One line of RACECARS.JSON: a car by its number -- its name, who drives, the island, the entity and body number it is in the entity
    // table, and its BODY.HQR bodies (its own and its half-size copy, -1 none).
    public sealed record CatalogueEntry(int Number, string Name, string Driver, string Island, int Entity, int Generic, int Body, int Small);

    // Adds the cast's cars to a game folder (after the cars made by hand and their half-size copies, RaceTrackSmallCars): the entities they
    // are bodies of -- copies of the racer's animations -- each car's body and its half-size copy at the end of BODY.HQR, and RACECARS.JSON
    // with every car of the folder. Returns lines for the build's log. (`only`: some of the cast, for a look at them)
    public static List<string> InstallCast(string gameDirectory, IReadOnlyCollection<int>? only = null)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        var racer = Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2);
        var table = HqrArchive.Open(ressPath).Read(44);
        var bodyFile = File.ReadAllBytes(bodyPath);
        var next = HqrArchive.CountEntries(bodyPath);
        var log = new List<string>();
        var cast = Cast;
        var entities = new List<int>();
        var entries = new List<CatalogueEntry>();
        var failed = 0;
        for (var i = 0; i < cast.Length; i++)
        {
            if (only is not null && !only.Contains(CastFirst + i)) continue;
            var c = cast[i];
            Body car;
            try { car = BuildCast(c, i, Body.Read(bodies.Read(c.Character), 2), racer); }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException)
            {
                log.Add($"{CastName(c)} (car {CastFirst + i}): not made -- {e.Message}");
                failed++;
                continue;
            }
            while (entities.Count <= i / CastPerEntity)
            {
                (table, var made) = WithEntityLike(table, RaceTrackBaldinoCar.RacerEntity);
                entities.Add(made);
            }
            var entity = entities[i / CastPerEntity]; var generic = i % CastPerEntity;
            var bytes = car.Write();
            bodyFile = HqrWriter.AppendEntry(bodyFile, HqrWriter.StoredEntry(bytes));
            var own = next++;
            bodyFile = HqrWriter.AppendEntry(bodyFile, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(bytes)));
            var small = next++;
            table = RaceTrackBaldinoCar.WithBody(table, entity, generic, own);
            table = RaceTrackBaldinoCar.WithBody(table, entity, RaceTrackSmallCars.SmallOf(generic), small);
            entries.Add(new CatalogueEntry(CastFirst + i, CastName(c), c.Driver, c.Island, entity, generic, own, small));
        }
        File.WriteAllBytes(bodyPath, bodyFile);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        // the catalogue: the cars made by hand first (the racer's entity), then the cast's
        var racerEntity = Lba2EntityTable.Parse(table).Entities.FirstOrDefault(e => e.Id == RaceTrackBaldinoCar.RacerEntity);
        var hand = new List<CatalogueEntry>();
        if (racerEntity is not null)
        {
            var names = new Dictionary<int, (string Name, string Driver, string Island)> { [0] = ("The racer's car", "the racer of the Desert Island track", Desert), [1] = ("Baldino's rocket car", "Jerome Baldino", Desert) };
            foreach (var car in All) names.TryAdd(car.Generic, (car.Name, car.Driver, car.Island));
            foreach (var (generic, body) in racerEntity.Bodies.Where(b => b.Generic < RaceTrackSmallCars.First).OrderBy(b => b.Generic))
            {
                var small = racerEntity.Bodies.Where(b => b.Generic == RaceTrackSmallCars.SmallOf(generic)).Select(b => b.Body).DefaultIfEmpty(-1).First();
                var (name, who, island) = names.TryGetValue(generic, out var n) ? n : ($"Car {generic}", "", "");
                hand.Add(new CatalogueEntry(generic, name, who, island, RaceTrackBaldinoCar.RacerEntity, generic, body, small));
            }
        }
        File.WriteAllText(Path.Combine(gameDirectory, CatalogueFile), JsonSerializer.Serialize(hand.Concat(entries).ToList(), new JsonSerializerOptions { WriteIndented = true }));
        log.Add($"the cast's cars: {entries.Count} made ({failed} not), cars {CastFirst}-{CastFirst + cast.Length - 1}, bodies of entities {string.Join(", ", entities)} (copies of the racer's), each with its half-size copy; " +
                $"{CatalogueFile} lists all {hand.Count + entries.Count} cars");
        return log;
    }

    // The game folder's cars (RACECARS.JSON), or none.
    public static List<CatalogueEntry> Catalogue(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, CatalogueFile);
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<List<CatalogueEntry>>(File.ReadAllText(path)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { DebugLog.Log($"RaceTrackCharacterCars.Catalogue: {e.Message}"); return new(); }
    }

    // The entity table with a new entity at its end: the animations of `like` (its records less its bodies), no bodies yet.
    private static (byte[] Table, int Entity) WithEntityLike(byte[] table, int like)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;
        var offsets = Enumerable.Range(0, count + 1).Select(i => BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(i * 4))).ToArray();
        var records = new List<byte>();
        for (int p = offsets[like], end = offsets[like + 1]; p < end && table[p] != 255;)
        {
            var command = table[p];
            var size = command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
            if (command != 1) records.AddRange(table.AsSpan(p, size).ToArray());
            p += size;
        }
        records.Add(255);
        // (the offsets one longer: every record 4 bytes on; the new entity's records after the last one's)
        var data = table.AsSpan(offsets[0], offsets[count] - offsets[0]).ToArray();
        var result = new byte[(count + 2) * 4 + data.Length + records.Count];
        for (var i = 0; i <= count; i++) BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(i * 4), offsets[i] + 4);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan((count + 1) * 4), offsets[count] + 4 + records.Count);
        data.CopyTo(result.AsSpan((count + 2) * 4));
        records.ToArray().CopyTo(result.AsSpan((count + 2) * 4 + data.Length));
        return (result, count);
    }
}
