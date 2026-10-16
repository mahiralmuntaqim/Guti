using System.Collections.Generic;
using UnityEngine;

namespace BrineTime
{
    /// <summary>How a creature's procedural model is built (see FishModel).</summary>
    public class ModelDef
    {
        public string Shape = "fish";   // fish, eel, ray, crab, squid, boot
        public float H = 0.28f;          // body height as a fraction of length
        public float W = 0.14f;          // body width as a fraction of length
        public string Tail = "fork";     // fork, round, none
        public string Pattern = "belly"; // plain, belly, stripes, spots
        public Color Color2 = Color.gray;
        public string Extra = "";        // whiskers, crown, lure, glow, snorkel, frown
    }

    public class CreatureDef
    {
        public string Id;
        public string Name;
        public int Worth;          // base sell price at size 1.0
        public int Rarity;         // 0 common .. 3 legendary
        public float Strength;     // how hard it fights (0.2 .. 1.6)
        public float Length;       // metres at size 1.0
        public bool Junk;
        public Color Color;
        public ModelDef Model;
    }

    public class BaitDef
    {
        public string Id;
        public string Name;
        public int Cost;
        public int Pack;
        public bool Infinite;
        public float Consume;      // chance a catch uses up one bait
        public Vector2 Bite;       // min/max seconds before a bite
        public Dictionary<string, int> Table;
    }

    public class RodDef
    {
        public string Name;
        public int Cost;
        public float Reel;   // metres per second while reeling
        public float Line;   // higher = tension builds slower
    }

    public class CoolerDef
    {
        public string Name;
        public int Cost;
        public int Slots;
    }

    public class IslandDef
    {
        public string Id;
        public string Name;
        public Vector3 Origin;
        public string Theme;     // palm, coral, storm
        public Color Sand, Grass, Rock;
        public int[] Baits;      // bait indices sold in this island's store
        public int MaxRod;
        public int MaxCooler;
        public int Cost;         // ferry ticket
        public int Species;      // journal species needed before the ticket can be bought
        public Dictionary<string, Dictionary<string, int>> Exclusive; // bait id -> creature -> weight
    }

    /// <summary>All content and balance numbers. Tweak here to rebalance the game.</summary>
    public static class Data
    {
        public const float GoldenChance = 0.04f;
        public const float GoldenMult = 3f;
        public const float CleanCatchMult = 1.2f;
        public const float CleanTensionLimit = 0.85f;

        public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Legendary" };
        public static readonly Color[] RarityColors =
        {
            new Color(0.85f, 0.85f, 0.85f), new Color(0.45f, 0.9f, 0.45f),
            new Color(0.4f, 0.65f, 1f), new Color(1f, 0.7f, 0.2f),
        };

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static ModelDef M(string shape, float h, float w, string tail, string pattern, Color c2, string extra = "") =>
            new ModelDef { Shape = shape, H = h, W = w, Tail = tail, Pattern = pattern, Color2 = c2, Extra = extra };

        static CreatureDef Cr(string id, string name, int worth, int rarity, float strength, float length, Color color, ModelDef model, bool junk = false) =>
            new CreatureDef { Id = id, Name = name, Worth = worth, Rarity = rarity, Strength = strength, Length = length, Color = color, Model = model, Junk = junk };

        public static readonly List<CreatureDef> CreatureList = new List<CreatureDef>
        {
            Cr("soggy_boot", "Soggy Boot", 1, 0, 0.25f, 0.30f, C(0.35f, 0.25f, 0.18f), M("boot", 0, 0, "none", "plain", C(0.2f, 0.15f, 0.1f)), junk: true),
            Cr("puddle_minnow", "Puddle Minnow", 3, 0, 0.35f, 0.15f, C(0.72f, 0.8f, 0.84f), M("fish", 0.22f, 0.12f, "fork", "belly", C(0.95f, 0.95f, 0.95f))),
            Cr("grumpy_perch", "Grumpy Perch", 7, 0, 0.5f, 0.28f, C(0.55f, 0.62f, 0.3f), M("fish", 0.32f, 0.14f, "round", "stripes", C(0.25f, 0.3f, 0.15f), "frown")),
            Cr("stripe_bass", "Stripe Bass", 14, 0, 0.65f, 0.42f, C(0.5f, 0.58f, 0.45f), M("fish", 0.27f, 0.13f, "fork", "stripes", C(0.18f, 0.22f, 0.18f))),
            Cr("mud_gulper", "Mud Gulper", 18, 0, 0.8f, 0.5f, C(0.45f, 0.36f, 0.25f), M("fish", 0.26f, 0.2f, "round", "spots", C(0.3f, 0.24f, 0.16f), "whiskers")),
            Cr("pebblefin", "Pebblefin", 26, 1, 0.55f, 0.3f, C(0.6f, 0.58f, 0.55f), M("fish", 0.38f, 0.18f, "round", "spots", C(0.4f, 0.38f, 0.36f))),
            Cr("snorkel_crab", "Snorkel Crab", 48, 1, 0.7f, 0.35f, C(0.9f, 0.4f, 0.25f), M("crab", 0, 0, "none", "plain", C(1f, 0.85f, 0.3f), "snorkel")),
            Cr("glimmer_eel", "Glimmer Eel", 55, 1, 0.9f, 0.9f, C(0.3f, 0.85f, 0.8f), M("eel", 0.07f, 0.06f, "none", "spots", C(0.9f, 1f, 0.6f), "glow")),
            Cr("moon_carp", "Moon Carp", 70, 1, 0.75f, 0.6f, C(0.9f, 0.9f, 0.75f), M("fish", 0.34f, 0.16f, "fork", "spots", C(0.85f, 0.5f, 0.2f), "whiskers")),
            Cr("candy_wrasse", "Candy Wrasse", 85, 1, 0.7f, 0.35f, C(1f, 0.55f, 0.75f), M("fish", 0.3f, 0.12f, "round", "stripes", C(1f, 0.95f, 0.5f))),
            Cr("static_ray", "Static Ray", 190, 1, 1.0f, 1.1f, C(0.35f, 0.4f, 0.75f), M("ray", 0, 0, "none", "spots", C(0.8f, 0.9f, 1f), "glow")),
            Cr("tidal_tuna", "Tidal Tuna", 240, 1, 1.25f, 1.3f, C(0.25f, 0.35f, 0.55f), M("fish", 0.26f, 0.18f, "fork", "belly", C(0.85f, 0.88f, 0.9f))),
            Cr("crowned_sunfish", "Crowned Sunfish", 260, 2, 1.1f, 0.8f, C(1f, 0.8f, 0.3f), M("fish", 0.62f, 0.14f, "round", "belly", C(1f, 0.95f, 0.75f), "crown")),
            Cr("prism_squid", "Prism Squid", 320, 2, 0.95f, 0.6f, C(0.7f, 0.5f, 1f), M("squid", 0, 0, "none", "plain", C(0.5f, 1f, 0.95f), "glow")),
            Cr("abyss_lantern", "Abyss Lantern", 420, 2, 1.05f, 0.7f, C(0.15f, 0.12f, 0.25f), M("fish", 0.4f, 0.22f, "round", "spots", C(0.3f, 0.25f, 0.45f), "lure")),
            Cr("thunderhead_grouper", "Thunderhead Grouper", 650, 2, 1.3f, 1.2f, C(0.32f, 0.34f, 0.4f), M("fish", 0.36f, 0.24f, "round", "stripes", C(0.95f, 0.9f, 0.3f))),
            Cr("old_whiskers", "Old Whiskers", 1800, 3, 1.6f, 2.0f, C(0.5f, 0.42f, 0.32f), M("fish", 0.24f, 0.2f, "round", "spots", C(0.35f, 0.28f, 0.2f), "whiskers")),
            Cr("kraken_pup", "Kraken Pup", 2600, 3, 1.55f, 1.0f, C(0.55f, 0.15f, 0.35f), M("squid", 0, 0, "none", "plain", C(1f, 0.6f, 0.7f))),
        };

        public static readonly Dictionary<string, CreatureDef> Creatures = BuildIndex();

        static Dictionary<string, CreatureDef> BuildIndex()
        {
            var d = new Dictionary<string, CreatureDef>();
            foreach (var c in CreatureList) d[c.Id] = c;
            return d;
        }

        public static readonly BaitDef[] Baits =
        {
            new BaitDef { Id = "bread", Name = "Bread Crust", Cost = 0, Pack = 0, Infinite = true, Consume = 0f, Bite = new Vector2(4f, 7f),
                Table = new Dictionary<string, int> { { "puddle_minnow", 50 }, { "grumpy_perch", 30 }, { "soggy_boot", 20 } } },
            new BaitDef { Id = "worm", Name = "Wriggle Worm", Cost = 12, Pack = 5, Consume = 0.6f, Bite = new Vector2(2.5f, 5f),
                Table = new Dictionary<string, int> { { "grumpy_perch", 25 }, { "stripe_bass", 30 }, { "mud_gulper", 25 }, { "pebblefin", 15 }, { "soggy_boot", 5 } } },
            new BaitDef { Id = "glowberry", Name = "Glowberry", Cost = 90, Pack = 5, Consume = 0.5f, Bite = new Vector2(2f, 4f),
                Table = new Dictionary<string, int> { { "pebblefin", 15 }, { "glimmer_eel", 28 }, { "moon_carp", 24 }, { "snorkel_crab", 28 }, { "crowned_sunfish", 5 } } },
            new BaitDef { Id = "spoon", Name = "Thunder Spoon", Cost = 600, Pack = 3, Consume = 0.35f, Bite = new Vector2(1.5f, 3f),
                Table = new Dictionary<string, int> { { "glimmer_eel", 20 }, { "static_ray", 30 }, { "tidal_tuna", 30 }, { "abyss_lantern", 16 }, { "old_whiskers", 4 } } },
        };

        public static readonly RodDef[] Rods =
        {
            new RodDef { Name = "Driftwood Rod", Cost = 0, Reel = 2.2f, Line = 1.0f },
            new RodDef { Name = "Bamboo Rod", Cost = 150, Reel = 3.0f, Line = 1.3f },
            new RodDef { Name = "Carbon Rod", Cost = 900, Reel = 4.0f, Line = 1.7f },
            new RodDef { Name = "Storm Rod", Cost = 4000, Reel = 5.2f, Line = 2.2f },
        };

        public static readonly CoolerDef[] Coolers =
        {
            new CoolerDef { Name = "Bucket", Cost = 0, Slots = 4 },
            new CoolerDef { Name = "Cooler", Cost = 200, Slots = 8 },
            new CoolerDef { Name = "Ice Chest", Cost = 1200, Slots = 14 },
        };

        public static readonly IslandDef[] Islands =
        {
            new IslandDef { Id = "brine", Name = "Brine Island", Origin = Vector3.zero, Theme = "palm",
                Sand = C(0.86f, 0.78f, 0.55f), Grass = C(0.36f, 0.62f, 0.3f), Rock = C(0.45f, 0.45f, 0.48f),
                Baits = new[] { 0, 1, 2 }, MaxRod = 1, MaxCooler = 1, Cost = 0, Species = 0,
                Exclusive = new Dictionary<string, Dictionary<string, int>>() },
            new IslandDef { Id = "coral", Name = "Coral Key", Origin = new Vector3(420, 0, -160), Theme = "coral",
                Sand = C(0.96f, 0.88f, 0.82f), Grass = C(0.5f, 0.74f, 0.42f), Rock = C(0.95f, 0.5f, 0.55f),
                Baits = new[] { 0, 1, 2 }, MaxRod = 2, MaxCooler = 2, Cost = 250, Species = 5,
                Exclusive = new Dictionary<string, Dictionary<string, int>>
                {
                    { "worm", new Dictionary<string, int> { { "candy_wrasse", 10 } } },
                    { "glowberry", new Dictionary<string, int> { { "candy_wrasse", 18 }, { "prism_squid", 9 } } },
                    { "spoon", new Dictionary<string, int> { { "prism_squid", 10 } } },
                } },
            new IslandDef { Id = "storm", Name = "Stormrock", Origin = new Vector3(-360, 0, -520), Theme = "storm",
                Sand = C(0.58f, 0.57f, 0.55f), Grass = C(0.3f, 0.42f, 0.32f), Rock = C(0.3f, 0.3f, 0.34f),
                Baits = new[] { 0, 1, 2, 3 }, MaxRod = 3, MaxCooler = 2, Cost = 1500, Species = 10,
                Exclusive = new Dictionary<string, Dictionary<string, int>>
                {
                    { "glowberry", new Dictionary<string, int> { { "thunderhead_grouper", 5 } } },
                    { "spoon", new Dictionary<string, int> { { "thunderhead_grouper", 18 }, { "kraken_pup", 4 }, { "old_whiskers", 3 } } },
                } },
        };

        public static Dictionary<string, int> CatchTable(int bait, int island)
        {
            var table = new Dictionary<string, int>(Baits[bait].Table);
            if (Islands[island].Exclusive.TryGetValue(Baits[bait].Id, out var extra))
                foreach (var kv in extra)
                    table[kv.Key] = (table.TryGetValue(kv.Key, out var w) ? w : 0) + kv.Value;
            return table;
        }

        public static string RollCreature(int bait, int island, System.Random rng)
        {
            var table = CatchTable(bait, island);
            float total = 0;
            foreach (var w in table.Values) total += w;
            float r = (float)rng.NextDouble() * total;
            string last = null;
            foreach (var kv in table)
            {
                last = kv.Key;
                r -= kv.Value;
                if (r <= 0) return kv.Key;
            }
            return last;
        }

        /// <summary>Gaussian size roll (mean 1.0, sigma 0.18), clamped 0.45..1.9.</summary>
        public static float RollSize(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double n = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2);
            return Mathf.Clamp(1f + 0.18f * (float)n, 0.45f, 1.9f);
        }

        public static int CatchValue(string id, float size, bool golden, bool clean)
        {
            float v = Creatures[id].Worth * size;
            if (golden) v *= GoldenMult;
            if (clean) v *= CleanCatchMult;
            return Mathf.Max(1, Mathf.RoundToInt(v));
        }

        public static float FightStrength(string id, float size, bool golden)
        {
            float s = Creatures[id].Strength * Mathf.Lerp(0.85f, 1.2f, Mathf.InverseLerp(0.45f, 1.9f, size));
            return golden ? s * 1.25f : s;
        }

        public static string CatchTitle(string id, bool golden) => (golden ? "Golden " : "") + Creatures[id].Name;
    }
}
