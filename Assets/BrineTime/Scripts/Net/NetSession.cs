using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Co-op session on top of FishNet (Tugboat UDP transport). Every player owns their own money/gear
    /// (saved locally); the network shares presence (a NetPlayer per client, moved with FishNet's
    /// interpolated NetworkTransform) and events (catches, sales, travel). Lives on the NetworkManager.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetSession : MonoBehaviour
    {
        public const ushort Port = 24680;
        public const int MaxPlayers = 8;

        public static NetSession Instance { get; private set; }

        /// <summary>Presence prefab spawned for every connected client (assigned by the editor setup).</summary>
        public NetworkObject PlayerPrefab;

        public static event Action Connected;
        public static event Action<string> ConnectionFailed;
        public static event Action Disconnected;
        public static event Action<NetPlayer> PlayerJoined;
        public static event Action<NetPlayer> PlayerLeft;
        /// <summary>(sender, kind, intValue, floatValue, flag, text)</summary>
        public static event Action<NetPlayer, string, int, float, bool, string> EventReceived;

        public static string LocalName = "Angler";
        public static Color LocalColor = new Color(0.9f, 0.4f, 0.3f);

        NetworkManager _nm;
        Tugboat _transport;
        bool _clientWasStarted;

        public static bool IsOnline => Instance != null && (Instance._nm.IsServerStarted || Instance._nm.IsClientStarted);
        public static bool IsHost => Instance != null && Instance._nm.IsServerStarted;
        public static NetworkManager Manager => Instance != null ? Instance._nm : null;

        void Awake()
        {
            Instance = this;
            _nm = GetComponent<NetworkManager>();
            _transport = GetComponent<Tugboat>();
            _nm.ClientManager.OnClientConnectionState += OnClientState;
            _nm.ServerManager.OnServerConnectionState += OnServerState;
            _nm.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        void OnDestroy()
        {
            if (_nm == null) return;
            _nm.ClientManager.OnClientConnectionState -= OnClientState;
            _nm.ServerManager.OnServerConnectionState -= OnServerState;
            _nm.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        // ------------------------------------------------------------------ host / join / leave

        /// <summary>Starts a server plus a local client. Returns an error message, or "" on success.</summary>
        public static string Host()
        {
            if (Instance == null) return "Networking isn't available in this scene.";
            var s = Instance;
            s._transport.SetPort(Port);
            s._transport.SetMaximumClients(MaxPlayers);
            if (!s._nm.ServerManager.StartConnection(Port))
                return $"Couldn't host on port {Port}. Is another copy already hosting?";
            s._nm.ClientManager.StartConnection("localhost", Port);
            return "";
        }

        public static string Join(string address)
        {
            if (Instance == null) return "Networking isn't available in this scene.";
            if (string.IsNullOrWhiteSpace(address)) return "Type your friend's IP address first.";
            Instance._transport.SetClientAddress(address.Trim());
            if (!Instance._nm.ClientManager.StartConnection(address.Trim(), Port))
                return "Couldn't start connecting.";
            return "";
        }

        public static void Leave()
        {
            if (Instance == null) return;
            if (Instance._nm.IsClientStarted) Instance._nm.ClientManager.StopConnection();
            if (Instance._nm.IsServerStarted) Instance._nm.ServerManager.StopConnection(true);
        }

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !_nm.IsClientStarted && _clientWasStarted == false)
                ConnectionFailed?.Invoke($"The server stopped (port {Port} may be in use).");
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _clientWasStarted = true;
                    Connected?.Invoke();
                    break;
                case LocalConnectionState.Stopped:
                    if (_clientWasStarted) Disconnected?.Invoke();
                    else ConnectionFailed?.Invoke("Couldn't reach the host.");
                    _clientWasStarted = false;
                    break;
            }
        }

        // the server spawns one presence object per client, owned by that client
        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer || PlayerPrefab == null) return;
            var nob = Instantiate(PlayerPrefab);
            _nm.ServerManager.Spawn(nob, conn);
        }

        // ------------------------------------------------------------------ helpers used by game code

        /// <summary>Broadcast a gameplay event from the local player (no-op when offline).</summary>
        public static void SendEvent(string kind, int i, float f, bool b, string s)
        {
            if (!IsOnline || NetPlayer.Local == null) return;
            NetPlayer.Local.SendGameEvent(kind, i, f, b, s);
        }

        internal static void RaiseJoined(NetPlayer p) => PlayerJoined?.Invoke(p);
        internal static void RaiseLeft(NetPlayer p) => PlayerLeft?.Invoke(p);
        internal static void RaiseEvent(NetPlayer p, string kind, int i, float f, bool b, string s) => EventReceived?.Invoke(p, kind, i, f, b, s);

        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (a.AddressFamily != AddressFamily.InterNetwork) continue;
                    var s = a.ToString();
                    if (!s.StartsWith("127.") && !s.StartsWith("169.254.")) list.Add(s);
                }
            }
            catch (Exception) { /* no network adapters */ }
            return list;
        }
    }
}
