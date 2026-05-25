// DomainSkeletonTests.cs
// DoD 검증: v1.0-tasks.md Stage A / Task A.4 — V1.0 도메인 클래스 스켈레톤.
// 직렬화 라운드트립 테스트 (Newtonsoft.Json) + 기본 생성 확인.

using System;
using System.Collections.Generic;
using FMLite.Domain;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class DomainSkeletonTests
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static T Roundtrip<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj, Settings);
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        // ── 신규 클래스 기본 생성 ──────────────────────────────────────────

        [Test]
        public void HiddenAttributes_Roundtrip()
        {
            var h = new HiddenAttributes
            {
                loyalty = 70,
                ambition = 80,
                professionalism = 60,
                pressureHandling = 75,
                temperament = 55,
                controversy = 20,
                injuryProneness = 30,
                consistency = 65,
                versatility = 50,
            };
            var r = Roundtrip(h);
            Assert.AreEqual(70, r.loyalty);
            Assert.AreEqual(30, r.injuryProneness);
            Assert.AreEqual(50, r.versatility);
        }

        [Test]
        public void Promise_Roundtrip()
        {
            var p = new Promise
            {
                id = 1,
                playerId = 42,
                type = PromiseType.PlaytimeAgreement,
                madeAt = new DateTime(2026, 8, 1),
                deadline = new DateTime(2027, 5, 31),
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int> { { "minAppearances", 20 } },
            };
            var r = Roundtrip(p);
            Assert.AreEqual(1, r.id);
            Assert.AreEqual(PromiseType.PlaytimeAgreement, r.type);
            Assert.AreEqual(PromiseStatus.Active, r.status);
            Assert.AreEqual(20, r.targets["minAppearances"]);
        }

        [Test]
        public void MentoringGroup_Roundtrip()
        {
            var g = new MentoringGroup
            {
                id = 1,
                mentorPlayerId = 10,
                menteePlayerIds = new List<int> { 20, 21 },
                startedAt = new DateTime(2026, 7, 1),
            };
            var r = Roundtrip(g);
            Assert.AreEqual(10, r.mentorPlayerId);
            Assert.AreEqual(2, r.menteePlayerIds.Count);
        }

        [Test]
        public void Tactic_Roundtrip()
        {
            var t = new Tactic
            {
                formationId = 1,
                mentality = Mentality.Balanced,
                slots = new List<TacticSlot>
                {
                    new TacticSlot
                    {
                        slotIndex = 0,
                        roleId = 5,
                        duty = Duty.Defend,
                        assignedPlayerId = -1,
                    },
                },
                setPieceTakers = new List<int> { 7 },
            };
            var r = Roundtrip(t);
            Assert.AreEqual(Mentality.Balanced, r.mentality);
            Assert.AreEqual(1, r.slots.Count);
            Assert.AreEqual(Duty.Defend, r.slots[0].duty);
        }

        [Test]
        public void ScoutReport_Roundtrip()
        {
            var s = new ScoutReport
            {
                playerId = 99,
                scoutLevel = 40,
                lastUpdated = new DateTime(2026, 9, 1),
                caEstimate = new CaPaEstimate { estimate = 120, margin = 15 },
                paEstimate = new CaPaEstimate { estimate = 160, margin = 20 },
                revealedHidden = new HiddenAttributesPartial { loyalty = 70, ambition = null },
            };
            var r = Roundtrip(s);
            Assert.AreEqual(99, r.playerId);
            Assert.AreEqual(120, r.caEstimate.estimate);
            Assert.AreEqual(70, r.revealedHidden.loyalty);
            Assert.IsNull(r.revealedHidden.ambition);
        }

        [Test]
        public void SeasonAward_Roundtrip()
        {
            var a = new SeasonAward
            {
                type = AwardType.TopScorer,
                playerId = 11,
                seasonYear = 2026,
            };
            var r = Roundtrip(a);
            Assert.AreEqual(AwardType.TopScorer, r.type);
            Assert.AreEqual(2026, r.seasonYear);
        }

        [Test]
        public void TraitEffect_Roundtrip()
        {
            var e = new TraitEffect
            {
                type = TraitEffectType.InjuryRateModifier,
                value = 1.5f,
                targetStat = "injuryProneness",
            };
            var r = Roundtrip(e);
            Assert.AreEqual(TraitEffectType.InjuryRateModifier, r.type);
            Assert.AreEqual(1.5f, r.value, 0.001f);
        }

        // ── 기존 클래스 확장 필드 ──────────────────────────────────────────

        [Test]
        public void Player_NewFields_Roundtrip()
        {
            var p = new Player
            {
                id = 1,
                hiddenAttrs = new HiddenAttributes { loyalty = 80, ambition = 60 },
                parentClubId = 5,
                loanEndDate = new DateTime(2027, 1, 31),
            };
            var r = Roundtrip(p);
            Assert.AreEqual(80, r.hiddenAttrs.loyalty);
            Assert.AreEqual(5, r.parentClubId);
            Assert.AreEqual(new DateTime(2027, 1, 31), r.loanEndDate);
        }

        [Test]
        public void PlayerState_NewFields_Roundtrip()
        {
            var s = new PlayerState
            {
                fatigue = 30,
                morale = 70,
                happiness = 65,
                suspendedMatches = 1,
            };
            var r = Roundtrip(s);
            Assert.AreEqual(65, r.happiness);
            Assert.AreEqual(1, r.suspendedMatches);
        }

        [Test]
        public void Contract_BonusFields_Roundtrip()
        {
            var c = new Contract
            {
                weeklyWage = 50000,
                signingBonus = 100000,
                loyaltyBonus = 200000,
                appearanceBonus = 5000,
                goalBonus = 10000,
            };
            var r = Roundtrip(c);
            Assert.AreEqual(100000, r.signingBonus);
            Assert.AreEqual(10000, r.goalBonus);
        }

        [Test]
        public void TransferOffer_LoanFields_Roundtrip()
        {
            var o = new TransferOffer
            {
                id = 1,
                playerId = 10,
                isLoan = true,
                loanFee = 500000,
                loanWageShare = 0.5f,
                loanEndDate = new DateTime(2027, 6, 30),
                loanOption = new LoanOption
                {
                    mandatoryPurchaseAtEnd = false,
                    purchaseClause = 10000000,
                    recallClause = true,
                },
                counterAmount = 0,
                negotiationRound = 1,
                releaseClauseActivated = false,
                status = OfferStatus.CounterOffer,
            };
            var r = Roundtrip(o);
            Assert.IsTrue(r.isLoan);
            Assert.AreEqual(0.5f, r.loanWageShare, 0.001f);
            Assert.IsTrue(r.loanOption.recallClause);
            Assert.AreEqual(OfferStatus.CounterOffer, r.status);
        }

        [Test]
        public void PlayerMatchStat_ExtendedFields_Roundtrip()
        {
            var s = new PlayerMatchStat
            {
                playerId = 7,
                goals = 2,
                shots = 5,
                passes = 40,
                tackles = 3,
                interceptions = 2,
                keyPasses = 4,
                foulsCommitted = 1,
                foulsSuffered = 2,
            };
            var r = Roundtrip(s);
            Assert.AreEqual(5, r.shots);
            Assert.AreEqual(4, r.keyPasses);
        }

        [Test]
        public void GameState_NewFields_Roundtrip()
        {
            var state = new GameState
            {
                managerReputation = 60,
                nextPromiseId = 3,
                nextAwardId = 2,
                activePromises = new List<Promise>
                {
                    new Promise
                    {
                        id = 1,
                        type = PromiseType.Renewal,
                        status = PromiseStatus.Active,
                    },
                },
                activeAwards = new List<SeasonAward>
                {
                    new SeasonAward
                    {
                        type = AwardType.LeagueMVP,
                        playerId = 9,
                        seasonYear = 2026,
                    },
                },
            };
            var r = Roundtrip(state);
            Assert.AreEqual(60, r.managerReputation);
            Assert.AreEqual(1, r.activePromises.Count);
            Assert.AreEqual(AwardType.LeagueMVP, r.activeAwards[0].type);
        }

        [Test]
        public void SeasonState_NewFields_Roundtrip()
        {
            var s = new SeasonState
            {
                captainPlayerId = 10,
                viceCaptainPlayerId = 7,
                dressingRoomMood = 72,
                mentoringGroups = new List<MentoringGroup>
                {
                    new MentoringGroup
                    {
                        id = 1,
                        mentorPlayerId = 10,
                        menteePlayerIds = new List<int> { 20 },
                    },
                },
            };
            var r = Roundtrip(s);
            Assert.AreEqual(10, r.captainPlayerId);
            Assert.AreEqual(72, r.dressingRoomMood);
            Assert.AreEqual(1, r.mentoringGroups.Count);
        }

        [Test]
        public void OfferStatus_CounterOffer_EnumExists()
        {
            var status = OfferStatus.CounterOffer;
            Assert.AreEqual("CounterOffer", status.ToString());
        }
    }
}
