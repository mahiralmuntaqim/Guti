using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BrineTime
{
    /// <summary>All 2D UI: money, bait/cooler status, toasts, cast/fight meters, co-op feed,
    /// and the Store / Journal / Ferry / Pause panels.</summary>
    public class Hud : MonoBehaviour
    {
        public enum Panel { None, Store, Journal, Travel, Pause }

        public PlayerController Player;
        public event Action LeaveRequested;

        Text _money, _status, _players, _prompt, _message, _fightLabel;
        RectTransform _feed, _chargeBar, _chargeFill, _fightBox, _tensionFill;
        Image _tensionImage, _fade;
        GameObject _panelRoot;
        RectTransform _panelList;
        Text _panelTitle;
        float _messageTimer;
        int _openedFrame = -1;
        readonly List<(Text text, float time)> _feedLines = new List<(Text, float)>();
        public Panel Open { get; private set; } = Panel.None;

        public static Hud Create(PlayerController player)
        {
            var canvas = Ui.MakeCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.Player = player;
            player.Hud = hud;
            hud.Build(canvas.transform);
            return hud;
        }

        void Build(Transform root)
        {
            _money = Ui.Label(root, "", 34, new Color(1f, 0.92f, 0.4f));
            Ui.Place(_money.rectTransform, new Vector4(0, 1, 0, 1), new Vector2(24, -64), new Vector2(424, -14));

            _players = Ui.Label(root, "", 17, new Color(0.75f, 0.95f, 1f));
            Ui.Place(_players.rectTransform, new Vector4(0, 1, 0, 1), new Vector2(26, -96), new Vector2(700, -66));

            _feed = Ui.Rect(root, "Feed");
            Ui.Place(_feed, new Vector4(0, 1, 0, 1), new Vector2(26, -300), new Vector2(760, -100));
            var fv = Ui.VList(_feed, 2, 0);
            fv.childAlignment = TextAnchor.UpperLeft;

            _status = Ui.Label(root, "", 20, new Color(0.95f, 0.95f, 0.95f), TextAnchor.UpperRight);
            Ui.Place(_status.rectTransform, new Vector4(1, 1, 1, 1), new Vector2(-540, -150), new Vector2(-20, -16));

            var cross = Ui.Label(root, "+", 22, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter);
            Ui.Place(cross.rectTransform, new Vector4(0.5f, 0.5f, 0.5f, 0.5f), new Vector2(-10, -18), new Vector2(10, 18));

            _message = Ui.Label(root, "", 26, Color.white, TextAnchor.UpperCenter);
            Ui.Place(_message.rectTransform, new Vector4(0.5f, 1, 0.5f, 1), new Vector2(-600, -230), new Vector2(600, -150));

            _prompt = Ui.Label(root, "", 24, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(_prompt.rectTransform, new Vector4(0.5f, 0, 0.5f, 0), new Vector2(-400, 110), new Vector2(400, 150));

            (_chargeBar, _chargeFill, _) = Bar(root, new Color(0.4f, 0.8f, 1f));
            Ui.Place(_chargeBar, new Vector4(0.5f, 0, 0.5f, 0), new Vector2(-160, 182), new Vector2(160, 200));

            _fightBox = Ui.Rect(root, "Fight");
            Ui.Place(_fightBox, new Vector4(0.5f, 0, 0.5f, 0), new Vector2(-220, 190), new Vector2(220, 270));
            _fightLabel = Ui.Label(_fightBox, "", 22, Color.white, TextAnchor.UpperCenter);
            Ui.Place(_fightLabel.rectTransform, new Vector4(0, 1, 1, 1), new Vector2(0, -34), new Vector2(0, 0));
            RectTransform tensionBar;
            (tensionBar, _tensionFill, _tensionImage) = Bar(_fightBox, new Color(0.4f, 1f, 0.4f));
            Ui.Place(tensionBar, new Vector4(0, 1, 1, 1), new Vector2(0, -68), new Vector2(0, -42));

            var help = Ui.Label(root, "WASD move  ·  Shift sprint  ·  Space jump\nHold LMB charge, release cast  ·  Hold LMB reel\nE interact  ·  1-4 bait  ·  Tab journal  ·  Esc menu", 16, new Color(1, 1, 1, 0.75f), TextAnchor.LowerLeft);
            Ui.Place(help.rectTransform, new Vector4(0, 0, 0, 0), new Vector2(20, 20), new Vector2(720, 110));

            // one shared panel whose contents are rebuilt per view
            var panel = Ui.Panel(root, new Color(0.06f, 0.1f, 0.14f, 0.94f));
            _panelRoot = panel.gameObject;
            Ui.Place(panel.rectTransform, new Vector4(0.5f, 0.5f, 0.5f, 0.5f), new Vector2(-340, -330), new Vector2(340, 330));
            Ui.VList(panel.rectTransform, 10, 22).childAlignment = TextAnchor.UpperLeft;
            _panelTitle = Ui.Label(panel.transform, "", 30, new Color(1f, 0.92f, 0.5f));
            _panelTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            _panelList = Ui.Rect(panel.transform, "List");
            Ui.VList(_panelList, 8, 0);
            _panelList.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            Ui.Button(panel.transform, "Close  [Esc]", ClosePanel);
            _panelRoot.SetActive(false);

            _fade = Ui.Panel(root, new Color(0, 0, 0, 0));
            _fade.raycastTarget = false;
            Ui.Place(_fade.rectTransform, new Vector4(0, 0, 1, 1), Vector2.zero, Vector2.zero);

            GameState.Changed += Refresh;
            GameState.Message += ShowMessage;
            Refresh();
        }

        void OnDestroy()
        {
            GameState.Changed -= Refresh;
            GameState.Message -= ShowMessage;
        }

        static (RectTransform bar, RectTransform fill, Image fillImage) Bar(Transform parent, Color color)
        {
            var bg = Ui.Panel(parent, new Color(0, 0, 0, 0.55f));
            bg.raycastTarget = false;
            var fill = Ui.Panel(bg.transform, color);
            fill.raycastTarget = false;
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(0, 1);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            return (bg.rectTransform, fr, fill);
        }

        static void SetFill(RectTransform fill, float v) => fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1);

        void Update()
        {
            if (Player == null) return;
            var f = Player.Fishing;
            _chargeBar.gameObject.SetActive(f.State == Fishing.FishState.Charging);
            SetFill(_chargeFill, f.Charge);
            bool fighting = f.State == Fishing.FishState.Fight;
            _fightBox.gameObject.SetActive(fighting);
            if (fighting)
            {
                SetFill(_tensionFill, f.Tension);
                bool danger = f.Tension > Data.CleanTensionLimit;
                _tensionImage.color = danger ? new Color(1f, 0.25f, 0.2f)
                    : Color.Lerp(new Color(0.4f, 1f, 0.4f), new Color(1f, 0.85f, 0.2f), f.Tension / Data.CleanTensionLimit);
                _fightLabel.text = $"TENSION  ·  {Mathf.Max(f.Distance - 1.8f, 0):F1} m to go  ·  escapes in {Mathf.Max(f.MaxDistance - f.Distance, 0):F1} m";
            }

            if (_messageTimer > 0)
            {
                _messageTimer -= Time.deltaTime;
                var c = _message.color;
                c.a = Open != Panel.None ? 0f : Mathf.Clamp01(_messageTimer);
                _message.color = c;
            }
            for (int i = _feedLines.Count - 1; i >= 0; i--)
            {
                var (text, time) = _feedLines[i];
                float age = Time.time - time;
                if (age > 7f) { Destroy(text.gameObject); _feedLines.RemoveAt(i); continue; }
                var c = text.color;
                c.a = Mathf.Clamp01(7f - age);
                text.color = c;
            }

            if (Open != Panel.None && !Player.Automation && Time.frameCount != _openedFrame)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || (Input.GetKeyDown(KeyCode.Tab) && Open == Panel.Journal)
                    || (Input.GetKeyDown(KeyCode.E) && (Open == Panel.Store || Open == Panel.Travel)))
                    ClosePanel();
            }
        }

        public void SetPrompt(string text) => _prompt.text = text;

        public void ShowMessage(string text, Color color)
        {
            _message.text = text;
            _message.color = color;
            _messageTimer = 4f;
        }

        /// <summary>A line in the co-op feed (top-left) that fades after a few seconds.</summary>
        public void Feed(string text, Color? color = null)
        {
            var t = Ui.Label(_feed, text, 18, color ?? new Color(0.85f, 0.95f, 1f));
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            _feedLines.Add((t, Time.time));
            if (_feedLines.Count > 6)
            {
                Destroy(_feedLines[0].text.gameObject);
                _feedLines.RemoveAt(0);
            }
        }

        public void RefreshPlayers()
        {
            if (!NetSession.IsOnline) { _players.text = ""; return; }
            var names = new List<string> { "You" + (NetSession.IsHost ? " (host)" : "") };
            foreach (var p in NetPlayer.All)
                if (!p.IsOwner) names.Add(p.DisplayName + (p.IsHostPlayer ? " (host)" : ""));
            _players.text = "Co-op: " + string.Join("  ·  ", names);
        }

        void Refresh()
        {
            _money.text = "$" + GameState.Money;
            var b = GameState.Bait;
            string count = b.Infinite ? "∞" : GameState.BaitCounts[GameState.CurrentBait].ToString();
            _status.text = $"{GameState.Island.Name}\nBait: {b.Name} ({count})  [1-4]\n{Data.Coolers[GameState.CoolerLevel].Name}: {GameState.Cooler.Count} / {GameState.CoolerSlots}\n{GameState.Rod.Name}";
            if (Open != Panel.None) Rebuild();
        }

        // ------------------------------------------------------------------ panels

        public void OpenPanel(Panel p)
        {
            Open = p;
            _openedFrame = Time.frameCount;  // the key that opened it must not also close it
            _panelRoot.SetActive(true);
            Player.UiOpen = true;
            Player.ReleaseMouse();
            Rebuild();
        }

        public void TogglePanel(Panel p)
        {
            if (Open == p) ClosePanel();
            else if (Open == Panel.None) OpenPanel(p);
        }

        public void ClosePanel()
        {
            if (Open == Panel.None) return;
            Open = Panel.None;
            _panelRoot.SetActive(false);
            Player.UiOpen = false;
            Player.CaptureMouse();
        }

        void Rebuild()
        {
            foreach (Transform c in _panelList) Destroy(c.gameObject);
            switch (Open)
            {
                case Panel.Store: _panelTitle.text = "General Store"; BuildStore(); break;
                case Panel.Journal: _panelTitle.text = "Field Journal"; BuildJournal(); break;
                case Panel.Travel: _panelTitle.text = "Ferry"; BuildTravel(); break;
                case Panel.Pause: _panelTitle.text = "Paused"; BuildPause(); break;
            }
        }

        Text Line(string text, int size = 18, Color? color = null)
        {
            var t = Ui.Label(_panelList, text, size, color ?? new Color(0.8f, 0.9f, 1f));
            t.gameObject.AddComponent<LayoutElement>().minHeight = size + 8;
            return t;
        }

        void Button(string text, bool enabled, Action action)
        {
            var b = Ui.Button(_panelList, text, () =>
            {
                Sfx.Play("blip");
                action?.Invoke();
            }, 19);
            b.interactable = enabled;
        }

        static void Buy(bool ok)
        {
            if (!ok) Sfx.Play("deny");
        }

        void BuildStore()
        {
            var isl = GameState.Island;
            Line($"Wallet: ${GameState.Money}   ·   {isl.Name} store");
            foreach (int i in isl.Baits)
            {
                var b = Data.Baits[i];
                if (b.Infinite) continue;
                int idx = i;
                Button($"Bait: {b.Pack}× {b.Name}  (${b.Cost})  you have {GameState.BaitCounts[i]}", GameState.Money >= b.Cost, () => Buy(GameState.BuyBait(idx)));
            }
            int nextRod = GameState.RodLevel + 1;
            if (nextRod >= Data.Rods.Length) Button($"Rod: {GameState.Rod.Name} (best rod)", false, null);
            else if (nextRod > isl.MaxRod) Button($"Rod: {Data.Rods[nextRod].Name} is sold on another island", false, null);
            else
            {
                var r = Data.Rods[nextRod];
                Button($"Rod: {r.Name}  (${r.Cost})  faster reel, tougher line", GameState.Money >= r.Cost, () => Buy(GameState.BuyRod()));
            }
            int nextCooler = GameState.CoolerLevel + 1;
            if (nextCooler >= Data.Coolers.Length) Button($"Storage: {Data.Coolers[GameState.CoolerLevel].Name} (biggest)", false, null);
            else if (nextCooler > isl.MaxCooler) Button($"Storage: {Data.Coolers[nextCooler].Name} is sold on another island", false, null);
            else
            {
                var c = Data.Coolers[nextCooler];
                Button($"Storage: {c.Name}  (${c.Cost})  holds {c.Slots}", GameState.Money >= c.Cost, () => Buy(GameState.BuyCooler()));
            }
        }

        void BuildJournal()
        {
            Line($"Discovered {GameState.SpeciesCount} / {Data.CreatureList.Count}   ·   Lifetime earnings ${GameState.LifetimeEarnings}");
            var lines = new List<string>();
            foreach (var c in Data.CreatureList)
            {
                if (GameState.Journal.TryGetValue(c.Id, out var e))
                {
                    string hex = ColorUtility.ToHtmlStringRGB(Data.RarityColors[c.Rarity]);
                    lines.Add($"<color=#{hex}>{c.Name}</color>  ×{e.count}   best {Mathf.RoundToInt(c.Length * e.best * 100f)} cm   base ${c.Worth}");
                }
                else lines.Add($"<color=#666666>???  ({Data.RarityNames[c.Rarity]})</color>");
            }
            Line(string.Join("\n", lines), 16, new Color(0.92f, 0.92f, 0.92f)).GetComponent<LayoutElement>().minHeight = 24 * lines.Count;
        }

        void BuildTravel()
        {
            Line($"Wallet: ${GameState.Money}   ·   Species discovered: {GameState.SpeciesCount}");
            for (int i = 0; i < Data.Islands.Length; i++)
            {
                var isl = Data.Islands[i];
                int idx = i;
                if (i == GameState.CurrentIsland) Button($"{isl.Name}  (you are here)", false, null);
                else if (GameState.UnlockedIslands.Contains(i)) Button($"Sail to {isl.Name}", true, () => TravelTo(idx));
                else
                {
                    string reason = GameState.UnlockBlockReason(i);
                    if (reason == "")
                        Button($"Buy ticket to {isl.Name}  (${isl.Cost})", true, () =>
                        {
                            if (GameState.UnlockIsland(idx)) { Sfx.Play("cash"); TravelTo(idx); }
                        });
                    else Button($"{isl.Name}  (locked: {reason})", false, null);
                }
            }
        }

        void BuildPause()
        {
            if (NetSession.IsHost)
            {
                var addrs = NetSession.LocalAddresses();
                Line($"Hosting on port {NetSession.Port}.\nFriends on your network join with: {(addrs.Count > 0 ? string.Join(", ", addrs) : "your computer's IP")}\nOver the internet: forward UDP {NetSession.Port} on your router, or use a virtual LAN like Tailscale.", 17);
            }
            Button("Resume", true, ClosePanel);
            Button(NetSession.IsOnline ? "Leave to main menu" : "Main menu", true, () => LeaveRequested?.Invoke());
            Button("Quit game", true, () =>
            {
                NetSession.Leave();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        // ------------------------------------------------------------------ travel

        public void TravelTo(int island)
        {
            ClosePanel();
            Player.Fishing.ResetLine();
            Sfx.Play("horn");
            StartCoroutine(TravelRoutine(island));
        }

        System.Collections.IEnumerator TravelRoutine(int island)
        {
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { _fade.color = new Color(0, 0, 0, t / 0.6f); yield return null; }
            _fade.color = Color.black;
            GameState.Travel(island);
            Player.Respawn();
            NetSession.SendEvent("travel", island, 0f, false, "");
            yield return new WaitForSecondsRealtime(0.4f);
            for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime) { _fade.color = new Color(0, 0, 0, 1f - t / 0.8f); yield return null; }
            _fade.color = new Color(0, 0, 0, 0);
            ShowMessage($"Welcome to {Data.Islands[island].Name}!", new Color(0.8f, 0.95f, 1f));
        }
    }
}
