using System;
using System.Collections.Generic;

namespace BusTycoon.Multiplayer
{
    public enum TransportEventType
    {
        /// <summary>Oda açıldı (kurucu): artık katılımcı bekleniyor.</summary>
        Hosted,
        /// <summary>Odaya bağlanıldı (katılımcı).</summary>
        Connected,
        /// <summary>Kurucuya yeni bir oyuncu bağlandı.</summary>
        PeerJoined,
        /// <summary>Kurucudan bir oyuncu koptu.</summary>
        PeerLeft,
        Message,
        /// <summary>Katılımcının kurucuyla bağlantısı koptu.</summary>
        Closed,
        Error
    }

    public struct TransportEvent
    {
        public TransportEventType Type;
        /// <summary>Kurucuda gönderen oyuncunun kimliği; katılımcıda "host".</summary>
        public string Peer;
        /// <summary>Mesaj ya da hata türü (ör. "peer-unavailable").</summary>
        public string Data;
    }

    /// <summary>
    /// Oyuncular arası bağlantı: kurucu (host) yıldızın merkezidir, katılımcılar yalnız ona bağlanır. Web'de PeerJS
    /// (WebRTC) ile tarayıcıdan tarayıcıya; testlerde <see cref="LoopbackTransport"/> ile bellekte. Olaylar kuyruğa
    /// alınır ve oyun döngüsünde <see cref="Poll"/> ile işlenir (ağ geri çağrıları oyun durumuna doğrudan dokunmaz).
    /// </summary>
    public interface IRoomTransport
    {
        void Host(string code);
        void Join(string code);
        /// <summary>Kurucu: belirli oyuncuya; katılımcı: kurucuya (peer yok sayılır).</summary>
        void Send(string peer, string message);
        /// <summary>Kurucu: bütün oyunculara.</summary>
        void Broadcast(string message);
        void Close();
        void Poll(Action<TransportEvent> handler);
    }

    /// <summary>Bellek içi bağlantı: aynı süreçteki odalar birbirine bağlanır (testler, çevrimdışı deneme).</summary>
    public sealed class LoopbackTransport : IRoomTransport
    {
        /// <summary>Aynı merkezdeki odalar birbirini görür.</summary>
        public sealed class Hub
        {
            internal readonly Dictionary<string, LoopbackTransport> Hosts = new Dictionary<string, LoopbackTransport>();
            int serial;
            internal string NextId() => "peer" + (++serial);
        }

        public static readonly Hub Shared = new Hub();

        readonly Hub hub;
        readonly Queue<TransportEvent> queue = new Queue<TransportEvent>();
        readonly Dictionary<string, LoopbackTransport> clients = new Dictionary<string, LoopbackTransport>();
        LoopbackTransport host;
        string id, code;
        bool isHost, closed;

        public LoopbackTransport(Hub hub = null) => this.hub = hub ?? Shared;

        public void Host(string roomCode)
        {
            closed = false;
            isHost = true;
            code = roomCode;
            if (hub.Hosts.ContainsKey(roomCode))
            {
                Enqueue(TransportEventType.Error, null, "unavailable-id");
                return;
            }
            hub.Hosts[roomCode] = this;
            Enqueue(TransportEventType.Hosted, null, roomCode);
        }

        public void Join(string roomCode)
        {
            closed = false;
            id = hub.NextId();
            if (!hub.Hosts.TryGetValue(roomCode, out LoopbackTransport target) || target.closed)
            {
                Enqueue(TransportEventType.Error, null, "peer-unavailable");
                return;
            }
            host = target;
            target.clients[id] = this;
            Enqueue(TransportEventType.Connected, "host", null);
            target.Enqueue(TransportEventType.PeerJoined, id, null);
        }

        public void Send(string peer, string message)
        {
            if (closed) return;
            if (isHost)
            {
                if (peer != null && clients.TryGetValue(peer, out LoopbackTransport client)) client.Enqueue(TransportEventType.Message, "host", message);
            }
            else host?.Enqueue(TransportEventType.Message, id, message);
        }

        public void Broadcast(string message)
        {
            if (closed || !isHost) return;
            foreach (var client in clients.Values) client.Enqueue(TransportEventType.Message, "host", message);
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            if (isHost)
            {
                if (code != null && hub.Hosts.TryGetValue(code, out LoopbackTransport registered) && registered == this) hub.Hosts.Remove(code);
                foreach (var client in clients.Values) client.Enqueue(TransportEventType.Closed, "host", null);
                clients.Clear();
            }
            else if (host != null)
            {
                host.clients.Remove(id);
                host.Enqueue(TransportEventType.PeerLeft, id, null);
            }
        }

        public void Poll(Action<TransportEvent> handler)
        {
            while (queue.Count > 0) handler(queue.Dequeue());
        }

        void Enqueue(TransportEventType type, string peer, string data)
        {
            if (closed && type != TransportEventType.Error) return;
            queue.Enqueue(new TransportEvent { Type = type, Peer = peer, Data = data });
        }
    }
}
