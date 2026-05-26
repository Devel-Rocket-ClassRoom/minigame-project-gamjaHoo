// ScoutReportTests.cs
// V1.0 E.1 — ScoutReport + Club.scoutingKnowledge 도메인 검증.
// 완료 조건: 직렬화 라운드트립 OK (Newtonsoft.Json — design-decisions.md #21).

using System;
using System.Collections.Generic;
using FMLite.Domain;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class ScoutReportTests
    {
        // ── ScoutReport 직렬화 라운드트립 ────────────────────────────

        [Test]
        public void ScoutReport_Roundtrip_PreservesAllFields()
        {
            var report = new ScoutReport
            {
                playerId = 42,
                scoutLevel = 75,
                lastUpdated = new DateTime(2026, 5, 26),
                caEstimate = new CaPaEstimate { estimate = 145, margin = 10 },
                paEstimate = new CaPaEstimate { estimate = 180, margin = 15 },
                revealedTraitIds = new List<int> { 3, 7, 12 },
                revealedHidden = new HiddenAttributesPartial
                {
                    loyalty = 80,
                    ambition = 65,
                    professionalism = null,
                },
            };

            string json = JsonConvert.SerializeObject(report);
            var loaded = JsonConvert.DeserializeObject<ScoutReport>(json);

            Assert.AreEqual(42, loaded.playerId);
            Assert.AreEqual(75, loaded.scoutLevel);
            Assert.AreEqual(new DateTime(2026, 5, 26), loaded.lastUpdated);
            Assert.AreEqual(145, loaded.caEstimate.estimate);
            Assert.AreEqual(10, loaded.caEstimate.margin);
            Assert.AreEqual(180, loaded.paEstimate.estimate);
            Assert.AreEqual(15, loaded.paEstimate.margin);
            CollectionAssert.AreEqual(new[] { 3, 7, 12 }, loaded.revealedTraitIds);
            Assert.AreEqual(80, loaded.revealedHidden.loyalty);
            Assert.AreEqual(65, loaded.revealedHidden.ambition);
            Assert.IsNull(loaded.revealedHidden.professionalism);
        }

        // ── Club.scoutingKnowledge Dictionary 라운드트립 ────────────

        [Test]
        public void Club_ScoutingKnowledge_RoundtripsDictionary()
        {
            var club = new Club { id = 1, scoutingKnowledge = new Dictionary<int, ScoutReport>() };
            club.scoutingKnowledge[101] = new ScoutReport
            {
                playerId = 101,
                scoutLevel = 50,
                caEstimate = new CaPaEstimate { estimate = 120, margin = 20 },
            };
            club.scoutingKnowledge[202] = new ScoutReport
            {
                playerId = 202,
                scoutLevel = 100,
                caEstimate = new CaPaEstimate { estimate = 180, margin = 0 },
            };

            string json = JsonConvert.SerializeObject(club);
            var loaded = JsonConvert.DeserializeObject<Club>(json);

            Assert.AreEqual(2, loaded.scoutingKnowledge.Count);
            Assert.IsTrue(loaded.scoutingKnowledge.ContainsKey(101));
            Assert.IsTrue(loaded.scoutingKnowledge.ContainsKey(202));
            Assert.AreEqual(50, loaded.scoutingKnowledge[101].scoutLevel);
            Assert.AreEqual(100, loaded.scoutingKnowledge[202].scoutLevel);
            Assert.AreEqual(180, loaded.scoutingKnowledge[202].caEstimate.estimate);
            Assert.AreEqual(0, loaded.scoutingKnowledge[202].caEstimate.margin);
        }

        // ── ClubGenerator 기본 — scoutingKnowledge 빈 Dictionary ────

        [Test]
        public void Club_NewInstance_ScoutingKnowledgeNotNull()
        {
            var club = new Club();
            Assert.IsNotNull(
                club.scoutingKnowledge,
                "default = new Dictionary<int, ScoutReport>()"
            );
            Assert.AreEqual(0, club.scoutingKnowledge.Count);
        }

        // ── HiddenAttributesPartial nullable 검증 ──────────────────

        [Test]
        public void HiddenAttributesPartial_AllNullable()
        {
            var hidden = new HiddenAttributesPartial();
            Assert.IsNull(hidden.loyalty);
            Assert.IsNull(hidden.ambition);
            Assert.IsNull(hidden.professionalism);
            Assert.IsNull(hidden.pressureHandling);
            Assert.IsNull(hidden.temperament);
            Assert.IsNull(hidden.injuryProneness);
            Assert.IsNull(hidden.consistency);
            Assert.IsNull(hidden.versatility);
        }

        [Test]
        public void HiddenAttributesPartial_Roundtrip_PartialReveal()
        {
            var hidden = new HiddenAttributesPartial
            {
                loyalty = 80,
                professionalism = 65,
                // 나머지 6 필드 null (스카우트 시설 등급 부족으로 미공개)
            };

            string json = JsonConvert.SerializeObject(hidden);
            var loaded = JsonConvert.DeserializeObject<HiddenAttributesPartial>(json);

            Assert.AreEqual(80, loaded.loyalty);
            Assert.AreEqual(65, loaded.professionalism);
            Assert.IsNull(loaded.ambition);
            Assert.IsNull(loaded.pressureHandling);
            Assert.IsNull(loaded.injuryProneness);
        }

        // ── CaPaEstimate 라운드트립 ─────────────────────────────────

        [Test]
        public void CaPaEstimate_Roundtrip()
        {
            var est = new CaPaEstimate { estimate = 145, margin = 12 };
            string json = JsonConvert.SerializeObject(est);
            var loaded = JsonConvert.DeserializeObject<CaPaEstimate>(json);
            Assert.AreEqual(145, loaded.estimate);
            Assert.AreEqual(12, loaded.margin);
        }
    }
}
