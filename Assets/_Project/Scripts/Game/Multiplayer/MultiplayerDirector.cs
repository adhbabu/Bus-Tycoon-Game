using System;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Multiplayer;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Odayı (<see cref="RoomSession"/>) oyuna bağlar: yarış başlayınca her oyuncu seçtiği şehirde yeni şirket kurar,
    /// saat hızı modla sabitlenir; kurucunun her biten günü katılımcılara iletilir (katılımcının saati o güne kadar
    /// ilerler), her gün sonunda kasa odaya bildirilir. Kurucu ayrılırsa katılımcı tek başına devam eder.
    /// </summary>
    public sealed class MultiplayerDirector : MonoBehaviour
    {
        public static MultiplayerDirector Instance { get; private set; }

        /// <summary>Web bağlantısı (PeerJS).</summary>
        public static readonly Func<IRoomTransport> WebFactory = () => new WebRoomTransport();
        /// <summary>Bağlantı üretici: web'de PeerJS, testlerde bellek içi bağlantıyla değiştirilir.</summary>
        public static Func<IRoomTransport> TransportFactory = WebFactory;

        /// <summary>Katılım bağlantısındaki "?oda=KOD" (ana menü odaya katılma ekranını açar).</summary>
        public static string InviteCode => MatchRules.NormalizeCode(LaunchOptions.Get("oda") ?? LaunchOptions.Get("room"));

        GameSession session;
        string companyName;

        public RoomSession Room { get; private set; }
        /// <summary>Yarış sürüyor ya da bitti ama oda açık (sıralama gösterilir, saat kurallı).</summary>
        public bool InMatch => Room != null && (Room.Phase == RoomPhase.Playing || Room.Phase == RoomPhase.Ended);
        /// <summary>Saat odaya bağlı: hız değiştirilemez, duraklatılamaz.</summary>
        public bool ClockLocked => Room != null && Room.Phase == RoomPhase.Playing;

        public event Action MatchStarted;
        public event Action<RoomPlayer> MatchEnded;
        public event Action<string> Notice;

        public void Init(GameSession gameSession)
        {
            Instance = this;
            session = gameSession;
            session.DayCompleted += OnDayCompleted;
            session.Bankrupt += OnBankrupt;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Room?.Leave();
            if (session == null) return;
            session.DayCompleted -= OnDayCompleted;
            session.Bankrupt -= OnBankrupt;
        }

        // ------------------------------------------------------------------ oda

        public void CreateRoom(string playerName, MatchMode mode)
        {
            Begin(playerName);
            Room.Create(playerName, mode);
        }

        public void JoinRoom(string playerName, string code)
        {
            Begin(playerName);
            Room.Join(playerName, code);
        }

        void Begin(string playerName)
        {
            Leave();
            companyName = RoomMessage.CleanName(playerName);
            Room = new RoomSession(TransportFactory());
            Room.MatchStarted += OnMatchStarted;
            Room.DayAllowed += OnDayAllowed;
            Room.MatchEnded += OnMatchEnded;
            Room.Notice += OnNotice;
            Room.Closed += OnRoomClosed;
            Room.Changed += LogChanges;
            lastLogged = null;
        }

        string lastLogged;

        /// <summary>Konsola oda durumu (aşama, kod, oyuncu sayısı) yalnız değişince: canlı sürümde hata ayıklama için.</summary>
        void LogChanges()
        {
            if (Room == null) return;
            string line = $"[Multiplayer] {Room.Phase} {Room.Code} {(Room.IsHost ? "kurucu" : "katılımcı")} oyuncu={Room.Players.Count}" +
                          (string.IsNullOrEmpty(Room.Status) ? string.Empty : " · " + Room.Status);
            if (line == lastLogged) return;
            lastLogged = line;
            Debug.Log(line);
        }

        /// <summary>Odadan çık: yarış sürüyorsa oyun tek başına devam eder.</summary>
        public void Leave()
        {
            if (Room == null) return;
            Room.MatchStarted -= OnMatchStarted;
            Room.DayAllowed -= OnDayAllowed;
            Room.MatchEnded -= OnMatchEnded;
            Room.Notice -= OnNotice;
            Room.Closed -= OnRoomClosed;
            Room.Changed -= LogChanges;
            Room.Leave();
            Room = null;
            ReleaseClock();
        }

        void Update() => Room?.Update(Time.unscaledDeltaTime);

        // ------------------------------------------------------------------ yarış

        void OnMatchStarted()
        {
            RoomPlayer local = Room.Local;
            CityData city = session.World.GetCity(local?.CityId) ?? session.World.GetCity("TR_ISTANBUL");
            if (city == null) return;
            session.StartNewGame(companyName, city);
            session.Clock.SetSpeed(Room.Speed);
            session.Clock.DayLimit = Room.IsHost ? (int?)null : Room.HostDay;
            Report();
            MatchStarted?.Invoke();
        }

        void OnDayAllowed(int day)
        {
            if (session.Clock != null && !Room.IsHost) session.Clock.DayLimit = day;
        }

        void OnDayCompleted(DayReport _)
        {
            if (Room == null || Room.Phase != RoomPhase.Playing || !session.IsRunning) return;
            if (Room.IsHost) Room.HostDayEnded(session.State.currentDay);
            Report();
        }

        void OnBankrupt()
        {
            if (Room != null && Room.Phase == RoomPhase.Playing) Report();
        }

        void Report()
        {
            if (Room == null || !session.IsRunning) return;
            Room.ReportStats(new PlayerStats
            {
                Day = session.State.currentDay,
                Cash = session.Finance.Cash,
                Routes = session.Routes.Routes.Count,
                Buses = session.Fleet.Buses.Count,
                Bankrupt = session.IsBankrupt
            });
        }

        void OnMatchEnded(RoomPlayer winner)
        {
            ReleaseClock();
            MatchEnded?.Invoke(winner);
        }

        void OnRoomClosed() => ReleaseClock();

        void OnNotice(string text) => Notice?.Invoke(text);

        /// <summary>Yarış bitince ya da oda kapanınca saat oyuncuya geçer.</summary>
        void ReleaseClock()
        {
            if (session != null && session.Clock != null) session.Clock.DayLimit = null;
        }
    }
}
