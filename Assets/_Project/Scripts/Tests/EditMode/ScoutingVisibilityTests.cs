// ScoutingVisibilityTests.cs
// V0.5 E.3 — 가시성 판정 + 5단계 라벨 검증.

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class ScoutingVisibilityTests
    {
        // ── IsScouted ────────────────────────────────────────────────

        [Test]
        public void IsScouted_PlayerInKnowledge_ReturnsTrue()
        {
            var club = NewClub();
            club.scoutingKnowledge[101] = new ScoutReport { playerId = 101, scoutLevel = 50 };
            Assert.IsTrue(ScoutingVisibility.IsScouted(club, 101));
        }

        [Test]
        public void IsScouted_PlayerNotInKnowledge_ReturnsFalse()
        {
            var club = NewClub();
            Assert.IsFalse(ScoutingVisibility.IsScouted(club, 999));
        }

        [Test]
        public void IsScouted_DebugMode_AlwaysTrue()
        {
            var club = NewClub();
            Assert.IsTrue(ScoutingVisibility.IsScouted(club, 999, isDebugMode: true));
        }

        [Test]
        public void IsScouted_NullClub_ReturnsFalse()
        {
            Assert.IsFalse(ScoutingVisibility.IsScouted(null, 101));
        }

        [Test]
        public void IsScouted_NullClub_DebugMode_ReturnsTrue()
        {
            Assert.IsTrue(ScoutingVisibility.IsScouted(null, 101, isDebugMode: true));
        }

        // ── GetTier — CA 기반 (max 200) ──────────────────────────────

        [Test]
        public void GetTier_CA_VeryHigh()
        {
            Assert.AreEqual(ScoutTier.VeryHigh, ScoutingVisibility.GetTier(180, 200)); // 0.90
            Assert.AreEqual(ScoutTier.VeryHigh, ScoutingVisibility.GetTier(170, 200)); // 0.85 경계
        }

        [Test]
        public void GetTier_CA_High()
        {
            Assert.AreEqual(ScoutTier.High, ScoutingVisibility.GetTier(150, 200)); // 0.75
            Assert.AreEqual(ScoutTier.High, ScoutingVisibility.GetTier(130, 200)); // 0.65 경계
        }

        [Test]
        public void GetTier_CA_Average()
        {
            Assert.AreEqual(ScoutTier.Average, ScoutingVisibility.GetTier(120, 200)); // 0.60
            Assert.AreEqual(ScoutTier.Average, ScoutingVisibility.GetTier(90, 200)); // 0.45 경계
        }

        [Test]
        public void GetTier_CA_Low()
        {
            Assert.AreEqual(ScoutTier.Low, ScoutingVisibility.GetTier(70, 200)); // 0.35
            Assert.AreEqual(ScoutTier.Low, ScoutingVisibility.GetTier(50, 200)); // 0.25 경계
        }

        [Test]
        public void GetTier_CA_VeryLow()
        {
            Assert.AreEqual(ScoutTier.VeryLow, ScoutingVisibility.GetTier(40, 200)); // 0.20
            Assert.AreEqual(ScoutTier.VeryLow, ScoutingVisibility.GetTier(0, 200));
        }

        // ── GetTier — Stat 기반 (max 100) ────────────────────────────

        [Test]
        public void GetTier_Stat_BoundaryValues()
        {
            // stat 1-100 기준
            Assert.AreEqual(ScoutTier.VeryHigh, ScoutingVisibility.GetTier(85, 100));
            Assert.AreEqual(ScoutTier.High, ScoutingVisibility.GetTier(65, 100));
            Assert.AreEqual(ScoutTier.Average, ScoutingVisibility.GetTier(45, 100));
            Assert.AreEqual(ScoutTier.Low, ScoutingVisibility.GetTier(25, 100));
            Assert.AreEqual(ScoutTier.VeryLow, ScoutingVisibility.GetTier(24, 100));
        }

        // ── GetTier — Edge ──────────────────────────────────────────

        [Test]
        public void GetTier_ZeroMax_ReturnsVeryLow()
        {
            Assert.AreEqual(ScoutTier.VeryLow, ScoutingVisibility.GetTier(50, 0));
            Assert.AreEqual(ScoutTier.VeryLow, ScoutingVisibility.GetTier(50, -10));
        }

        // ── GetTierLocalizationKey ──────────────────────────────────

        [Test]
        public void GetTierLocalizationKey_AllTiersMapped()
        {
            Assert.AreEqual(
                "scout_tier_very_high",
                ScoutingVisibility.GetTierLocalizationKey(ScoutTier.VeryHigh)
            );
            Assert.AreEqual(
                "scout_tier_high",
                ScoutingVisibility.GetTierLocalizationKey(ScoutTier.High)
            );
            Assert.AreEqual(
                "scout_tier_average",
                ScoutingVisibility.GetTierLocalizationKey(ScoutTier.Average)
            );
            Assert.AreEqual(
                "scout_tier_low",
                ScoutingVisibility.GetTierLocalizationKey(ScoutTier.Low)
            );
            Assert.AreEqual(
                "scout_tier_very_low",
                ScoutingVisibility.GetTierLocalizationKey(ScoutTier.VeryLow)
            );
        }

        // ── CanRevealDetails (R.6 #77-1) ─────────────────────────────

        [Test]
        public void CanRevealDetails_OwnClubPlayer_ReturnsTrue()
        {
            var club = NewClub(); // id=1
            var player = new Player { id = 50, currentClubId = 1 };
            Assert.IsTrue(ScoutingVisibility.CanRevealDetails(club, player));
        }

        [Test]
        public void CanRevealDetails_OtherClubNotScouted_ReturnsFalse()
        {
            var club = NewClub();
            var player = new Player { id = 50, currentClubId = 2 }; // 타팀, 미정찰
            Assert.IsFalse(ScoutingVisibility.CanRevealDetails(club, player));
        }

        [Test]
        public void CanRevealDetails_OtherClubScouted_ReturnsTrue()
        {
            var club = NewClub();
            club.scoutingKnowledge[50] = new ScoutReport { playerId = 50, scoutLevel = 20 };
            var player = new Player { id = 50, currentClubId = 2 }; // 타팀, 정찰됨
            Assert.IsTrue(ScoutingVisibility.CanRevealDetails(club, player));
        }

        [Test]
        public void CanRevealDetails_DebugMode_AlwaysTrue()
        {
            var club = NewClub();
            var player = new Player { id = 50, currentClubId = 2 };
            Assert.IsTrue(ScoutingVisibility.CanRevealDetails(club, player, isDebugMode: true));
        }

        [Test]
        public void CanRevealDetails_NullPlayer_ReturnsFalse()
        {
            var club = NewClub();
            Assert.IsFalse(ScoutingVisibility.CanRevealDetails(club, null));
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static Club NewClub() =>
            new Club { id = 1, scoutingKnowledge = new Dictionary<int, ScoutReport>() };
    }
}
