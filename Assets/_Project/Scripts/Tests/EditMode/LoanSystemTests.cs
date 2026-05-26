// LoanSystemTests.cs
// V1.0 K.3 — Loan System DoD: 임대 → 종료 → 자동 복귀 라운드트립.
// algorithms.md V1.0-3.1 DailyProcessor 임대 복귀 처리 / design-decisions.md #48.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class LoanSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 7, 1); // 이적시장 활성화 기간

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.marketValueBase = 5_000_000;
            _balance.marketValueCaExponent = 4.0f;
            _balance.marketValuePaCoeff = 0f;
            _balance.marketValueAgeCurve = new float[] { 0.8f, 1.0f, 0.9f, 0.7f };
            _balance.marketValueContractCurve = new float[] { 0.5f, 0.8f, 1.0f, 1.05f };
            _balance.marketValuePositionFactor = new float[] { 0.75f, 0.85f, 1.0f, 1.2f };
            _balance.marketValueInjuryFactor = 0.5f;
            _balance.aiValueNoiseSigma = 0.10f;
            _balance.aiAcceptThreshold = 1.30f;
            _balance.aiCounterOfferThreshold = 1.10f;
            _balance.aiMockingThreshold = 0.85f;
            _balance.aiCounterOfferFactor = 1.30f;
            _balance.maxNegotiationRounds = 3;
            _balance.aiMockingMoralePenalty = 3;
            _balance.playtimeAgreementBonus = 0.2f;
            _balance.wageBaseAtMinCA = 500;
            _balance.wagePerCAPoint = 350f;
            _balance.wageFloor = 500;
            _balance.minCA = 30;
            _balance.transferWindowSummerStartMonth = 6;
            _balance.transferWindowSummerStartDay = 1;
            _balance.transferWindowSummerEndMonth = 8;
            _balance.transferWindowSummerEndDay = 31;
            _balance.transferWindowWinterStartMonth = 1;
            _balance.transferWindowWinterStartDay = 1;
            _balance.transferWindowWinterEndMonth = 1;
            _balance.transferWindowWinterEndDay = 31;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── T1. SubmitLoanOffer → Accepted, isLoan=true ───────────────

        [Test]
        public void T1_SubmitLoanOffer_AcceptedAndIsLoan()
        {
            var (state, c1, c2, player) = BuildState();
            var term = BuildLoanTerm(_today.AddMonths(6));

            var offer = TransferSystem.SubmitLoanOffer(player.id, c1.id, c2.id, term, state, _balance);

            Assert.AreEqual(OfferStatus.Accepted, offer.status, "T1: 임대 오퍼 즉시 Accepted");
            Assert.IsTrue(offer.isLoan, "T1: isLoan = true");
            Assert.AreEqual(term.loanEndDate, offer.loanEndDate, "T1: loanEndDate 복사");
        }

        // ── T2. CompleteTransfer(loan) → player in toClub, parentClubId 설정 ──

        [Test]
        public void T2_CompleteTransfer_Loan_PlayerMovesToLoanClub()
        {
            var (state, c1, c2, player) = BuildState();
            var term = BuildLoanTerm(_today.AddMonths(6));
            _ = TransferSystem.SubmitLoanOffer(player.id, c1.id, c2.id, term, state, _balance);

            TransferSystem.ProcessOffers(state, _balance); // 이적창 열린 날 → CompleteTransfer

            Assert.AreEqual(c2.id, player.currentClubId, "T2: player.currentClubId = toClub");
            Assert.AreEqual(c1.id, player.parentClubId, "T2: player.parentClubId = fromClub");
            Assert.AreEqual(term.loanEndDate, player.loanEndDate, "T2: player.loanEndDate 설정");
            Assert.IsFalse(c1.seniorSquadIds.Contains(player.id), "T2: fromClub 명단 제거");
            Assert.IsTrue(c2.seniorSquadIds.Contains(player.id), "T2: toClub 명단 추가");
        }

        // ── T3. 임대 종료 → 자동 복귀 (K.3 핵심 DoD) ────────────────

        [Test]
        public void T3_LoanReturn_PlayerReturnsToParentClub()
        {
            var (state, c1, c2, player) = BuildState();
            var loanEnd = _today.AddMonths(6);
            var term = BuildLoanTerm(loanEnd);
            _ = TransferSystem.SubmitLoanOffer(player.id, c1.id, c2.id, term, state, _balance);
            TransferSystem.ProcessOffers(state, _balance);

            state.currentDate = loanEnd;

            bool eventFired = false;
            EventBus.Subscribe<LoanReturnedEvent>(e => eventFired = true);
            TransferSystem.ProcessLoanReturns(state);

            Assert.AreEqual(c1.id, player.currentClubId, "T3: player 원 구단 복귀");
            Assert.AreEqual(-1, player.parentClubId, "T3: parentClubId 초기화");
            Assert.IsNull(player.loanEndDate, "T3: loanEndDate 초기화");
            Assert.IsTrue(c1.seniorSquadIds.Contains(player.id), "T3: 원 구단 명단 복귀");
            Assert.IsFalse(c2.seniorSquadIds.Contains(player.id), "T3: 임차 구단 명단 제거");
            Assert.IsTrue(eventFired, "T3: LoanReturnedEvent 발행");
        }

        // ── T4. loanFee 자금 이동 ─────────────────────────────────────

        [Test]
        public void T4_LoanFee_TransferredCorrectly()
        {
            var (state, c1, c2, player) = BuildState();
            int initialC1Money = c1.finance.money;
            int initialC2Money = c2.finance.money;
            int loanFee = 500_000;
            var term = BuildLoanTerm(_today.AddMonths(6), loanFee: loanFee);
            _ = TransferSystem.SubmitLoanOffer(player.id, c1.id, c2.id, term, state, _balance);

            TransferSystem.ProcessOffers(state, _balance);

            Assert.AreEqual(initialC1Money + loanFee, c1.finance.money, "T4: fromClub loanFee 수령");
            Assert.AreEqual(initialC2Money - loanFee, c2.finance.money, "T4: toClub loanFee 지출");
        }

        // ── T5. 임대 종료 전 → 복귀 없음 ─────────────────────────────

        [Test]
        public void T5_BeforeLoanEnd_NoReturn()
        {
            var (state, c1, c2, player) = BuildState();
            var loanEnd = _today.AddMonths(6);
            var term = BuildLoanTerm(loanEnd);
            _ = TransferSystem.SubmitLoanOffer(player.id, c1.id, c2.id, term, state, _balance);
            TransferSystem.ProcessOffers(state, _balance);

            state.currentDate = loanEnd.AddDays(-1);
            TransferSystem.ProcessLoanReturns(state);

            Assert.AreEqual(c2.id, player.currentClubId, "T5: 종료 전 — toClub 유지");
            Assert.AreEqual(c1.id, player.parentClubId, "T5: parentClubId 유지");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private (GameState state, Club c1, Club c2, Player player) BuildState()
        {
            var state = new GameState
            {
                currentDate = _today,
                randomSeed = 42,
                userClubId = -1,
                nextPlayerId = 100,
                nextIntakeId = 1,
                nextOfferId = 1,
            };
            var c1 = new Club
            {
                id = 1,
                name = "FromClub",
                reputation = 60,
                leagueId = 1,
                finance = new Finance { money = 50_000_000 },
            };
            var c2 = new Club
            {
                id = 2,
                name = "ToClub",
                reputation = 70,
                leagueId = 1,
                finance = new Finance { money = 50_000_000 },
            };
            var player = new Player
            {
                id = 1,
                currentAbility = 100,
                potentialAbility = 110,
                currentClubId = c1.id,
                info = new PersonalInfo
                {
                    firstName = "T",
                    lastName = "P",
                    birthDate = _today.AddYears(-25),
                    primaryPosition = Position.CM,
                },
                state = new PlayerState
                {
                    morale = 50,
                    happiness = 50,
                    fatigue = 0,
                    form = 50,
                    injury = new InjuryInfo { injuryTypeId = -1 },
                },
                contract = new Contract
                {
                    weeklyWage = 25_000,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddYears(2),
                },
                hiddenAttrs = new HiddenAttributes { loyalty = 50, ambition = 50 },
            };
            state.AddClub(c1);
            state.AddClub(c2);
            state.AddPlayer(player);
            c1.seniorSquadIds.Add(player.id);
            return (state, c1, c2, player);
        }

        private LoanTerm BuildLoanTerm(DateTime loanEndDate, int loanFee = 0)
        {
            return new LoanTerm
            {
                loanFee = loanFee,
                loanWageShare = 0.5f,
                loanEndDate = loanEndDate,
                proposed = new Contract
                {
                    weeklyWage = 25_000,
                    startDate = _today,
                    endDate = loanEndDate,
                },
                option = null,
            };
        }
    }
}
