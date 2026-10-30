using System;
using System.Collections.Generic;

namespace BusTycoon.Multiplayer
{
    /// <summary>
    /// Çok oyunculu oda: kurucu (host) odayı kod ile açar, arkadaşları kodla katılır. Lobide herkes merkez şehrini seçer,
    /// kurucu mod (Blitz / Normal) ve hızı belirleyip başlatır. Her oyuncu kendi şirketini kendi cihazında yönetir; oyun
    /// günlerini kurucunun saati belirler (TICK), böylece herkes aynı günü oynar. Oyuncular her gün kasasını bildirir,
    /// kurucu sıralamayı herkese yayar; hedef kasaya ilk ulaşan (Blitz $1M, Normal $10M) kazanır.
    ///
    /// Mesajlar (RoomMessage): katılımcı → kurucu HELLO, CITY, STATS; kurucu → katılımcı WELCOME, REJECT, ROOM, START,
    /// TICK, END. Ağ olayları <see cref="Update"/> içinde işlenir.
    /// </summary>
    public sealed class RoomSession
    {
        const string HostId = "host";

        readonly IRoomTransport transport;
        readonly Random random;
        readonly List<RoomPlayer> players = new List<RoomPlayer>();
        string localName;
        int hostAttempts;
        bool roomDirty;
        float sinceBroadcast;
        /// <summary>Sıralama yayınları arasında en az bu kadar saniye (oyuncular her gün bildirir; ağ boğulmasın).</summary>
        const float StandingsInterval = 0.4f;

        public RoomPhase Phase { get; private set; }
        public bool IsHost { get; private set; }
        public string Code { get; private set; }
        public string LocalId { get; private set; }
        public MatchMode Mode { get; private set; } = MatchMode.Blitz;
        public int Speed { get; private set; } = MatchRules.DefaultSpeed(MatchMode.Blitz);
        /// <summary>Kurucunun saatindeki gün: katılımcılar bu güne kadar oynayabilir.</summary>
        public int HostDay { get; private set; }
        public RoomPlayer Winner { get; private set; }
        /// <summary>Son hata ya da kapanma nedeni (arayüzde gösterilir).</summary>
        public string Status { get; private set; }

        public IReadOnlyList<RoomPlayer> Players => players;
        public RoomPlayer Local => players.Find(p => p.Id == LocalId);
        public double TargetCash => MatchRules.TargetCash(Mode);

        /// <summary>Oyuncu listesi, mod, hız ya da aşama değişti.</summary>
        public event Action Changed;
        public event Action MatchStarted;
        /// <summary>Katılımcı: kurucunun saati ilerledi, bu güne kadar oynanabilir.</summary>
        public event Action<int> DayAllowed;
        public event Action<RoomPlayer> MatchEnded;
        /// <summary>Kısa bilgi ("Ayşe odaya katıldı").</summary>
        public event Action<string> Notice;
        /// <summary>Oda kapandı (kurucu ayrıldı, bağlantı kurulamadı, oda bulunamadı).</summary>
        public event Action Closed;

        public RoomSession(IRoomTransport transport, int seed = 0)
        {
            this.transport = transport;
            random = seed != 0 ? new Random(seed) : new Random();
        }

        // ------------------------------------------------------------------ oda kurma / katılma

        public void Create(string name, MatchMode mode)
        {
            if (Phase != RoomPhase.Idle) return;
            IsHost = true;
            localName = RoomMessage.CleanName(name);
            Mode = mode;
            Speed = MatchRules.DefaultSpeed(mode);
            LocalId = HostId;
            players.Clear();
            players.Add(new RoomPlayer { Id = HostId, Name = localName, IsHost = true });
            Phase = RoomPhase.Connecting;
            Code = MatchRules.NewCode(random);
            transport.Host(Code);
            Changed?.Invoke();
        }

        public void Join(string name, string code)
        {
            if (Phase != RoomPhase.Idle) return;
            IsHost = false;
            localName = RoomMessage.CleanName(name);
            Code = MatchRules.NormalizeCode(code);
            if (Code.Length != MatchRules.CodeLength)
            {
                Fail("Oda kodu " + MatchRules.CodeLength + " karakter olmalı.");
                return;
            }
            Phase = RoomPhase.Connecting;
            transport.Join(Code);
            Changed?.Invoke();
        }

        public void Leave()
        {
            if (Phase == RoomPhase.Idle) return;
            // Diğerleri bağlantının kopmasını beklemeden ayrıldığını öğrensin
            if (Phase == RoomPhase.Lobby || Phase == RoomPhase.Playing || Phase == RoomPhase.Ended)
            {
                if (IsHost) transport.Broadcast(RoomMessage.Pack("BYE"));
                else transport.Send(HostId, RoomMessage.Pack("BYE"));
            }
            transport.Close();
            Phase = RoomPhase.Idle;
            players.Clear();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ lobi

        public void SetCity(string cityId)
        {
            RoomPlayer local = Local;
            if (local == null || Phase != RoomPhase.Lobby) return;
            local.CityId = cityId;
            if (IsHost) BroadcastRoom();
            else transport.Send(HostId, RoomMessage.Pack("CITY", cityId ?? string.Empty));
            Changed?.Invoke();
        }

        public void SetMode(MatchMode mode)
        {
            if (!IsHost || Phase != RoomPhase.Lobby) return;
            Mode = mode;
            Speed = MatchRules.DefaultSpeed(mode);
            BroadcastRoom();
            Changed?.Invoke();
        }

        public void SetSpeed(int speed)
        {
            if (!IsHost || Phase != RoomPhase.Lobby) return;
            Speed = Math.Max(1, Math.Min(4, speed));
            BroadcastRoom();
            Changed?.Invoke();
        }

        /// <summary>Kurucu başlatabilir mi: en az iki bağlı oyuncu ve herkes şehrini seçmiş.</summary>
        public bool CanStart
        {
            get
            {
                if (!IsHost || Phase != RoomPhase.Lobby) return false;
                int connected = 0;
                foreach (var p in players)
                {
                    if (!p.Connected) continue;
                    if (string.IsNullOrEmpty(p.CityId)) return false;
                    connected++;
                }
                return connected >= 2;
            }
        }

        public void Start()
        {
            if (!CanStart) return;
            players.RemoveAll(p => !p.Connected);
            foreach (var p in players)
            {
                p.Cash = 0;
                p.Day = 0;
                p.Bankrupt = false;
            }
            Phase = RoomPhase.Playing;
            HostDay = 0;
            Winner = null;
            BroadcastRoom();
            transport.Broadcast(RoomMessage.Pack("START", Mode.ToString(), RoomMessage.Num(Speed)));
            MatchStarted?.Invoke();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ yarış

        /// <summary>Kurucu: kendi saatinde bir gün bitti; katılımcılar bu güne kadar oynayabilir.</summary>
        public void HostDayEnded(int day)
        {
            if (!IsHost || Phase != RoomPhase.Playing) return;
            HostDay = day;
            transport.Broadcast(RoomMessage.Pack("TICK", RoomMessage.Num(day)));
        }

        /// <summary>Yerel oyuncunun gün sonu durumu: kurucuda sıralamaya işlenir ve kazanan kontrol edilir.</summary>
        public void ReportStats(PlayerStats stats)
        {
            RoomPlayer local = Local;
            if (local == null || Phase != RoomPhase.Playing) return;
            Apply(local, stats);
            if (IsHost)
            {
                CheckWinner(local);
                roomDirty = true;
            }
            else
            {
                transport.Send(HostId, RoomMessage.Pack("STATS", RoomMessage.Num(stats.Day), RoomMessage.Num(stats.Cash),
                    RoomMessage.Num(stats.Routes), RoomMessage.Num(stats.Buses), stats.Bankrupt ? "1" : "0"));
            }
            Changed?.Invoke();
        }

        /// <summary>Sıralama: kasaya göre (iflas edenler sonda).</summary>
        public List<RoomPlayer> Standings()
        {
            var list = new List<RoomPlayer>(players);
            list.Sort((a, b) => a.Bankrupt != b.Bankrupt ? a.Bankrupt.CompareTo(b.Bankrupt) : b.Cash.CompareTo(a.Cash));
            return list;
        }

        // ------------------------------------------------------------------ ağ olayları

        /// <summary>Her karede: ağ olaylarını işler, kurucu sıralamayı aralıklı yayar.</summary>
        public void Update(float deltaSeconds)
        {
            transport.Poll(Handle);
            sinceBroadcast += deltaSeconds;
            if (IsHost && roomDirty && sinceBroadcast >= StandingsInterval) BroadcastRoom();
        }

        void Handle(TransportEvent e)
        {
            switch (e.Type)
            {
                case TransportEventType.Hosted:
                    Phase = RoomPhase.Lobby;
                    Changed?.Invoke();
                    break;

                case TransportEventType.Connected:
                    transport.Send(HostId, RoomMessage.Pack("HELLO", localName, MatchRules.ProtocolVersion));
                    break;

                case TransportEventType.PeerJoined:
                    break; // HELLO bekleniyor

                case TransportEventType.PeerLeft:
                    OnPeerLeft(e.Peer);
                    break;

                case TransportEventType.Closed:
                    if (!IsHost) HostGone(Phase == RoomPhase.Playing ? "Odayla bağlantı koptu; oyun tek başına sürüyor." : "Odayla bağlantı koptu.");
                    break;

                case TransportEventType.Error:
                    if (IsHost && !string.IsNullOrEmpty(e.Peer)) break; // tek oyuncunun bağlantı sorunu odayı kapatmaz
                    OnError(e.Data);
                    break;

                case TransportEventType.Message:
                    if (IsHost) HandleAsHost(e.Peer, RoomMessage.Unpack(e.Data));
                    else HandleAsClient(RoomMessage.Unpack(e.Data));
                    break;
            }
        }

        void HandleAsHost(string peer, string[] m)
        {
            RoomPlayer player = players.Find(p => p.Id == peer);
            switch (m[0])
            {
                case "HELLO":
                    if (m.Length < 3 || m[2] != MatchRules.ProtocolVersion) { Reject(peer, "Oyununuz eski sürüm: sayfayı yenileyin."); return; }
                    if (Phase != RoomPhase.Lobby) { Reject(peer, "Yarış başladı; bu odaya artık katılınamaz."); return; }
                    if (players.Count >= MatchRules.MaxPlayers) { Reject(peer, "Oda dolu."); return; }
                    if (player == null)
                    {
                        player = new RoomPlayer { Id = peer, Name = UniqueName(RoomMessage.CleanName(m[1])) };
                        players.Add(player);
                    }
                    transport.Send(peer, RoomMessage.Pack("WELCOME", peer));
                    BroadcastRoom();
                    Notice?.Invoke(player.Name + " odaya katıldı.");
                    Changed?.Invoke();
                    break;

                case "BYE":
                    OnPeerLeft(peer);
                    break;

                case "CITY":
                    if (player == null || Phase != RoomPhase.Lobby || m.Length < 2) return;
                    player.CityId = m[1];
                    BroadcastRoom();
                    Changed?.Invoke();
                    break;

                case "STATS":
                    if (player == null || Phase != RoomPhase.Playing || m.Length < 6) return;
                    Apply(player, new PlayerStats
                    {
                        Day = RoomMessage.ParseInt(m[1]), Cash = RoomMessage.ParseDouble(m[2]), Routes = RoomMessage.ParseInt(m[3]),
                        Buses = RoomMessage.ParseInt(m[4]), Bankrupt = m[5] == "1"
                    });
                    CheckWinner(player);
                    roomDirty = true;
                    Changed?.Invoke();
                    break;
            }
        }

        void HandleAsClient(string[] m)
        {
            switch (m[0])
            {
                case "WELCOME":
                    if (m.Length < 2) return;
                    LocalId = m[1];
                    Phase = RoomPhase.Lobby;
                    Changed?.Invoke();
                    break;

                case "REJECT":
                    Fail(m.Length > 1 ? m[1] : "Odaya katılınamadı.");
                    transport.Close();
                    break;

                case "ROOM":
                    if (m.Length < 4) return;
                    Mode = m[1] == MatchMode.Normal.ToString() ? MatchMode.Normal : MatchMode.Blitz;
                    Speed = Math.Max(1, RoomMessage.ParseInt(m[2]));
                    var incoming = RoomMessage.DecodePlayers(m[3]);
                    RoomPlayer local = Local;
                    players.Clear();
                    players.AddRange(incoming);
                    // Yerel oyuncunun kendi bildirdiği güncel durum, kurucudan gelen eski kopyanın önüne geçer.
                    RoomPlayer echoed = Local;
                    if (local != null && echoed != null && Phase == RoomPhase.Playing && local.Day > echoed.Day)
                    {
                        echoed.Cash = local.Cash;
                        echoed.Day = local.Day;
                        echoed.Routes = local.Routes;
                        echoed.Buses = local.Buses;
                    }
                    Changed?.Invoke();
                    break;

                case "START":
                    if (Phase != RoomPhase.Lobby) return;
                    Mode = m.Length > 1 && m[1] == MatchMode.Normal.ToString() ? MatchMode.Normal : MatchMode.Blitz;
                    Speed = m.Length > 2 ? Math.Max(1, RoomMessage.ParseInt(m[2])) : MatchRules.DefaultSpeed(Mode);
                    Phase = RoomPhase.Playing;
                    HostDay = 0;
                    Winner = null;
                    MatchStarted?.Invoke();
                    Changed?.Invoke();
                    break;

                case "TICK":
                    if (Phase != RoomPhase.Playing || m.Length < 2) return;
                    HostDay = Math.Max(HostDay, RoomMessage.ParseInt(m[1]));
                    DayAllowed?.Invoke(HostDay);
                    break;

                case "BYE":
                    HostGone(Phase == RoomPhase.Playing ? "Oda sahibi yarıştan ayrıldı; oyun tek başına sürüyor." : "Oda sahibi odayı kapattı.");
                    transport.Close();
                    break;

                case "END":
                    if (m.Length < 2 || Phase != RoomPhase.Playing) return;
                    Winner = players.Find(p => p.Id == m[1]) ?? new RoomPlayer
                    {
                        Id = m[1], Name = m.Length > 2 ? m[2] : "?", Cash = m.Length > 3 ? RoomMessage.ParseDouble(m[3]) : 0
                    };
                    Phase = RoomPhase.Ended;
                    MatchEnded?.Invoke(Winner);
                    Changed?.Invoke();
                    break;
            }
        }

        /// <summary>Katılımcı: kurucuya ulaşılamıyor ya da kurucu ayrıldı. Yarış sürüyorsa oyun tek başına devam eder.</summary>
        void HostGone(string status)
        {
            if (Phase == RoomPhase.Idle || Phase == RoomPhase.Closed) return;
            Phase = RoomPhase.Closed;
            Status = status;
            Notice?.Invoke(Status);
            Closed?.Invoke();
            Changed?.Invoke();
        }

        void OnPeerLeft(string peer)
        {
            if (!IsHost) return;
            RoomPlayer player = players.Find(p => p.Id == peer);
            if (player == null || (Phase != RoomPhase.Lobby && !player.Connected)) return;
            if (Phase == RoomPhase.Lobby) players.Remove(player);
            else player.Connected = false;
            Notice?.Invoke(player.Name + " odadan ayrıldı.");
            BroadcastRoom();
            Changed?.Invoke();
        }

        void OnError(string kind)
        {
            if (Phase == RoomPhase.Idle || Phase == RoomPhase.Closed) return; // ilk hata durumu belirledi
            // Kod başka bir odada kullanılıyorsa yeni kodla birkaç kez daha dene
            if (IsHost && kind == "unavailable-id" && Phase == RoomPhase.Connecting && hostAttempts++ < 3)
            {
                Code = MatchRules.NewCode(random);
                transport.Close();
                transport.Host(Code);
                Changed?.Invoke();
                return;
            }
            if (Phase == RoomPhase.Playing || Phase == RoomPhase.Ended)
            {
                Notice?.Invoke("Bağlantı sorunu: " + Describe(kind));
                return;
            }
            Fail(Describe(kind));
        }

        static string Describe(string kind)
        {
            switch (kind)
            {
                case "peer-unavailable": return "Bu kodla açık bir oda bulunamadı. Kodu kontrol et; odayı kuranın oyunu açık kalmalı.";
                case "timeout":
                case "negotiation-failed":
                case "webrtc":
                    return "Odaya bağlanılamadı: iki cihaz arasında bağlantı kurulamadı. İkiniz de oyunu açık tutun; olmazsa biriniz Wi-Fi ya da mobil veriye geçip tekrar deneyin.";
                case "server-timeout":
                case "network":
                case "server-error":
                case "socket-error":
                case "socket-closed":
                case "disconnected":
                    return "Eşleştirme sunucusuna ulaşılamadı. İnternet bağlantını kontrol edip tekrar dene.";
                case "unavailable-id": return "Oda açılamadı, tekrar deneyin.";
                case "browser-incompatible": return "Tarayıcınız çok oyunculu bağlantıyı desteklemiyor.";
                case "unsupported": return "Çok oyunculu şimdilik web sürümünde oynanıyor.";
                case "script": return "Bağlantı kitaplığı yüklenemedi; internet bağlantınızı kontrol edin.";
                default: return "Bağlantı kurulamadı (" + kind + "). İnternet bağlantınızı kontrol edip tekrar deneyin.";
            }
        }

        void Fail(string message)
        {
            Status = message;
            Phase = RoomPhase.Closed;
            Closed?.Invoke();
            Changed?.Invoke();
        }

        void Reject(string peer, string reason) => transport.Send(peer, RoomMessage.Pack("REJECT", reason));

        void CheckWinner(RoomPlayer player)
        {
            if (!IsHost || Phase != RoomPhase.Playing || player.Bankrupt || player.Cash < TargetCash) return;
            Winner = player;
            Phase = RoomPhase.Ended;
            BroadcastRoom();
            transport.Broadcast(RoomMessage.Pack("END", player.Id, player.Name, RoomMessage.Num(player.Cash), RoomMessage.Num(player.Day)));
            MatchEnded?.Invoke(player);
        }

        static void Apply(RoomPlayer player, PlayerStats stats)
        {
            player.Day = stats.Day;
            player.Cash = stats.Cash;
            player.Routes = stats.Routes;
            player.Buses = stats.Buses;
            player.Bankrupt = stats.Bankrupt;
        }

        void BroadcastRoom()
        {
            if (!IsHost) return;
            roomDirty = false;
            sinceBroadcast = 0f;
            transport.Broadcast(RoomMessage.Pack("ROOM", Mode.ToString(), RoomMessage.Num(Speed), RoomMessage.EncodePlayers(players)));
        }

        string UniqueName(string name)
        {
            string candidate = name;
            for (int i = 2; players.Exists(p => p.Name == candidate); i++) candidate = name + " " + i;
            return candidate;
        }
    }
}
