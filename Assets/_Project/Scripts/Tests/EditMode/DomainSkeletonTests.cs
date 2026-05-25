// DomainSkeletonTests.cs
// DoD 검증: v1.0-tasks.md Stage A / Task A.4 — V1.0 도메인 클래스 스켈레톤.
// 클래스 인스턴스 생성 + 필드 할당/읽기 검증.

using System;
using System.Collections.Generic;
using FMLite.Domain;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class DomainSkeletonTests
    {
        // ── 신규 클래스 기본 생성 ──────────────────────────────────────────

        [Test]
        public void HiddenAttributes_FieldsAssignable()
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
            Assert.AreEqual(70, h.loyalty);
            Assert.AreEqual(30, h.injuryProneness);
            Assert.AreEqual(50, h.versatility);
        }

        [Test]
        public void Promise_FieldsAndEnums_Assignable()
        {
            var p = new Promise
            {
                id = 1,
                playerId = 42,
                type = PromiseType.PlaytimeAgreement,
                madeAt = new DateTime(2026, 8, 1),
                deadline = new DateTime(2027, 5, 31),
                status = PromiseStatus.Active,
            };
            p.targets["minAppearances"] = 20;

            Assert.AreEqual(PromiseType.PlaytimeAgreement, p.type);
            Assert.AreEqual(PromiseStatus.Active, p.status);
            Assert.AreEqual(20, p.targets["minAppearances"]);
        }

        [Test]
        public void Promise_AllTypeValues_Defined()
        {
            Assert.DoesNotThrow(() =>
            {
                _ = PromiseType.PlaytimeAgreement;
                _ = PromiseType.TransferIn;
                _ = PromiseType.Renewal;
                _ = PromiseType.TransferOut;
                _ = PromiseStatus.Active;
                _ = PromiseStatus.Fulfilled;
                _ = PromiseStatus.Broken;
            });
        }

        [Test]
        public void MentoringGroup_FieldsAssignable()
        {
            var g = new MentoringGroup
            {
                id = 1,
                mentorPlayerId = 10,
                menteePlayerIds = new List<int> { 20, 21 },
                startedAt = new DateTime(2026, 7, 1),
            };
            Assert.AreEqual(10, g.mentorPlayerId);
            Assert.AreEqual(2, g.menteePlayerIds.Count);
        }

        [Test]
        public void Tactic_FieldsAndEnums_Assignable()
        {
            var slot = new TacticSlot
            {
                slotIndex = 0,
                roleId = 5,
                duty = Duty.Defend,
                assignedPlayerId = -1,
            };
            var t = new Tactic
            {
                formationId = 1,
                mentality = Mentality.Balanced,
                slots = new List<TacticSlot> { slot },
                setPieceTakers = new List<int> { 7 },
            };

            Assert.AreEqual(Mentality.Balanced, t.mentality);
            Assert.AreEqual(1, t.slots.Count);
            Assert.AreEqual(Duty.Defend, t.slots[0].duty);
        }

        [Test]
        public void Mentality_AllValues_Defined()
        {
            var values = (Mentality[])Enum.GetValues(typeof(Mentality));
            Assert.AreEqual(7, values.Length);
        }

        [Test]
        public void ScoutReport_FieldsAssignable()
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

            Assert.AreEqual(99, s.playerId);
            Assert.AreEqual(120, s.caEstimate.estimate);
            Assert.AreEqual(70, s.revealedHidden.loyalty);
            Assert.IsNull(s.revealedHidden.ambition);
        }

        [Test]
        public void SeasonAward_AllAwardTypes_Defined()
        {
            var values = (AwardType[])Enum.GetValues(typeof(AwardType));
            Assert.AreEqual(7, values.Length);

            var a = new SeasonAward
            {
                type = AwardType.TopScorer,
                playerId = 11,
                seasonYear = 2026,
            };
            Assert.AreEqual(AwardType.TopScorer, a.type);
        }

        [Test]
        public void TraitEffect_FieldsAndEnum_Assignable()
        {
            var e = new TraitEffect
            {
                type = TraitEffectType.InjuryRateModifier,
                value = 1.5f,
                targetStat = "injuryProneness",
            };
            Assert.AreEqual(TraitEffectType.InjuryRateModifier, e.type);
            Assert.AreEqual(1.5f, e.value, 0.001f);
        }

        [Test]
        public void LoanOption_FieldsAssignable()
        {
            var lo = new LoanOption
            {
                mandatoryPurchaseAtEnd = false,
                purchaseClause = 10_000_000,
                recallClause = true,
            };
            Assert.IsTrue(lo.recallClause);
            Assert.AreEqual(10_000_000, lo.purchaseClause);
        }

        // ── 기존 클래스 확장 필드 ──────────────────────────────────────────

        [Test]
        public void Player_NewFields_Assignable()
        {
            var p = new Player
            {
                id = 1,
                hiddenAttrs = new HiddenAttributes { loyalty = 80 },
                parentClubId = 5,
                loanEndDate = new DateTime(2027, 1, 31),
            };
            Assert.AreEqual(80, p.hiddenAttrs.loyalty);
            Assert.AreEqual(5, p.parentClubId);
            Assert.AreEqual(new DateTime(2027, 1, 31), p.loanEndDate);
        }

        [Test]
        public void PlayerState_NewFields_Assignable()
        {
            var s = new PlayerState { happiness = 65, suspendedMatches = 1 };
            Assert.AreEqual(65, s.happiness);
            Assert.AreEqual(1, s.suspendedMatches);
        }

        [Test]
        public void PlayerState_HappinessDefault_Is70()
        {
            var s = new PlayerState();
            Assert.AreEqual(70, s.happiness);
        }

        [Test]
        public void Contract_BonusFields_Assignable()
        {
            var c = new Contract
            {
                signingBonus = 100_000,
                loyaltyBonus = 200_000,
                appearanceBonus = 5_000,
                goalBonus = 10_000,
            };
            Assert.AreEqual(100_000, c.signingBonus);
            Assert.AreEqual(10_000, c.goalBonus);
        }

        [Test]
        public void TransferOffer_LoanAndNegotiationFields_Assignable()
        {
            var o = new TransferOffer
            {
                isLoan = true,
                loanFee = 500_000,
                loanWageShare = 0.5f,
                loanEndDate = new DateTime(2027, 6, 30),
                loanOption = new LoanOption { recallClause = true },
                counterAmount = 8_000_000,
                negotiationRound = 2,
                releaseClauseActivated = false,
                status = OfferStatus.CounterOffer,
            };
            Assert.IsTrue(o.isLoan);
            Assert.AreEqual(0.5f, o.loanWageShare, 0.001f);
            Assert.IsTrue(o.loanOption.recallClause);
            Assert.AreEqual(OfferStatus.CounterOffer, o.status);
        }

        [Test]
        public void OfferStatus_CounterOffer_Defined()
        {
            Assert.AreEqual("CounterOffer", OfferStatus.CounterOffer.ToString());
        }

        [Test]
        public void PlayerMatchStat_ExtendedFields_Assignable()
        {
            var s = new PlayerMatchStat
            {
                shots = 5,
                passes = 40,
                tackles = 3,
                interceptions = 2,
                keyPasses = 4,
                foulsCommitted = 1,
                foulsSuffered = 2,
            };
            Assert.AreEqual(5, s.shots);
            Assert.AreEqual(4, s.keyPasses);
        }

        [Test]
        public void GameState_NewFields_Assignable()
        {
            var state = new GameState
            {
                managerReputation = 60,
                nextPromiseId = 3,
                nextAwardId = 2,
            };
            state.activePromises.Add(
                new Promise
                {
                    id = 1,
                    type = PromiseType.Renewal,
                    status = PromiseStatus.Active,
                }
            );
            state.activeAwards.Add(
                new SeasonAward
                {
                    type = AwardType.LeagueMVP,
                    playerId = 9,
                    seasonYear = 2026,
                }
            );

            Assert.AreEqual(60, state.managerReputation);
            Assert.AreEqual(1, state.activePromises.Count);
            Assert.AreEqual(AwardType.LeagueMVP, state.activeAwards[0].type);
        }

        [Test]
        public void SeasonState_NewFields_Assignable()
        {
            var s = new SeasonState
            {
                captainPlayerId = 10,
                viceCaptainPlayerId = 7,
                dressingRoomMood = 72,
            };
            s.mentoringGroups.Add(new MentoringGroup { id = 1, mentorPlayerId = 10 });

            Assert.AreEqual(10, s.captainPlayerId);
            Assert.AreEqual(72, s.dressingRoomMood);
            Assert.AreEqual(1, s.mentoringGroups.Count);
        }

        [Test]
        public void Facilities_NewLevelFields_Assignable()
        {
            var f = new Facilities
            {
                youthCoachLevel = 3,
                youthRecruitmentLevel = 2,
                youthFacilityLevel = 4,
                medicalLevel = 1,
                stadiumLevel = 5,
                gymLevel = 2,
            };
            Assert.AreEqual(3, f.youthCoachLevel);
            Assert.AreEqual(5, f.stadiumLevel);
        }
    }
}
