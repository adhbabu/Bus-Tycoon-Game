using System.Collections.Generic;
using System.Linq;
using BusTycoon.Core;
using BusTycoon.Multiplayer;
using NUnit.Framework;

namespace BusTycoon.Tests
{
    public sealed class MultiplayerTests
    {
        LoopbackTransport.Hub hub;
        LoopbackTransport hostLink, aliLink;
        RoomSession host, ali, ayse;

        [SetUp]
        public void SetUp()
        {
            hub = new LoopbackTransport.Hub();
            hostLink = new LoopbackTransport(hub);
            aliLink = new LoopbackTransport(hub);
            host = new RoomSession(hostLink, 7);
            ali = new RoomSession(aliLink, 8);
            ayse = new RoomSession(new LoopbackTransport(hub), 9);
        }

        void Pump(int rounds = 4)
        {
            for (int i = 0; i < rounds; i++)
            {
                host.Update(1f);
                ali.Update(1f);
                ayse.Update(1f);
            }
        }

        void Lobby(MatchMode mode = MatchMode.Blitz)
        {
            host.Create("Kurucu", mode);
            Pump();
            ali.Join("Ali", host.Code.ToLowerInvariant());
            ayse.Join("Ayşe", host.Code);
            Pump();
        }

        [Test]
        public void PlayersJoinWithCode_AndEveryoneSeesTheSameLobby()
        {
            Lobby();
            Assert.That(host.Phase, Is.EqualTo(RoomPhase.Lobby));
            Assert.That(host.Code.Length, Is.EqualTo(MatchRules.CodeLength));
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Lobby));
            Assert.That(host.Players.Count, Is.EqualTo(3));
            Assert.That(ali.Players.Count, Is.EqualTo(3));
            Assert.That(ayse.Local.Name, Is.EqualTo("Ayşe"));
            Assert.That(ali.Players.Any(p => p.IsHost && p.Name == "Kurucu"), Is.True);
        }

        [Test]
        public void WrongCode_FailsWithReadableMessage()
        {
            ali.Join("Ali", "ZZZZZ");
            Pump();
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(ali.Status, Does.Contain("bulunamadı"));
        }

        [Test]
        public void Start_NeedsTwoPlayersWithCities_ThenBroadcastsModeAndSpeed()
        {
            Lobby(MatchMode.Normal);
            Assert.That(host.CanStart, Is.False, "Şehir seçilmeden başlamaz.");
            host.SetCity("TR_ISTANBUL");
            ali.SetCity("TR_ANKARA");
            ayse.SetCity("TR_IZMIR");
            Pump();
            Assert.That(host.CanStart, Is.True);
            Assert.That(ali.Players.First(p => p.Name == "Ayşe").CityId, Is.EqualTo("TR_IZMIR"));

            bool started = false;
            ali.MatchStarted += () => started = true;
            host.Start();
            Pump();
            Assert.That(started, Is.True);
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Playing));
            Assert.That(ali.Mode, Is.EqualTo(MatchMode.Normal));
            Assert.That(ali.TargetCash, Is.EqualTo(10_000_000));
            Assert.That(ali.Speed, Is.EqualTo(MatchRules.DefaultSpeed(MatchMode.Normal)));
        }

        [Test]
        public void HostClock_AllowsDays_AndStatsReachTheLeaderboard()
        {
            Lobby();
            host.SetCity("TR_ISTANBUL");
            ali.SetCity("TR_ANKARA");
            ayse.SetCity("TR_IZMIR");
            Pump();
            host.Start();
            Pump();

            int allowed = -1;
            ali.DayAllowed += day => allowed = day;
            host.HostDayEnded(3);
            ali.ReportStats(new PlayerStats { Day = 3, Cash = 52_000, Routes = 2, Buses = 3 });
            host.ReportStats(new PlayerStats { Day = 3, Cash = 41_000, Routes = 1, Buses = 1 });
            Pump();
            Assert.That(allowed, Is.EqualTo(3));
            List<RoomPlayer> standings = ayse.Standings();
            Assert.That(standings[0].Name, Is.EqualTo("Ali"));
            Assert.That(standings[0].Cash, Is.EqualTo(52_000));
            Assert.That(standings[1].Name, Is.EqualTo("Kurucu"));
        }

        [Test]
        public void FirstToTarget_WinsForEveryone()
        {
            Lobby(MatchMode.Blitz);
            host.SetCity("TR_ISTANBUL");
            ali.SetCity("TR_ANKARA");
            ayse.SetCity("TR_IZMIR");
            Pump();
            host.Start();
            Pump();

            RoomPlayer seen = null;
            ayse.MatchEnded += winner => seen = winner;
            ali.ReportStats(new PlayerStats { Day = 200, Cash = 999_999 });
            Pump();
            Assert.That(host.Phase, Is.EqualTo(RoomPhase.Playing));
            ali.ReportStats(new PlayerStats { Day = 201, Cash = 1_000_500 });
            Pump();
            Assert.That(host.Phase, Is.EqualTo(RoomPhase.Ended));
            Assert.That(host.Winner.Name, Is.EqualTo("Ali"));
            Assert.That(seen, Is.Not.Null);
            Assert.That(seen.Name, Is.EqualTo("Ali"));
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Ended));
        }

        [Test]
        public void BankruptPlayer_CannotWin()
        {
            Lobby(MatchMode.Blitz);
            host.SetCity("TR_ISTANBUL");
            ali.SetCity("TR_ANKARA");
            ayse.SetCity("TR_IZMIR");
            Pump();
            host.Start();
            Pump();
            ali.ReportStats(new PlayerStats { Day = 50, Cash = 2_000_000, Bankrupt = true });
            Pump();
            Assert.That(host.Phase, Is.EqualTo(RoomPhase.Playing));
            Assert.That(host.Standings()[2].Name, Is.EqualTo("Ali"), "İflas eden sıralamada sona düşer.");
        }

        [Test]
        public void JoiningAfterStart_IsRejected_AndLeavingIsAnnounced()
        {
            host.Create("Kurucu", MatchMode.Blitz);
            Pump();
            ali.Join("Ali", host.Code);
            Pump();
            host.SetCity("TR_ISTANBUL");
            ali.SetCity("TR_ANKARA");
            Pump();
            host.Start();
            Pump();
            ayse.Join("Ayşe", host.Code);
            Pump();
            Assert.That(ayse.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(ayse.Status, Does.Contain("başladı"));

            var notices = new List<string>();
            host.Notice += notices.Add;
            ali.Leave();
            Pump();
            Assert.That(notices, Has.Count.EqualTo(1), "Ayrılma iletisi ve bağlantının kapanması tek duyuru olur.");
            Assert.That(notices[0], Does.Contain("Ali"));
            Assert.That(host.Players.First(p => p.Name == "Ali").Connected, Is.False);
        }

        [Test]
        public void HostLeavingRace_PlayersContinueAlone_WithClearMessage()
        {
            host.Create("Kurucu", MatchMode.Blitz);
            Pump();
            ali.Join("Ali", host.Code);
            Pump();
            host.SetCity("TR_ISTANBUL");
            ali.SetCity("TR_ANKARA");
            Pump();
            host.Start();
            Pump();
            host.Leave();
            Pump();
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(ali.Status, Does.Contain("yarıştan ayrıldı"));
        }

        [Test]
        public void LostConnection_WithoutGoodbye_IsReportedAsDisconnect()
        {
            Lobby();
            hostLink.Close(); // kurucunun bağlantısı haber vermeden koptu
            Pump();
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(ali.Status, Does.Contain("bağlantı koptu"));
        }

        [Test]
        public void OnePlayersConnectionError_DoesNotCloseTheRoom()
        {
            Lobby();
            hostLink.SimulateError("baska-oyuncu", "negotiation-failed");
            Pump();
            Assert.That(host.Phase, Is.EqualTo(RoomPhase.Lobby));
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Lobby));

            // Katılımcının bağlantısı kurulamazsa (NAT, zaman aşımı) anlaşılır mesajla biter; sonraki hatalar mesajı ezmez
            var strangerLink = new LoopbackTransport(hub);
            var stranger = new RoomSession(strangerLink, 12);
            stranger.Join("Yabancı", host.Code);
            strangerLink.SimulateError(null, "timeout");
            strangerLink.SimulateError(null, "network");
            stranger.Update(1f);
            Assert.That(stranger.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(stranger.Status, Does.Contain("bağlanılamadı"));
        }

        [Test]
        public void HostLeaving_ClosesRoomForPlayers()
        {
            Lobby();
            bool closed = false;
            ali.Closed += () => closed = true;
            host.Leave();
            Pump();
            Assert.That(closed, Is.True);
            Assert.That(ali.Phase, Is.EqualTo(RoomPhase.Closed));
            Assert.That(ali.Status, Does.Contain("odayı kapattı"));
        }

        [Test]
        public void Messages_SurviveNamesWithSeparators()
        {
            var players = new List<RoomPlayer> { new RoomPlayer { Id = "p1", Name = "A|B;C~D", CityId = "TR_RIZE", Cash = 1234.5, Day = 9, IsHost = true } };
            List<RoomPlayer> decoded = RoomMessage.DecodePlayers(RoomMessage.EncodePlayers(players));
            Assert.That(decoded.Count, Is.EqualTo(1));
            Assert.That(decoded[0].Name, Is.EqualTo("ABCD"));
            Assert.That(decoded[0].Cash, Is.EqualTo(1234.5));
            Assert.That(decoded[0].IsHost, Is.True);
            Assert.That(MatchRules.NormalizeCode(" ab-c2 d9x"), Is.EqualTo("ABC2D"));
        }

        [Test]
        public void ClockWithDayLimit_WaitsForHost_AndCatchesUp()
        {
            var clock = new SimulationClock(0, 1f) { DayLimit = 2 };
            int ended = 0;
            clock.DayEnded += _ => ended++;
            for (int i = 0; i < 10; i++) clock.Tick(1f);
            Assert.That(clock.CurrentDay, Is.EqualTo(2), "Kurucunun gününü geçmez.");
            Assert.That(clock.DayProgress01, Is.EqualTo(1f).Within(1e-4), "Sınırda gün dolu bekler.");

            clock.DayLimit = 6;
            clock.Tick(0.01f);
            Assert.That(clock.CurrentDay, Is.GreaterThanOrEqualTo(3), "Geride kalınca hemen ilerler.");
            for (int i = 0; i < 10; i++) clock.Tick(0.01f);
            Assert.That(clock.CurrentDay, Is.EqualTo(5), "Bir gün geride kalınca normal hızda oynar.");
            clock.Tick(1f);
            Assert.That(clock.CurrentDay, Is.EqualTo(6));
            Assert.That(ended, Is.EqualTo(6));
        }
    }
}
