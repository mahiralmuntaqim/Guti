using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// One per connected player. The owner copies their local PlayerController's pose onto this object
    /// every frame; FishNet's NetworkTransform (client-authoritative, 2-tick interpolation) sends it and
    /// smooths it on everyone else's screen. Rod/bobber state goes over an unreliable RPC at 20 Hz,
    /// gameplay events over reliable RPCs. Remote copies render an AvatarView.
    /// </summary>
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public static NetPlayer Local { get; private set; }

        readonly SyncVar<string> _name = new SyncVar<string>("Angler");
        readonly SyncVar<Color> _color = new SyncVar<Color>(Color.white);

        public string DisplayName => _name.Value;
        public Color BodyColor => _color.Value;
        public bool IsHostPlayer => Owner.IsValid && Owner.IsHost;
        public AvatarView Avatar { get; private set; }
        /// <summary>Island the player is on (from the last state packet).</summary>
        public int Island { get; private set; }
        public bool HasState { get; private set; }

        const float StateInterval = 0.05f;
        float _stateTimer;
        PlayerController _local;

        public override void OnStartClient()
        {
            base.OnStartClient();
            All.Add(this);
            if (IsOwner)
            {
                Local = this;
                _local = FindAnyObjectByType<PlayerController>();
                SetIdentity(NetSession.LocalName, NetSession.LocalColor);
            }
            else
            {
                Avatar = AvatarView.Create(this);
            }
            _name.OnChange += (prev, next, asServer) => { if (Avatar != null) Avatar.SetIdentity(next, _color.Value); NetSession.RaiseJoined(this); };
            _color.OnChange += (prev, next, asServer) => { if (Avatar != null) Avatar.SetIdentity(_name.Value, next); };
            if (!IsOwner) NetSession.RaiseJoined(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            All.Remove(this);
            if (Local == this) Local = null;
            if (Avatar != null) Destroy(Avatar.gameObject);
            if (!IsOwner) NetSession.RaiseLeft(this);
        }

        [ServerRpc]
        void SetIdentity(string displayName, Color color)
        {
            displayName = (displayName ?? "").Trim();
            if (displayName.Length > 16) displayName = displayName.Substring(0, 16);
            _name.Value = displayName.Length > 0 ? displayName : "Player " + OwnerId;
            _color.Value = color;
        }

        void Update()
        {
            if (!IsOwner || _local == null) return;
            // copy the local player's pose; NetworkTransform replicates and interpolates it
            transform.SetPositionAndRotation(_local.transform.position, _local.transform.rotation);
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0) return;
            _stateTimer = StateInterval;
            _local.Fishing.NetState(out bool hasBobber, out Vector3 bobber, out float tension);
            SendState(GameState.CurrentIsland, _local.Pitch, hasBobber, bobber, tension);
        }

        [ServerRpc(RequireOwnership = true)]
        void SendState(int island, float pitch, bool hasBobber, Vector3 bobber, float tension, Channel channel = Channel.Unreliable)
        {
            RelayState(island, pitch, hasBobber, bobber, tension);
        }

        [ObserversRpc(ExcludeOwner = true)]
        void RelayState(int island, float pitch, bool hasBobber, Vector3 bobber, float tension, Channel channel = Channel.Unreliable)
        {
            Island = island;
            HasState = true;
            if (Avatar != null) Avatar.ApplyState(pitch, hasBobber, bobber, tension);
        }

        public void SendGameEvent(string kind, int i, float f, bool b, string s) => EventToServer(kind, i, f, b, s ?? "");

        [ServerRpc(RequireOwnership = true)]
        void EventToServer(string kind, int i, float f, bool b, string s) => EventToObservers(kind, i, f, b, s);

        [ObserversRpc(ExcludeOwner = true)]
        void EventToObservers(string kind, int i, float f, bool b, string s) => NetSession.RaiseEvent(this, kind, i, f, b, s);
    }
}
