using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace BrineTime.Tests
{
    /// <summary>
    /// Deterministic gameplay checks (no scene needed): the fight model keeps the rod/bait tiers the balance
    /// sim promises (docs/gameplay/BALANCE.md), saves are atomic and migrate, Catch of the Day is stable.
    /// Owned by the gameplay agent; QA owns the folder.
    /// </summary>
    public class GameplayTests
    {
        const float Dt = 0.02f;

        /// <summary>
        /// The "human" bot from the balance sim: hold until 75% tension, let go until 30%, 0.15-0.3 s reaction;
        /// attentive also lets go when a lunge is telegraphed. Mirrors run() in docs/gameplay/sim/sim.py.
        /// </summary>
        static FightModel.Outcome Fight(string id, int rod, bool attentive, System.Random r, out float seconds)
        {
            float size = Data.RollSize(r);
            bool golden = r.NextDouble() < Data.GoldenChance;
            var c = Data.Creatures[id];
            var rd = Data.Rods[rod];
            float start = (10f + (float)r.NextDouble() * 12f) * rd.Cast * 0.85f + 1.5f;
            var f = new FightModel(Data.FightStrength(id, size, golden), c.Style, Data.FightStamina(id), rd.Reel, rd.Line, start, r);
            bool hold = true, want = true, lungeSeen = false;
            float delay = 0f;
            int n = 0;
            while (f.Result == FightModel.Outcome.None && n < 9000)
            {
                bool tele = attentive && (f.LungeWarning > 0f || f.Lunging);
                if (want && (f.Tension >= 0.75f || (tele && !lungeSeen)))
                {
                    want = false; delay = 0.15f + (float)r.NextDouble() * 0.15f;
                    if (tele) lungeSeen = true;
                }
                else if (!want && f.Tension <= 0.3f && !tele)
                {
                    want = true; delay = 0.15f + (float)r.NextDouble() * 0.15f;
                }
                if (!tele) lungeSeen = false;
                if (delay > 0f) delay -= Dt; else hold = want;
                f.Step(hold, Dt);
                n++;
            }
            seconds = n * Dt;
            return f.Result;
        }

        static float WinRate(string id, int rod, bool attentive, int n = 300, int seed = 11)
        {
            var r = new System.Random(seed);
            int wins = 0;
            for (int i = 0; i < n; i++) if (Fight(id, rod, attentive, r, out _) == FightModel.Outcome.Land) wins++;
            return wins / (float)n;
        }

        [Test]
        public void FightTiers_EachRodOpensTheNextBait()
        {
            // bread fish on the starter rod, worm fish on Bamboo, glowberry fish on Carbon, spoon fish on Storm
            string[][] tiers =
            {
                new[] { "puddle_minnow", "grumpy_perch" },
                new[] { "stripe_bass", "mud_gulper", "pebblefin" },
                new[] { "snorkel_crab", "glimmer_eel", "moon_carp", "candy_wrasse" },
                new[] { "static_ray", "tidal_tuna", "abyss_lantern" },
            };
            var sb = new StringBuilder("[gameplay] attentive win rates (rod tier = bait tier / next tier):\n");
            for (int t = 0; t < tiers.Length; t++)
            {
                foreach (var id in tiers[t])
                {
                    float own = WinRate(id, t, true);
                    sb.AppendLine($"  {id,-16} {Data.Rods[t].Name,-14} {own:P0}" + (t > 0 ? $"   on {Data.Rods[t - 1].Name}: {WinRate(id, t - 1, true):P0}" : ""));
                    Assert.Greater(own, 0.75f, $"{id} should be reliably catchable on the {Data.Rods[t].Name}");
                    if (t > 0) Assert.Less(WinRate(id, t - 1, true), 0.4f, $"{id} should still be hard on the {Data.Rods[t - 1].Name}");
                }
            }
            foreach (var id in new[] { "old_whiskers", "kraken_pup" })
            {
                float storm = WinRate(id, 3, true), carbon = WinRate(id, 2, true);
                sb.AppendLine($"  {id,-16} Storm {storm:P0}  Carbon {carbon:P0}");
                Assert.Greater(storm, 0.3f, id + " should be beatable with the Storm Rod");
                Assert.Less(carbon, 0.08f, id + " should need the Storm Rod");
            }
            Debug.Log(sb.ToString());
        }

        [Test]
        public void Fight_SnapsOnlyWhileHolding_AndLungesAreTelegraphed()
        {
            var r = new System.Random(3);
            var f = new FightModel(1.2f, FightStyle.Thrasher, 10f, 2.2f, 1f, 15f, r);
            bool sawWarning = false;
            float lastWarning = 0f;
            for (int i = 0; i < 3000 && f.Result == FightModel.Outcome.None; i++)
            {
                bool wasLunging = f.Lunging;
                f.Step(false, Dt);  // never reeling
                if (f.LungeWarning > 0f) { sawWarning = true; lastWarning = f.LungeWarning; }
                if (f.Lunging && !wasLunging) Assert.Greater(lastWarning, 0.9f, "a lunge must be preceded by a full telegraph");
                if (!f.Lunging && f.LungeWarning == 0f) lastWarning = 0f;
            }
            Assert.IsTrue(sawWarning);
            Assert.AreEqual(FightModel.Outcome.Escape, f.Result, "slack line can never snap");
            Assert.Less(f.Tension, 0.01f);
        }

        [Test]
        public void Fight_IsDeterministicForASeed()
        {
            string Run()
            {
                var r = new System.Random(42);
                var f = new FightModel(0.8f, FightStyle.Runner, 10f, 3f, 1.3f, 14f, r);
                for (int i = 0; i < 500; i++) f.Step(i % 50 < 30, Dt);
                return $"{f.Tension:F4} {f.Distance:F4} {f.Fatigue:F4} {f.Result}";
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void ChargeCurve_EasesInAndWobblesNearFull()
        {
            Assert.AreEqual(0f, Fishing.ChargeCurve(0f), 1e-5f);
            Assert.Less(Fishing.ChargeCurve(0.5f), 0.5f, "ease-in: half the time is less than half the power");
            float prev = -1f;
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                float c = Fishing.ChargeCurve(t);
                Assert.GreaterOrEqual(c, prev);
                prev = c;
            }
            for (float t = 1f; t < 5f; t += 0.03f)
            {
                float c = Fishing.ChargeCurve(t);
                Assert.That(c, Is.InRange(0.92f, 1.0001f));
            }
        }

        [Test]
        public void Save_IsAtomicAndFallsBackToBackup()
        {
            string dir = Path.Combine(Application.temporaryCachePath, "bt_save_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "save.json");
                GameState.WriteAtomic(path, "one");
                Assert.AreEqual("one", File.ReadAllText(path));
                GameState.WriteAtomic(path, "two");
                Assert.AreEqual("two", File.ReadAllText(path));
                Assert.AreEqual("one", File.ReadAllText(path + ".bak"), "previous save kept as .bak");
                Assert.IsFalse(File.Exists(path + ".tmp"), "temp file cleaned up");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void Save_RoundTripsAndMigratesOldSaves()
        {
            bool persist = GameState.Persist;
            GameState.Persist = false;
            try
            {
                GameState.Reset();
                GameState.Money = 1234;
                GameState.RodLevel = 2;
                GameState.BaitCounts[2] = 7;
                GameState.CurrentBait = 2;
                GameState.UnlockedIslands.Add(1);
                GameState.CurrentIsland = 1;
                GameState.Journal["moon_carp"] = new JournalEntry { id = "moon_carp", count = 3, best = 1.4f };
                GameState.Cooler.Add(new CatchRecord { id = "moon_carp", size = 1.1f, value = 80 });
                string json = GameState.ToJson();
                StringAssert.Contains("\"version\": " + GameState.SaveVersion, json);

                GameState.Reset();
                Assert.IsTrue(GameState.FromJson(json));
                Assert.AreEqual(1234, GameState.Money);
                Assert.AreEqual(2, GameState.RodLevel);
                Assert.AreEqual(7, GameState.BaitCounts[2]);
                Assert.AreEqual(2, GameState.CurrentBait);
                Assert.AreEqual(1, GameState.CurrentIsland);
                Assert.AreEqual(3, GameState.Journal["moon_carp"].count);
                Assert.AreEqual(80, GameState.Cooler[0].value);

                // an unversioned save from the first release, with junk in it
                const string old = "{\"money\":-5,\"baitCounts\":[0,3],\"currentBait\":3,\"rodLevel\":9,\"coolerLevel\":1," +
                    "\"cooler\":[{\"id\":\"grumpy_perch\",\"size\":1.0,\"golden\":false,\"clean\":true,\"value\":0},{\"id\":\"no_such_fish\",\"size\":1}]," +
                    "\"journal\":[{\"id\":\"grumpy_perch\",\"count\":0,\"best\":1.0},{\"id\":\"bogus\",\"count\":2}],\"lifetimeEarnings\":50," +
                    "\"currentIsland\":2,\"unlockedIslands\":[0,2,7]}";
                Assert.IsTrue(GameState.FromJson(old));
                Assert.AreEqual(0, GameState.Money, "negative money clamped");
                Assert.AreEqual(3, GameState.BaitCounts[1]);
                Assert.AreEqual(0, GameState.CurrentBait, "no spoons left -> back to bread");
                Assert.AreEqual(Data.Rods.Length - 1, GameState.RodLevel);
                Assert.AreEqual(1, GameState.Cooler.Count, "unknown creatures dropped");
                Assert.Greater(GameState.Cooler[0].value, 0, "missing value recomputed");
                Assert.AreEqual(1, GameState.Journal.Count);
                Assert.AreEqual(1, GameState.Journal["grumpy_perch"].count);
                CollectionAssert.AreEquivalent(new[] { 0, 2 }, GameState.UnlockedIslands);
                Assert.AreEqual(2, GameState.CurrentIsland);

                Assert.IsFalse(GameState.FromJson(""));
            }
            finally
            {
                GameState.Reset();
                GameState.Persist = persist;
            }
        }

        [Test]
        public void CatchOfTheDay_IsStableAndFromTheIsland()
        {
            var day = new DateTime(2026, 10, 16);
            for (int i = 0; i < Data.Islands.Length; i++)
            {
                var pool = Data.IslandSpecies(i);
                string a = Data.CatchOfTheDay(i, day);
                Assert.AreEqual(a, Data.CatchOfTheDay(i, day.AddHours(13)), "same all day");
                CollectionAssert.Contains(pool, a);
                Assert.IsFalse(Data.Creatures[a].Junk);
                Assert.Less(Data.Creatures[a].Rarity, 3);
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int d = 0; d < 60; d++) seen.Add(Data.CatchOfTheDay(i, day.AddDays(d)));
                Assert.Greater(seen.Count, pool.Count / 2, "rotates through the island's species");
            }
            Assert.AreEqual(Mathf.RoundToInt(Data.CatchValue("moon_carp", 1f, false, false) * 1.5f), Data.CatchValue("moon_carp", 1f, false, false, true));
        }
    }
}
