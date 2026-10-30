using System;
using BusTycoon.Multiplayer;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    /// <summary>
    /// Tarayıcıda oda bağlantısı: Assets/Plugins/WebGL/BusTycoonNet.jslib (PeerJS / WebRTC). Olaylar JavaScript'te
    /// kuyruğa alınır, <see cref="Poll"/> her karede okur. Web dışında çok oyunculu desteklenmez ("unsupported").
    /// </summary>
    public sealed class WebRoomTransport : IRoomTransport
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BusTycoon_NetStart(int asHost, string code);
        [DllImport("__Internal")] static extern void BusTycoon_NetSend(string peer, string data);
        [DllImport("__Internal")] static extern void BusTycoon_NetBroadcast(string data);
        [DllImport("__Internal")] static extern void BusTycoon_NetStop();
        [DllImport("__Internal")] static extern string BusTycoon_NetPoll();
#endif

        [Serializable]
        sealed class NetEvent
        {
            public string t, p, d;
        }

        bool unsupported;

        public static bool IsSupported =>
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public void Host(string code) => Start(true, code);

        public void Join(string code) => Start(false, code);

        void Start(bool asHost, string code)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BusTycoon_NetStart(asHost ? 1 : 0, code);
#else
            unsupported = true;
#endif
        }

        public void Send(string peer, string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BusTycoon_NetSend(peer ?? string.Empty, message);
#endif
        }

        public void Broadcast(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BusTycoon_NetBroadcast(message);
#endif
        }

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BusTycoon_NetStop();
#endif
        }

        public void Poll(Action<TransportEvent> handler)
        {
            if (unsupported)
            {
                unsupported = false;
                handler(new TransportEvent { Type = TransportEventType.Error, Data = "unsupported" });
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            for (int i = 0; i < 64; i++)
            {
                string json = BusTycoon_NetPoll();
                if (string.IsNullOrEmpty(json)) break;
                NetEvent e = JsonUtility.FromJson<NetEvent>(json);
                if (e == null) continue;
                handler(new TransportEvent { Type = Parse(e.t), Peer = e.p, Data = e.d });
            }
#endif
        }

        static TransportEventType Parse(string type)
        {
            switch (type)
            {
                case "hosted": return TransportEventType.Hosted;
                case "connected": return TransportEventType.Connected;
                case "joined": return TransportEventType.PeerJoined;
                case "left": return TransportEventType.PeerLeft;
                case "message": return TransportEventType.Message;
                case "closed": return TransportEventType.Closed;
                default: return TransportEventType.Error;
            }
        }
    }
}
