// TransferSystemTests.cs
// DoD: algorithms.md #3 + #3.1 Test Scenarios. 이슈 #42 / #43 (Task 11.1/11.2).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class TransferSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 7, 1); // 여름 활성화 기간 안

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // 명세 디폴트값 확인 (SO 기본값)
            _balance.marketValueBase = 500_000;
            _balance.marketValueCaExponent = 4.0f;
            _balance.marketValuePaCoeff = 50_000f;
            _balance.marketValueAgeCurve = new[] { 0.85f, 1.20f, 0.75f, 0.35f };
            _balance.marketValueContractCurve = new[] { 0.50f, 0.80f, 1.00f, 1.05f };
            _balance.marketValuePositionFactor = new[] { 0.75f, 0.85f, 1.00f, 1.20f };
            _balance.marketValueInjuryFactor = 0.50f;
            _balance.aiValueNoiseSigma = 0.10f;
            _balance.aiAcceptRatio = 1.20f;
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

        // ── T1. Market Value 결정성 ──────────────────────────────────

        [Test]
        public void T1_MarketValue_Deterministic()
        {
            var (s1, c1, c2) = BuildState();
            var p1 = NewPlayer(
                1,
                ca: 150,
                pa: 170,
                age: 24,
                position: Position.ST,
                contractYears: 4
            );
            s1.AddPlayer(p1);
            c1.seniorSquadIds.Add(1);

            int v1 = TransferSystem.CalculateMarketValue(p1, s1, _balance);
            int v2 = TransferSystem.CalculateMarketValue(p1, s1, _balance);
            Assert.AreEqual(v1, v2, "T1: 같은 입력 동일 결과");
        }

        // ── T2. Market Value 슈퍼스타 vs 평범 — 10배 이상 차이 ───────

        [Test]
        public void T2_MarketValue_SuperstarVsAverage_Over10x()
        {
            var (state, _, _) = BuildState();
            var avg = NewPlayer(
                1,
                ca: 100,
                pa: 100,
                age: 25,
                position: Position.CM,
                contractYears: 3
            );
            var star = NewPlayer(
                2,
                ca: 180,
                pa: 200,
                age: 22,
                position: Position.ST,
                contractYears: 5
            );

            int avgValue = TransferSystem.CalculateMarketValue(avg, state, _balance);
            int starValue = TransferSystem.CalculateMarketValue(star, state, _balance);

            Assert.Greater(
                starValue,
                avgValue * 10,
                $"T2: 슈퍼스타({starValue}) ≥ 평범({avgValue}) × 10"
            );
        }

        // ── T3. AgeCurve — 피크 시기 가장 비싸 ───────────────────────

        [Test]
        public void T3_AgeCurve_PeakIsExpensive()
        {
            var (state, _, _) = BuildState();
            var young = NewPlayer(
                1,
                ca: 130,
                pa: 150,
                age: 19,
                position: Position.CM,
                contractYears: 3
            );
            var peak = NewPlayer(
                2,
                ca: 130,
                pa: 150,
                age: 25,
                position: Position.CM,
                contractYears: 3
            );
            var old = NewPlayer(
                3,
                ca: 130,
                pa: 150,
                age: 31,
                position: Position.CM,
                contractYears: 3
            );
            var done = NewPlayer(
                4,
                ca: 130,
                pa: 150,
                age: 36,
                position: Position.CM,
                contractYears: 3
            );

            int vYoung = TransferSystem.CalculateMarketValue(young, state, _balance);
            int vPeak = TransferSystem.CalculateMarketValue(peak, state, _balance);
            int vOld = TransferSystem.CalculateMarketValue(old, state, _balance);
            int vDone = TransferSystem.CalculateMarketValue(done, state, _balance);

            Assert.Greater(vPeak, vYoung, "T3: 피크(25) > 유망주(19)");
            Assert.Greater(vPeak, vOld, "T3: 피크(25) > 노장(31)");
            Assert.Greater(vOld, vDone, "T3: 노장(31) > 말년(36)");
        }

        // ── T4. ContractCurve — 잔여 1년 가장 싸 ─────────────────────

        [Test]
        public void T4_ContractCurve_LastYearCheap()
        {
            var (state, _, _) = BuildState();
            var p1 = NewPlayer(
                1,
                ca: 130,
                pa: 130,
                age: 25,
                position: Position.CM,
                contractYears: 1
            );
            var p4 = NewPlayer(
                2,
                ca: 130,
                pa: 130,
                age: 25,
                position: Position.CM,
                contractYears: 4
            );

            int v1 = TransferSystem.CalculateMarketValue(p1, state, _balance);
            int v4 = TransferSystem.CalculateMarketValue(p4, state, _balance);
            Assert.Greater(v4, v1 * 2 - 200_000, "T4: 잔여 4년이 1년의 ~2배");
        }

        // ── T5. PositionFactor + Injury ──────────────────────────────

        [Test]
        public void T5_PositionFactor_AT_Over_GK_AndInjuryDiscount()
        {
            var (state, _, _) = BuildState();
            var st = NewPlayer(
                1,
                ca: 130,
                pa: 130,
                age: 25,
                position: Position.ST,
                contractYears: 3
            );
            var gk = NewPlayer(
                2,
                ca: 130,
                pa: 130,
                age: 25,
                position: Position.GK,
                contractYears: 3
            );
            var stInjured = NewPlayer(
                3,
                ca: 130,
                pa: 130,
                age: 25,
                position: Position.ST,
                contractYears: 3
            );
            stInjured.state.injury.injuryTypeId = 1; // 부상

            int vSt = TransferSystem.CalculateMarketValue(st, state, _balance);
            int vGk = TransferSystem.CalculateMarketValue(gk, state, _balance);
            int vInjur = TransferSystem.CalculateMarketValue(stInjured, state, _balance);

            Assert.Greater(vSt, vGk, "T5: ST > GK (포지션 가중치)");
            // injuryFactor 0.50 → 절반 ±10% (round 100k)
            Assert.That(
                vInjur,
                Is.LessThan((int)(vSt * 0.6)),
                $"T5: 부상자({vInjur}) ≤ 정상 ST({vSt}) × 0.6"
            );
        }

        // ── T6. IsTransferWindowOpen ─────────────────────────────────

        [Test]
        public void T6_IsTransferWindowOpen_BothPeriods()
        {
            Assert.IsTrue(
                TransferSystem.IsTransferWindowOpen(new DateTime(2026, 6, 1), _balance),
                "T6: 6/1 여름 시작"
            );
            Assert.IsTrue(
                TransferSystem.IsTransferWindowOpen(new DateTime(2026, 8, 31), _balance),
                "T6: 8/31 여름 끝"
            );
            Assert.IsFalse(
                TransferSystem.IsTransferWindowOpen(new DateTime(2026, 9, 1), _balance),
                "T6: 9/1 닫힘"
            );
            Assert.IsFalse(
                TransferSystem.IsTransferWindowOpen(new DateTime(2026, 11, 15), _balance),
                "T6: 11/15 닫힘"
            );
            Assert.IsTrue(
                TransferSystem.IsTransferWindowOpen(new DateTime(2026, 1, 15), _balance),
                "T6: 1/15 겨울"
            );
            Assert.IsFalse(
                TransferSystem.IsTransferWindowOpen(new DateTime(2026, 2, 1), _balance),
                "T6: 2/1 닫힘"
            );
        }

        // ── T7. SubmitOffer — 정상 + Pending ─────────────────────────

        [Test]
        public void T7_SubmitOffer_Pending()
        {
            var (state, c1, c2) = BuildState();
            var p = NewPlayer(
                1,
                ca: 130,
                pa: 150,
                age: 25,
                position: Position.CM,
                contractYears: 3
            );
            p.currentClubId = c1.id;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            int eventCount = 0;
            Action<OfferSubmittedEvent> h = _ => eventCount++;
            EventBus.Subscribe(h);

            var contract = new Contract
            {
                weeklyWage = 50_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
            };
            var offer = TransferSystem.SubmitOffer(
                p.id,
                c1.id,
                c2.id,
                3_000_000,
                contract,
                state,
                _balance
            );

            Assert.IsNotNull(offer, "T7: offer 반환");
            Assert.AreEqual(OfferStatus.Pending, offer.status);
            Assert.AreEqual(1, state.activeOffers.Count, "T7: activeOffers 추가");
            Assert.AreEqual(1, eventCount, "T7: OfferSubmittedEvent 1회 발행");

            EventBus.Unsubscribe(h);
        }

        // ── T8. AI 응답 — Accept vs Reject ───────────────────────────

        [Test]
        public void T8_AI_AcceptVsReject_BasedOnRatio()
        {
            var (state, c1, c2) = BuildState();
            var p = NewPlayer(
                1,
                ca: 100,
                pa: 100,
                age: 25,
                position: Position.CM,
                contractYears: 3
            );
            p.currentClubId = c1.id;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            int marketValue = TransferSystem.CalculateMarketValue(p, state, _balance);

            // High offer (ratio ~1.5) → Accepted (대부분, noise 영향 적음)
            var contract = new Contract
            {
                weeklyWage = 30_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
            };
            var highOffer = TransferSystem.SubmitOffer(
                p.id,
                c1.id,
                c2.id,
                (int)(marketValue * 1.5),
                contract,
                state,
                _balance
            );
            TransferSystem.ProcessOffers(state, _balance);
            // 여름 활성화 기간 안 (currentDate=7/1) — Accepted → 즉시 Completed 일 수도 / 또는 Accepted 유지
            Assert.That(
                highOffer.status,
                Is.EqualTo(OfferStatus.Accepted).Or.EqualTo(OfferStatus.Completed),
                $"T8a: 높은 오퍼 → Accepted 또는 Completed (실측 {highOffer.status})"
            );

            // Low offer (ratio ~0.5) → Rejected
            // 새 선수 — 다른 player 사용
            var p2 = NewPlayer(
                2,
                ca: 100,
                pa: 100,
                age: 25,
                position: Position.CM,
                contractYears: 3
            );
            p2.currentClubId = c1.id;
            state.AddPlayer(p2);
            c1.seniorSquadIds.Add(p2.id);
            var lowOffer = TransferSystem.SubmitOffer(
                p2.id,
                c1.id,
                c2.id,
                (int)(marketValue * 0.5),
                contract,
                state,
                _balance
            );
            TransferSystem.ProcessOffers(state, _balance);
            Assert.AreEqual(OfferStatus.Rejected, lowOffer.status, "T8b: 낮은 오퍼 → Rejected");
        }

        // ── T9. Accepted 대기 → 활성화 기간 진입 시 자동 체결 ────────

        [Test]
        public void T9_AcceptedWaiting_ThenAutoCompleteOnWindowOpen()
        {
            var (state, c1, c2) = BuildState();
            state.currentDate = new DateTime(2025, 11, 15); // 활성화 기간 외
            var p = NewPlayer(
                1,
                ca: 130,
                pa: 150,
                age: 25,
                position: Position.CM,
                contractYears: 3
            );
            p.currentClubId = c1.id;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            var contract = new Contract
            {
                weeklyWage = 50_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
            };
            int marketValue = TransferSystem.CalculateMarketValue(p, state, _balance);
            var offer = TransferSystem.SubmitOffer(
                p.id,
                c1.id,
                c2.id,
                (int)(marketValue * 1.5),
                contract,
                state,
                _balance
            );

            TransferSystem.ProcessOffers(state, _balance); // Pending → Accepted (높은 ratio)
            Assert.AreEqual(OfferStatus.Accepted, offer.status, "T9 사전: Accepted 대기");
            Assert.AreEqual(c1.id, p.currentClubId, "T9 사전: 아직 이적 X (활성화 기간 외)");

            // 시간 진행 — 활성화 기간 진입 (1/1)
            state.currentDate = new DateTime(2026, 1, 1);

            int completedCount = 0;
            Action<TransferCompletedEvent> h = _ => completedCount++;
            EventBus.Subscribe(h);

            TransferSystem.ProcessOffers(state, _balance); // Accepted + IsTransferWindowOpen → Completed

            Assert.AreEqual(OfferStatus.Completed, offer.status, "T9: 활성화 기간 시 자동 체결");
            Assert.AreEqual(c2.id, p.currentClubId, "T9: 선수 이적 발효");
            Assert.IsTrue(c2.seniorSquadIds.Contains(p.id), "T9: toClub squad 추가");
            Assert.IsFalse(c1.seniorSquadIds.Contains(p.id), "T9: fromClub squad 제거");
            Assert.AreEqual(1, completedCount, "T9: TransferCompletedEvent 발행");

            EventBus.Unsubscribe(h);
        }

        // ── T10. SearchPlayers — 필터 정확 ───────────────────────────

        [Test]
        public void T10_SearchPlayers_Filter()
        {
            var (state, c1, c2) = BuildState();
            // 다양한 선수 추가
            var st1 = NewPlayer(
                1,
                ca: 130,
                pa: 150,
                age: 22,
                position: Position.ST,
                contractYears: 3
            );
            st1.currentClubId = c2.id;
            var st2 = NewPlayer(
                2,
                ca: 100,
                pa: 110,
                age: 30,
                position: Position.ST,
                contractYears: 2
            );
            st2.currentClubId = c2.id;
            var cm = NewPlayer(
                3,
                ca: 130,
                pa: 140,
                age: 24,
                position: Position.CM,
                contractYears: 3
            );
            cm.currentClubId = c2.id;
            var user = NewPlayer(
                4,
                ca: 130,
                pa: 150,
                age: 22,
                position: Position.ST,
                contractYears: 3
            );
            user.currentClubId = c1.id;
            state.AddPlayer(st1);
            state.AddPlayer(st2);
            state.AddPlayer(cm);
            state.AddPlayer(user);
            c2.seniorSquadIds.AddRange(new[] { 1, 2, 3 });
            c1.seniorSquadIds.Add(4);
            state.userClubId = c1.id;

            // 필터: ST + age 20~25 + CA 120+ + 유저 클럽 제외
            var filter = new TransferSearchFilter
            {
                position = Position.ST,
                minAge = 20,
                maxAge = 25,
                minCA = 120,
                maxCA = 200,
                excludeUserClub = true,
            };
            var result = TransferSystem.SearchPlayers(filter, state);
            Assert.AreEqual(1, result.Count, "T10: 정확히 1명 매치 (st1)");
            Assert.AreEqual(1, result[0].id);
        }

        // ── T11~T13. Release Clause (H.2 DoD) ────────────────────────

        // T11. amount ≥ releaseClause → 즉시 Accepted + releaseClauseActivated
        [Test]
        public void T11_ReleaseClause_Exact_ImmediatelyAccepted()
        {
            var (state, c1, c2) = BuildState();
            var p = NewPlayer(1, ca: 100, pa: 100, age: 25, position: Position.CM, contractYears: 3);
            p.contract.releaseClause = 5_000_000;
            p.currentClubId = c1.id;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            var contract = new Contract
            {
                weeklyWage = 30_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
            };
            var offer = TransferSystem.SubmitOffer(p.id, c1.id, c2.id, 5_000_000, contract, state, _balance);

            Assert.AreEqual(OfferStatus.Accepted, offer.status, "T11: release clause 발동 → Accepted");
            Assert.IsTrue(offer.releaseClauseActivated, "T11: releaseClauseActivated=true");
        }

        // T12. amount < releaseClause → Pending (발동 안 됨)
        [Test]
        public void T12_ReleaseClause_BelowThreshold_StaysPending()
        {
            var (state, c1, c2) = BuildState();
            var p = NewPlayer(1, ca: 100, pa: 100, age: 25, position: Position.CM, contractYears: 3);
            p.contract.releaseClause = 5_000_000;
            p.currentClubId = c1.id;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            var contract = new Contract
            {
                weeklyWage = 30_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
            };
            var offer = TransferSystem.SubmitOffer(p.id, c1.id, c2.id, 4_999_999, contract, state, _balance);

            Assert.AreEqual(OfferStatus.Pending, offer.status, "T12: 미달 → Pending");
            Assert.IsFalse(offer.releaseClauseActivated, "T12: releaseClauseActivated=false");
        }

        // T13. releaseClause=0 (없음) → 어떤 금액이어도 Pending
        [Test]
        public void T13_ReleaseClause_Zero_NeverActivated()
        {
            var (state, c1, c2) = BuildState();
            var p = NewPlayer(1, ca: 100, pa: 100, age: 25, position: Position.CM, contractYears: 3);
            p.contract.releaseClause = 0;
            p.currentClubId = c1.id;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            var contract = new Contract
            {
                weeklyWage = 30_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
            };
            var offer = TransferSystem.SubmitOffer(p.id, c1.id, c2.id, 999_999_999, contract, state, _balance);

            Assert.AreEqual(OfferStatus.Pending, offer.status, "T13: clause=0 → Pending");
            Assert.IsFalse(offer.releaseClauseActivated, "T13: releaseClauseActivated=false");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private (GameState state, Club c1, Club c2) BuildState()
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
            state.AddClub(c1);
            state.AddClub(c2);
            return (state, c1, c2);
        }

        private Player NewPlayer(
            int id,
            int ca,
            int pa,
            int age,
            Position position,
            int contractYears
        )
        {
            var birth = new DateTime(_today.Year - age, _today.Month, _today.Day);
            return new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = pa,
                info = new PersonalInfo
                {
                    primaryPosition = position,
                    firstName = "F",
                    lastName = "L",
                    birthDate = birth,
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                },
                contract = new Contract
                {
                    weeklyWage = 50_000,
                    startDate = _today,
                    endDate = _today.AddYears(contractYears),
                },
                currentClubId = 1,
            };
        }
    }
}
