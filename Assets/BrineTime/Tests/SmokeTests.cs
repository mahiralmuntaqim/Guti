using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BrineTime.Tests
{
    /// <summary>
    /// Play-mode smoke tests that drive the real game: cast, bite, fight, sell, buy; ferry + island
    /// tables + every model + every sound; and a FishNet host session. The two-process co-op test runs
    /// against a built player instead (see run_tests.ps1).
    /// </summary>
    public class SmokeTests
    {
        class Result { public string Value; }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Bootstrap.TestMode = true;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsNotNull(Bootstrap.Instance, "Bootstrap missing from Main.unity");
            Bootstrap.Instance.Player.Automation = true;
            yield return new WaitForSeconds(0.5f);  // settle on the dock
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NetSession.Leave();
            yield return null;
        }

        static IEnumerator FishOnce(PlayerController p, Result result)
        {
            var f = p.Fishing;
            yield return new WaitForSeconds(0.3f);
            f.AutoReel = 1;
            yield return new WaitForSeconds(0.75f);  // charge
            f.AutoReel = 0;
            float waited = 0;
            while (f.State != Fishing.FishState.Bite && waited < 15f) { waited += Time.deltaTime; yield return null; }
            if (f.State != Fishing.FishState.Bite) { f.ResetLine(); result.Value = "no_bite"; yield break; }
            int before = GameState.Cooler.Count;
            yield return new WaitForSeconds(0.15f);  // human reaction time
            f.AutoReel = 1;  // hook it
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            while (f.State == Fishing.FishState.Fight)
            {
                f.AutoReel = f.Tension < 0.7f ? 1 : 0;  // reel in bursts
                yield return new WaitForFixedUpdate();
            }
            f.AutoReel = 0;
            result.Value = GameState.Cooler.Count > before ? "caught" : "lost";
        }

        static Stall Market()
        {
            foreach (var s in Object.FindObjectsByType<Stall>())
                if (s.Type == Stall.Kind.Market && World.IslandAt(s.transform.position) == GameState.CurrentIsland) return s;
            return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CoreLoop_CastFightSellBuy()
        {
            var p = Bootstrap.Instance.Player;
            int caught = 0, attempts = 0;
            var r = new Result();
            while (GameState.Cooler.Count < 3 && attempts++ < 12)
            {
                yield return FishOnce(p, r);
                if (r.Value == "caught") caught++;
                Debug.Log($"[smoke] attempt {attempts}: {r.Value}  cooler={GameState.Cooler.Count}");
            }
            Assert.Greater(caught, 0, "never caught anything");
            int sold = GameState.Cooler.Count;
            Market().Interact(p);
            Assert.AreEqual(0, GameState.Cooler.Count, "market didn't buy the catch");
            Assert.Greater(GameState.Money, GameState.StartingMoney);
            Assert.IsTrue(GameState.BuyBait(1), "couldn't buy worms");
            Debug.Log($"[smoke] sold {sold}, money now ${GameState.Money}");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Islands_ModelsSoundsFerryTables()
        {
            foreach (var c in Data.CreatureList)
                foreach (bool golden in new[] { false, true })
                {
                    var m = FishModel.Build(c.Id, 1f, golden);
                    Assert.Greater(m.GetComponentsInChildren<MeshRenderer>().Length, 0, c.Id + " has no renderers");
                    Object.Destroy(m);
                }
            foreach (var s in Sfx.Names) Assert.IsTrue(Sfx.Has(s), "missing sound " + s);

            Assert.IsFalse(GameState.UnlockIsland(1), "Coral Key unlocked too early");
            foreach (var id in new[] { "puddle_minnow", "grumpy_perch", "stripe_bass", "mud_gulper", "pebblefin" })
                GameState.Journal[id] = new JournalEntry { id = id, count = 1, best = 1f };
            GameState.Money = 10000;
            Assert.IsTrue(GameState.UnlockIsland(1));
            var p = Bootstrap.Instance.Player;
            Bootstrap.Instance.Hud.TravelTo(1);
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(1, GameState.CurrentIsland);
            Assert.Less(Vector3.Distance(p.transform.position, World.SpawnPoint(1)), 3f, "didn't arrive at Coral Key's dock");

            var rng = new System.Random(5);
            bool wrasse = false, squid = false, brineWrasse = false;
            for (int i = 0; i < 3000; i++)
            {
                var id = Data.RollCreature(2, 1, rng);
                wrasse |= id == "candy_wrasse";
                squid |= id == "prism_squid";
                brineWrasse |= Data.RollCreature(2, 0, rng) == "candy_wrasse";
            }
            Assert.IsTrue(wrasse && squid && !brineWrasse, "island-exclusive tables wrong");

            GameState.RodLevel = 1;
            Assert.IsTrue(GameState.BuyRod(), "Coral Key should sell the Carbon Rod");
            Assert.IsFalse(GameState.BuyRod(), "Coral Key shouldn't sell the Storm Rod");

            var r = new Result();
            for (int i = 0; i < 4 && r.Value != "caught"; i++) yield return FishOnce(p, r);
            Assert.AreEqual("caught", r.Value, "couldn't catch anything on Coral Key");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Network_HostSpawnsPresence()
        {
            NetSession.LocalName = "HostTester";
            Assert.AreEqual("", NetSession.Host());
            float t = 0;
            while ((NetPlayer.Local == null || NetPlayer.Local.DisplayName != "HostTester") && t < 10f) { t += Time.deltaTime; yield return null; }
            Assert.IsNotNull(NetPlayer.Local, "host never got a presence object");
            Assert.AreEqual("HostTester", NetPlayer.Local.DisplayName);
            Assert.IsTrue(NetSession.IsHost);
            // the presence object follows the local player
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Distance(NetPlayer.Local.transform.position, Bootstrap.Instance.Player.transform.position), 0.5f);
            NetSession.SendEvent("catch", 10, 1f, false, "grumpy_perch");  // must not throw
            yield return null;
        }
    }
}
