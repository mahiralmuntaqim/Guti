using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Entry point (the only object in Main.unity besides the NetworkManager): builds the world, ocean,
    /// player, HUD and title menu, and turns co-op events into feed messages and effects.
    /// Command line for built players: -autoplay (skip menu), -smoke-host / -smoke-join=ADDRESS (network test).
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        public static Bootstrap Instance { get; private set; }

        public World World { get; private set; }
        public WaterSurface Water { get; private set; }
        public PlayerController Player { get; private set; }
        public Hud Hud { get; private set; }
        public bool InGame { get; private set; }

        MainMenu _menu;

        /// <summary>Set by tests before the scene loads: skip the menu, don't touch the real save.</summary>
        public static bool TestMode;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            if (TestMode) { GameState.Persist = false; GameState.Reset(); }
            else GameState.Load();
            Sfx.Ensure();

            World = new GameObject("World").AddComponent<World>();
            World.Build();
            Water = new GameObject("Ocean").AddComponent<WaterSurface>();
            Player = PlayerController.Create();
            Player.Respawn();
            Hud = Hud.Create(Player);
            Hud.LeaveRequested += LeaveToMenu;
            Water.Follow = Player.transform;

            NetSession.PlayerJoined += OnPlayerJoined;
            NetSession.PlayerLeft += OnPlayerLeft;
            NetSession.EventReceived += OnNetEvent;
            NetSession.Disconnected += OnDisconnected;
        }

        void OnDestroy()
        {
            NetSession.PlayerJoined -= OnPlayerJoined;
            NetSession.PlayerLeft -= OnPlayerLeft;
            NetSession.EventReceived -= OnNetEvent;
            NetSession.Disconnected -= OnDisconnected;
        }

        void Start()
        {
            var args = new List<string>(Environment.GetCommandLineArgs());
            string join = args.Find(a => a.StartsWith("-smoke-join="));
            if (args.Contains("-smoke-host") || join != null)
            {
                StartCoroutine(NetSmokeTest(join?.Substring("-smoke-join=".Length)));
                return;
            }
            if (TestMode || args.Contains("-autoplay"))
            {
                StartGame();
                return;
            }
            ShowMenu();
        }

        // ------------------------------------------------------------------ menu / session flow

        void ShowMenu()
        {
            Player.UiOpen = true;
            _menu = MainMenu.Create();
            _menu.SoloPressed += () => { _menu.Remember(); StartGame(); };
            _menu.HostPressed += MenuHost;
            _menu.JoinPressed += MenuJoin;
        }

        void ApplyIdentity()
        {
            NetSession.LocalName = _menu != null ? _menu.PlayerName : "Angler";
            NetSession.LocalColor = NameColor(NetSession.LocalName);
        }

        void MenuHost()
        {
            _menu.Remember();
            ApplyIdentity();
            string err = NetSession.Host();
            if (err != "") { _menu.SetStatus(err); return; }
            StartGame();
            var addrs = NetSession.LocalAddresses();
            Hud.Feed($"Hosting! Friends join with: {(addrs.Count > 0 ? string.Join(", ", addrs) : "your IP")}  (port {NetSession.Port})", new Color(0.6f, 1f, 0.7f));
            Hud.Feed("Press Esc any time to see the join address again.");
        }

        void MenuJoin(string address)
        {
            _menu.Remember();
            ApplyIdentity();
            string err = NetSession.Join(address);
            if (err != "") { _menu.SetStatus(err); return; }
            _menu.SetStatus($"Connecting to {address}:{NetSession.Port} ...");
            _menu.SetBusy(true);
            StartCoroutine(AwaitConnection(address));
        }

        IEnumerator AwaitConnection(string address)
        {
            bool done = false, ok = false;
            string reason = "timed out.";
            Action onOk = () => { done = true; ok = true; };
            Action<string> onFail = r => { done = true; reason = r; };
            NetSession.Connected += onOk;
            NetSession.ConnectionFailed += onFail;
            float t = 0;
            while (!done && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            NetSession.Connected -= onOk;
            NetSession.ConnectionFailed -= onFail;
            if (ok) { StartGame(); yield break; }
            NetSession.Leave();
            if (_menu == null) yield break;
            _menu.SetBusy(false);
            _menu.SetStatus($"Couldn't connect to {address}: {reason} Check the IP, that the host pressed \"Host Co-op\", and that their firewall allows the game.");
        }

        public void StartGame()
        {
            if (_menu != null) Destroy(_menu.gameObject);
            InGame = true;
            Player.UiOpen = false;
            Player.Respawn();
            Player.CaptureMouse();
            Hud.RefreshPlayers();
            Hud.ShowMessage($"Welcome to {GameState.Island.Name}. Fish off the dock, sell at the Market, gear up at the Store, sail at the Ferry.", new Color(0.8f, 0.95f, 1f));
        }

        void LeaveToMenu()
        {
            NetSession.Leave();
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        void OnDisconnected()
        {
            if (!InGame) return;
            Hud.Feed("Lost connection to the host. You're playing solo now.", new Color(1f, 0.6f, 0.4f));
            Hud.RefreshPlayers();
        }

        public static Color NameColor(string n)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in n) h = h * 31 + c;
                return Color.HSVToRGB(Mathf.Abs(h % 1000) / 1000f, 0.6f, 0.9f);
            }
        }

        // ------------------------------------------------------------------ co-op events

        void OnPlayerJoined(NetPlayer p)
        {
            if (InGame && !p.IsOwner)
            {
                Hud.Feed($"{p.DisplayName} joined the island.", new Color(0.6f, 1f, 0.7f));
                Sfx.Play("blip", 1.3f);
            }
            Hud.RefreshPlayers();
        }

        void OnPlayerLeft(NetPlayer p)
        {
            Hud.Feed($"{p.DisplayName} left.", new Color(1f, 0.8f, 0.6f));
            Hud.RefreshPlayers();
        }

        void OnNetEvent(NetPlayer p, string kind, int i, float f, bool b, string s)
        {
            string who = p.DisplayName;
            Vector3 pos = p.transform.position;
            switch (kind)
            {
                case "catch":
                    if (!Data.Creatures.TryGetValue(s, out var c)) return;
                    int cm = Mathf.RoundToInt(c.Length * f * 100f);
                    Hud.Feed($"{who} caught a {Data.CatchTitle(s, b)} ({cm} cm, ${i})", b ? new Color(1f, 0.85f, 0.2f) : Data.RarityColors[c.Rarity]);
                    if (p.Avatar != null) p.Avatar.ShowCatch(s, f, b);
                    Sfx.PlayAt("splash_big", pos, 1f, -4f);
                    if (b || c.Rarity >= 2) Sfx.Play("golden", 1f, -8f);
                    break;
                case "sold":
                    Hud.Feed($"{who} sold their catch for ${i}");
                    Sfx.PlayAt("cash", pos, 1f, -6f);
                    break;
                case "travel":
                    if (i >= 0 && i < Data.Islands.Length) Hud.Feed($"{who} sailed to {Data.Islands[i].Name}.");
                    break;
                case "splash":
                    Sfx.PlayAt("splash_big", pos);
                    Hud.Feed($"{who} fell in the water!", new Color(0.6f, 0.85f, 1f));
                    break;
            }
        }

        // ------------------------------------------------------------------ two-process network smoke test

        IEnumerator NetSmokeTest(string joinAddress)
        {
            TestMode = true;
            GameState.Persist = false;
            Player.Automation = true;
            bool host = joinAddress == null;
            NetSession.LocalName = host ? "HostTester" : "ClientTester";
            string err = host ? NetSession.Host() : NetSession.Join(joinAddress);
            if (err != "") { Debug.Log("[smoke] error " + err); Finish(false); yield break; }
            StartGame();
            bool sawEvent = false;
            NetSession.EventReceived += (pl, kind, i, f, b, s) => { if (kind == "catch") sawEvent = true; Debug.Log($"[smoke] got {kind} from {pl.DisplayName}"); };
            float t = 0, nextSend = 0;
            while (t < 30f)
            {
                t += Time.unscaledDeltaTime;
                NetPlayer remote = NetPlayer.All.Find(x => !x.IsOwner);
                bool remoteOk = remote != null && remote.HasState && remote.Avatar != null && remote.DisplayName != "Angler";
                if (remoteOk && t >= nextSend)
                {
                    nextSend = t + 1f;
                    NetSession.SendEvent("catch", 231, 1.1f, true, host ? "moon_carp" : "glimmer_eel");
                }
                if (remoteOk && sawEvent)
                {
                    Debug.Log($"[smoke] {(host ? "host" : "client")} sees {remote.DisplayName} at {remote.transform.position} island={remote.Island}");
                    for (float w = 0; w < 2f; w += Time.unscaledDeltaTime) yield return null;  // let the other side finish
                    Finish(true);
                    yield break;
                }
                yield return null;
            }
            Debug.Log($"[smoke] timeout: players={NetPlayer.All.Count} sawEvent={sawEvent}");
            Finish(false);
        }

        static void Finish(bool ok)
        {
            Debug.Log(ok ? "[smoke] PASS" : "[smoke] FAIL");
            NetSession.Leave();
            Application.Quit(ok ? 0 : 1);
        }
    }
}
