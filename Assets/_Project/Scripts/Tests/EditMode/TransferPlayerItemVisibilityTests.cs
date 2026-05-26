// TransferPlayerItemVisibilityTests.cs
// V1.0 E.4 — TransferPlayerItem 의 가시성 분기 로직 검증.
// Setup 자체는 MonoBehaviour 호출이라 PlayMode 영역.
// 대신 가시성 판정 + 라벨 산정 (ScoutingVisibility 사용) 의 통합 시나리오 검증.

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class TransferPlayerItemVisibilityTests
    {
        // ── 명단 ∈ vs ∉ 시 CA 표시 차이 ────────────────────────────────

        [Test]
        public void ScoutedPlayer_ExactCa_Displayed()
        {
            var userClub = NewClub(1);
            userClub.scoutingKnowledge[101] = new ScoutReport { playerId = 101, scoutLevel = 100 };

            bool scouted = ScoutingVisibility.IsScouted(userClub, 101);
            Assert.IsTrue(scouted, "명단 ∈ → IsScouted true");
            // UI 표시: ca.ToString() = "145"
        }

        [Test]
        public void NotScoutedPlayer_QualitativeLabel()
        {
            var userClub = NewClub(1);
            // 명단 등록 안 함

            bool scouted = ScoutingVisibility.IsScouted(userClub, 999);
            Assert.IsFalse(scouted, "명단 ∉ → IsScouted false");

            // CA 145 → tier High (145/200 = 0.725, 0.65 이상 = High)
            var tier = ScoutingVisibility.GetTier(145, 200);
            Assert.AreEqual(ScoutTier.High, tier);
            Assert.AreEqual("scout_tier_high", ScoutingVisibility.GetTierLocalizationKey(tier));
        }

        // ── Market value 정성적 라벨 ─────────────────────────────────

        [Test]
        public void MarketValue_NotScouted_TierBasedOn100M()
        {
            // £50M (5천만) → 5천만 / 1억 = 0.5 → Average
            var tier50M = ScoutingVisibility.GetTier(50_000_000, 100_000_000);
            Assert.AreEqual(ScoutTier.Average, tier50M);

            // £90M → 0.90 → VeryHigh
            var tier90M = ScoutingVisibility.GetTier(90_000_000, 100_000_000);
            Assert.AreEqual(ScoutTier.VeryHigh, tier90M);

            // £10M → 0.10 → VeryLow
            var tier10M = ScoutingVisibility.GetTier(10_000_000, 100_000_000);
            Assert.AreEqual(ScoutTier.VeryLow, tier10M);
        }

        // ── 다중 선수 명단 / 비명단 혼합 ─────────────────────────────

        [Test]
        public void MixedScoutingKnowledge_BothPathsActive()
        {
            var userClub = NewClub(1);
            userClub.scoutingKnowledge[101] = new ScoutReport { playerId = 101 };
            userClub.scoutingKnowledge[102] = new ScoutReport { playerId = 102 };

            Assert.IsTrue(ScoutingVisibility.IsScouted(userClub, 101));
            Assert.IsTrue(ScoutingVisibility.IsScouted(userClub, 102));
            Assert.IsFalse(ScoutingVisibility.IsScouted(userClub, 103));
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static Club NewClub(int id) =>
            new Club { id = id, scoutingKnowledge = new Dictionary<int, ScoutReport>() };
    }
}
