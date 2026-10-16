using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BrineTime
{
    [Serializable]
    public class CatchRecord
    {
        public string id;
        public float size;
        public bool golden;
        public bool clean;
        public int value;
    }

    [Serializable]
    public class JournalEntry
    {
        public string id;
        public int count;
        public float best;
    }

    [Serializable]
    class SaveData
    {
        public int money;
        public int[] baitCounts;
        public int currentBait;
        public int rodLevel;
        public int coolerLevel;
        public List<CatchRecord> cooler;
        public List<JournalEntry> journal;
        public int lifetimeEarnings;
        public int currentIsland;
        public List<int> unlockedIslands;
    }

    /// <summary>Player progress: money, baits, gear, cooler, journal, islands. Saved as JSON.</summary>
    public static class GameState
    {
        public const int StartingMoney = 20;

        public static event Action Changed;
        public static event Action<string, Color> Message;

        public static int Money = StartingMoney;
        public static int[] BaitCounts = new int[4];
        public static int CurrentBait;
        public static int RodLevel;
        public static int CoolerLevel;
        public static List<CatchRecord> Cooler = new List<CatchRecord>();
        public static Dictionary<string, JournalEntry> Journal = new Dictionary<string, JournalEntry>();
        public static int LifetimeEarnings;
        public static int CurrentIsland;
        public static List<int> UnlockedIslands = new List<int> { 0 };
        /// <summary>False in automated tests so the real save isn't touched.</summary>
        public static bool Persist = true;

        static string SavePath => Path.Combine(Application.persistentDataPath, "brinetime_save.json");

        public static void Notify(string text, Color color) => Message?.Invoke(text, color);
        public static void Notify(string text) => Message?.Invoke(text, Color.white);
        static void Emit() => Changed?.Invoke();

        public static RodDef Rod => Data.Rods[RodLevel];
        public static BaitDef Bait => Data.Baits[CurrentBait];
        public static IslandDef Island => Data.Islands[CurrentIsland];
        public static int CoolerSlots => Data.Coolers[CoolerLevel].Slots;
        public static bool CoolerFull => Cooler.Count >= CoolerSlots;
        public static int SpeciesCount => Journal.Count;

        public static bool HasBait(int i) => Data.Baits[i].Infinite || BaitCounts[i] > 0;

        public static void SelectBait(int i)
        {
            if (i < 0 || i >= Data.Baits.Length) return;
            if (!HasBait(i))
            {
                Notify($"You're out of {Data.Baits[i].Name}. Buy some at the General Store.", new Color(1f, 0.6f, 0.4f));
                return;
            }
            CurrentBait = i;
            Emit();
        }

        public static void ConsumeBait()
        {
            if (Bait.Infinite) return;
            BaitCounts[CurrentBait]--;
            if (BaitCounts[CurrentBait] <= 0)
            {
                Notify($"Last {Bait.Name} used. Back to Bread Crust.", new Color(1f, 0.8f, 0.4f));
                CurrentBait = 0;
            }
            Emit();
        }

        public static bool AddCatch(string id, float size, bool golden, bool clean)
        {
            if (CoolerFull) return false;
            Cooler.Add(new CatchRecord { id = id, size = size, golden = golden, clean = clean, value = Data.CatchValue(id, size, golden, clean) });
            if (!Journal.TryGetValue(id, out var e)) Journal[id] = e = new JournalEntry { id = id };
            e.count++;
            e.best = Mathf.Max(e.best, size);
            Emit();
            Save();
            return true;
        }

        public static int CoolerValue()
        {
            int total = 0;
            foreach (var c in Cooler) total += c.value;
            return total;
        }

        public static int SellAll()
        {
            int total = CoolerValue();
            if (total <= 0) return 0;
            Money += total;
            LifetimeEarnings += total;
            Cooler.Clear();
            Emit();
            Save();
            return total;
        }

        static bool Spend(int amount)
        {
            if (Money < amount) return false;
            Money -= amount;
            return true;
        }

        public static bool BuyBait(int i)
        {
            var b = Data.Baits[i];
            if (b.Infinite || Array.IndexOf(Island.Baits, i) < 0 || !Spend(b.Cost)) return false;
            BaitCounts[i] += b.Pack;
            Emit();
            Save();
            return true;
        }

        public static bool BuyRod()
        {
            if (RodLevel + 1 > Island.MaxRod || !Spend(Data.Rods[RodLevel + 1].Cost)) return false;
            RodLevel++;
            Emit();
            Save();
            return true;
        }

        public static bool BuyCooler()
        {
            if (CoolerLevel + 1 > Island.MaxCooler || !Spend(Data.Coolers[CoolerLevel + 1].Cost)) return false;
            CoolerLevel++;
            Emit();
            Save();
            return true;
        }

        /// <summary>Empty if the island can be unlocked now, otherwise the reason it can't.</summary>
        public static string UnlockBlockReason(int i)
        {
            var isl = Data.Islands[i];
            if (SpeciesCount < isl.Species) return $"Discover {isl.Species} species first ({SpeciesCount} / {isl.Species})";
            if (Money < isl.Cost) return $"Ticket costs ${isl.Cost}";
            return "";
        }

        public static bool UnlockIsland(int i)
        {
            if (UnlockedIslands.Contains(i) || UnlockBlockReason(i) != "") return false;
            Money -= Data.Islands[i].Cost;
            UnlockedIslands.Add(i);
            Emit();
            Save();
            return true;
        }

        public static bool Travel(int i)
        {
            if (!UnlockedIslands.Contains(i)) return false;
            CurrentIsland = i;
            Emit();
            Save();
            return true;
        }

        public static void Reset()
        {
            Money = StartingMoney;
            BaitCounts = new int[4];
            CurrentBait = RodLevel = CoolerLevel = LifetimeEarnings = CurrentIsland = 0;
            Cooler = new List<CatchRecord>();
            Journal = new Dictionary<string, JournalEntry>();
            UnlockedIslands = new List<int> { 0 };
            Emit();
        }

        public static void Save()
        {
            if (!Persist) return;
            var d = new SaveData
            {
                money = Money, baitCounts = BaitCounts, currentBait = CurrentBait, rodLevel = RodLevel,
                coolerLevel = CoolerLevel, cooler = Cooler, journal = new List<JournalEntry>(Journal.Values),
                lifetimeEarnings = LifetimeEarnings, currentIsland = CurrentIsland, unlockedIslands = UnlockedIslands,
            };
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(d, true)); }
            catch (Exception e) { Debug.LogWarning("Couldn't save: " + e.Message); }
        }

        public static void Load()
        {
            if (!File.Exists(SavePath)) return;
            SaveData d;
            try { d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)); }
            catch (Exception e) { Debug.LogWarning("Save unreadable, starting fresh: " + e.Message); return; }
            if (d == null) return;
            Money = d.money;
            BaitCounts = new int[4];
            if (d.baitCounts != null)
                for (int i = 0; i < Mathf.Min(4, d.baitCounts.Length); i++) BaitCounts[i] = d.baitCounts[i];
            RodLevel = Mathf.Clamp(d.rodLevel, 0, Data.Rods.Length - 1);
            CoolerLevel = Mathf.Clamp(d.coolerLevel, 0, Data.Coolers.Length - 1);
            Cooler = new List<CatchRecord>();
            if (d.cooler != null)
                foreach (var c in d.cooler) if (c != null && Data.Creatures.ContainsKey(c.id)) Cooler.Add(c);
            Journal = new Dictionary<string, JournalEntry>();
            if (d.journal != null)
                foreach (var e in d.journal) if (e != null && Data.Creatures.ContainsKey(e.id)) Journal[e.id] = e;
            LifetimeEarnings = d.lifetimeEarnings;
            UnlockedIslands = new List<int> { 0 };
            if (d.unlockedIslands != null)
                foreach (var i in d.unlockedIslands) if (i > 0 && i < Data.Islands.Length && !UnlockedIslands.Contains(i)) UnlockedIslands.Add(i);
            CurrentIsland = UnlockedIslands.Contains(d.currentIsland) ? d.currentIsland : 0;
            CurrentBait = Mathf.Clamp(d.currentBait, 0, Data.Baits.Length - 1);
            if (!HasBait(CurrentBait)) CurrentBait = 0;
            Emit();
        }
    }
}
