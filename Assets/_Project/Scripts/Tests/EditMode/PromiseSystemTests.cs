// PromiseSystemTests.cs
// V1.0 G.2 — PromiseSystem.CheckProgress 4종 + Create 헬퍼 + MoraleSystem.OnInterview wire-up.

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class PromiseSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _madeAt = new DateTime(2026, 8, 1);
        private readonly DateTime _afterDeadline = new DateTime(2027, 6, 1);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            EventBus.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            EventBus.Clear();
        }

        // ── T1. CreatePlaytimeAgreement — Promise 등록 + 이벤트 발행 ─

        [Test]
        public void T1_CreatePlaytimeAgreement_AddsToStateAndFiresEvent()
        {
            var state = NewState(currentDate: _madeAt);
            var p = NewPlayer(1, clubId: 1);
            state.AddPlayer(p);

            int createdCount = 0;
            int receivedId = -1;
            Action<PromiseCreatedEvent> handler = e =>
            {
                createdCount++;
                receivedId = e.promiseId;
            };
            EventBus.Subscribe(handler);

            var promise = PromiseSystem.CreatePlaytimeAgreement(state, 1, 50, _balance);

            EventBus.Unsubscribe(handler);

            Assert.AreEqual(1, state.activePromises.Count, "T1: Promise 1개 등록");
            Assert.AreEqual(PromiseStatus.Active, promise.status, "T1: status Active");
            Assert.AreEqual(PromiseType.PlaytimeAgreement, promise.type, "T1: 타입");
            Assert.AreEqual(50, promise.targets["minPlayRatio"], "T1: minPlayRatio 50");
            Assert.AreEqual(1, createdCount, "T1: PromiseCreatedEvent 1회");
            Assert.AreEqual(promise.id, receivedId, "T1: 페이로드 일치");
        }

        // ── T2. PlaytimeAgreement Fulfilled ─────────────────────────

        [Test]
        public void T2_PlaytimeAgreement_FulfilledWhenRatioMet()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 1, happiness: 50);
            state.AddPlayer(p);
            AddLeagueWithMatch(state, clubId: 1, matchDate: _madeAt.AddDays(30), playerStarted: 1);

            var promise = MakeActivePlaytime(state, playerId: 1, ratio: 50);

            int fulfilledCount = 0;
            Action<PromiseFulfilledEvent> handler = _ => fulfilledCount++;
            EventBus.Subscribe(handler);

            PromiseSystem.CheckProgress(state, _balance);

            EventBus.Unsubscribe(handler);

            Assert.AreEqual(PromiseStatus.Fulfilled, promise.status, "T2: 100% > 50% → Fulfilled");
            Assert.AreEqual(60, p.state.happiness, "T2: happiness +10 (loyalty 50 = 1.0)");
            Assert.AreEqual(1, fulfilledCount, "T2: PromiseFulfilledEvent 발행");
        }

        // ── T3. PlaytimeAgreement Broken → TransferRequest ──────────

        [Test]
        public void T3_PlaytimeAgreement_BrokenWhenRatioMissed_FiresTransferRequest()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 1, happiness: 25, loyalty: 50);
            state.AddPlayer(p);
            // 매치 1 — 선수 X (다른 선수 starting11)
            AddLeagueWithMatch(state, clubId: 1, matchDate: _madeAt.AddDays(30), playerStarted: 99);

            var promise = MakeActivePlaytime(state, playerId: 1, ratio: 50);

            int brokenCount = 0;
            int transferRequestCount = 0;
            Action<PromiseBrokenEvent> brokenHandler = _ => brokenCount++;
            Action<TransferRequestEvent> trHandler = _ => transferRequestCount++;
            EventBus.Subscribe(brokenHandler);
            EventBus.Subscribe(trHandler);

            PromiseSystem.CheckProgress(state, _balance);

            EventBus.Unsubscribe(brokenHandler);
            EventBus.Unsubscribe(trHandler);

            Assert.AreEqual(PromiseStatus.Broken, promise.status, "T3: 0% < 50% → Broken");
            Assert.AreEqual(5, p.state.happiness, "T3: happiness 25 - 20 = 5");
            Assert.AreEqual(1, brokenCount, "T3: PromiseBrokenEvent 발행");
            Assert.AreEqual(1, transferRequestCount, "T3: happiness < 20 → TransferRequest 발행");
        }

        // ── T4. Renewal Fulfilled — contract.startDate ≥ madeAt ─────

        [Test]
        public void T4_Renewal_FulfilledWhenContractRenewed()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 1);
            p.contract.startDate = _madeAt.AddDays(10); // 약속 후 재계약
            state.AddPlayer(p);

            var promise = MakeActiveRenewal(state, playerId: 1);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Fulfilled, promise.status, "T4: 재계약 → Fulfilled");
        }

        // ── T5. Renewal Broken ──────────────────────────────────────

        [Test]
        public void T5_Renewal_BrokenWhenContractUnchanged()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 1);
            p.contract.startDate = _madeAt.AddDays(-365); // 1년 전 계약 그대로
            state.AddPlayer(p);

            var promise = MakeActiveRenewal(state, playerId: 1);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Broken, promise.status, "T5: 미재계약 → Broken");
        }

        // ── T6. TransferIn Fulfilled ────────────────────────────────

        [Test]
        public void T6_TransferIn_FulfilledWhenPositionSigned()
        {
            var state = NewState(currentDate: _afterDeadline);
            var club = NewClub(1);
            // 약속 받는 선수 (madeAt 이전 영입)
            var receiver = NewPlayer(1, clubId: 1);
            receiver.contract.startDate = _madeAt.AddDays(-100);
            state.AddPlayer(receiver);
            club.seniorSquadIds.Add(1);
            // 약속 후 영입 — ST 1명
            var newSigning = NewPlayer(2, clubId: 1, position: Position.ST);
            newSigning.contract.startDate = _madeAt.AddDays(20);
            state.AddPlayer(newSigning);
            club.seniorSquadIds.Add(2);
            state.AddClub(club);

            var promise = MakeActiveTransferIn(state, playerId: 1, clubId: 1, position: Position.ST);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Fulfilled, promise.status, "T6: ST 영입 완료 → Fulfilled");
        }

        // ── T7. TransferIn Broken ───────────────────────────────────

        [Test]
        public void T7_TransferIn_BrokenWhenNoSigning()
        {
            var state = NewState(currentDate: _afterDeadline);
            var club = NewClub(1);
            var receiver = NewPlayer(1, clubId: 1);
            receiver.contract.startDate = _madeAt.AddDays(-100);
            state.AddPlayer(receiver);
            club.seniorSquadIds.Add(1);
            state.AddClub(club);

            var promise = MakeActiveTransferIn(state, playerId: 1, clubId: 1, position: Position.ST);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Broken, promise.status, "T7: 영입 X → Broken");
        }

        // ── T8. TransferOut Fulfilled — 다른 클럽으로 이동 ──────────

        [Test]
        public void T8_TransferOut_FulfilledWhenLeftOriginalClub()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 2); // 다른 클럽
            state.AddPlayer(p);

            var promise = new Promise
            {
                id = 1,
                playerId = 1,
                type = PromiseType.TransferOut,
                madeAt = _madeAt,
                deadline = _madeAt.AddDays(60),
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int> { ["originalClubId"] = 1 },
            };
            state.activePromises.Add(promise);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Fulfilled, promise.status, "T8: 다른 클럽 이동 → Fulfilled");
        }

        // ── T9. TransferOut Broken ──────────────────────────────────

        [Test]
        public void T9_TransferOut_BrokenWhenStillAtClub()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 1); // 같은 클럽
            state.AddPlayer(p);

            var promise = new Promise
            {
                id = 1,
                playerId = 1,
                type = PromiseType.TransferOut,
                madeAt = _madeAt,
                deadline = _madeAt.AddDays(60),
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int> { ["originalClubId"] = 1 },
            };
            state.activePromises.Add(promise);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Broken, promise.status, "T9: 그대로 → Broken");
        }

        // ── T10. CheckProgress — deadline 전 → no-op ────────────────

        [Test]
        public void T10_BeforeDeadline_NoFinalize()
        {
            var state = NewState(currentDate: _madeAt.AddDays(5));
            var p = NewPlayer(1, clubId: 1);
            state.AddPlayer(p);

            var promise = MakeActivePlaytime(state, playerId: 1, ratio: 50);
            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Active, promise.status, "T10: deadline 전 → 그대로");
        }

        // ── T11. CheckProgress — non-Active 무시 ────────────────────

        [Test]
        public void T11_NonActivePromise_Skipped()
        {
            var state = NewState(currentDate: _afterDeadline);
            var p = NewPlayer(1, clubId: 1, happiness: 50);
            state.AddPlayer(p);

            var promise = MakeActivePlaytime(state, playerId: 1, ratio: 50);
            promise.status = PromiseStatus.Fulfilled; // 이미 처리됨

            PromiseSystem.CheckProgress(state, _balance);

            Assert.AreEqual(PromiseStatus.Fulfilled, promise.status, "T11: 변경 X");
            Assert.AreEqual(50, p.state.happiness, "T11: 사기 변동 X");
        }

        // ── T12. OnInterview PromisePlaytime → Promise 생성 ─────────

        [Test]
        public void T12_OnInterview_PromisePlaytime_CreatesPromise()
        {
            var state = NewState(currentDate: _madeAt);
            var p = NewPlayer(1, clubId: 1);
            state.AddPlayer(p);

            MoraleSystem.OnInterview(state, 1, InterviewType.PromisePlaytime, _balance);

            Assert.AreEqual(1, state.activePromises.Count, "T12: Promise 1개 생성");
            Assert.AreEqual(
                PromiseType.PlaytimeAgreement,
                state.activePromises[0].type,
                "T12: 타입 일치"
            );
            Assert.AreEqual(
                _balance.promisePlaytimeDefaultRatio,
                state.activePromises[0].targets["minPlayRatio"],
                "T12: 기본 ratio 적용"
            );
        }

        // ── T13. OnInterview PromiseRenewal → Promise 생성 ──────────

        [Test]
        public void T13_OnInterview_PromiseRenewal_CreatesPromise()
        {
            var state = NewState(currentDate: _madeAt);
            var p = NewPlayer(1, clubId: 1);
            state.AddPlayer(p);

            MoraleSystem.OnInterview(state, 1, InterviewType.PromiseRenewal, _balance);

            Assert.AreEqual(1, state.activePromises.Count, "T13: Promise 1개 생성");
            Assert.AreEqual(PromiseType.Renewal, state.activePromises[0].type, "T13: 타입 일치");
        }

        // ── T14. PromiseDeadlineApproachingEvent — 30일 이내 진입 시 1회 발행 ──

        [Test]
        public void T14_DeadlineApproaching_FiresOnceWithinThreshold()
        {
            // deadline = madeAt + 60일. currentDate = deadline - 20일 (임계 30일 이내)
            var deadline = _madeAt.AddDays(60);
            var state = NewState(currentDate: deadline.AddDays(-20));
            var p = NewPlayer(1, clubId: 1);
            state.AddPlayer(p);

            var promise = new Promise
            {
                id = 1,
                playerId = 1,
                type = PromiseType.PlaytimeAgreement,
                madeAt = _madeAt,
                deadline = deadline,
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int> { ["minPlayRatio"] = 50 },
            };
            state.activePromises.Add(promise);

            int approachingCount = 0;
            int receivedDays = -1;
            Action<PromiseDeadlineApproachingEvent> handler = e =>
            {
                approachingCount++;
                receivedDays = e.daysRemaining;
            };
            EventBus.Subscribe(handler);

            // 첫 호출 → 발행 + 플래그 설정
            PromiseSystem.CheckProgress(state, _balance);
            // 두 번째 호출 (같은 주) → 중복 발행 X
            PromiseSystem.CheckProgress(state, _balance);

            EventBus.Unsubscribe(handler);

            Assert.AreEqual(1, approachingCount, "T14: 30일 이내 진입 시 1회만 발행");
            Assert.AreEqual(20, receivedDays, "T14: daysRemaining 페이로드");
            Assert.IsTrue(promise.deadlineNotified, "T14: deadlineNotified 플래그 설정");
            Assert.AreEqual(PromiseStatus.Active, promise.status, "T14: 아직 deadline 전 → 그대로 Active");
        }

        // ── T15. DeadlineApproaching — 임계 밖이면 발행 X ────────────

        [Test]
        public void T15_DeadlineApproaching_NotFiredOutsideThreshold()
        {
            // deadline = madeAt + 60일. currentDate = madeAt + 20일 (deadline - 40일, 임계 30일 밖)
            var deadline = _madeAt.AddDays(60);
            var state = NewState(currentDate: _madeAt.AddDays(20));
            var p = NewPlayer(1, clubId: 1);
            state.AddPlayer(p);

            var promise = new Promise
            {
                id = 1,
                playerId = 1,
                type = PromiseType.PlaytimeAgreement,
                madeAt = _madeAt,
                deadline = deadline,
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int> { ["minPlayRatio"] = 50 },
            };
            state.activePromises.Add(promise);

            int approachingCount = 0;
            Action<PromiseDeadlineApproachingEvent> handler = _ => approachingCount++;
            EventBus.Subscribe(handler);

            PromiseSystem.CheckProgress(state, _balance);

            EventBus.Unsubscribe(handler);

            Assert.AreEqual(0, approachingCount, "T15: 임계 밖 → 발행 X");
            Assert.IsFalse(promise.deadlineNotified, "T15: 플래그 그대로");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState NewState(DateTime currentDate) =>
            new GameState { currentDate = currentDate, randomSeed = 42, nextPromiseId = 1 };

        private static Player NewPlayer(
            int id,
            int clubId,
            int happiness = 70,
            int loyalty = 50,
            int professionalism = 50,
            Position position = Position.CM
        )
        {
            return new Player
            {
                id = id,
                currentClubId = clubId,
                currentAbility = 100,
                potentialAbility = 100,
                info = new PersonalInfo
                {
                    firstName = $"P{id}",
                    lastName = "Test",
                    primaryPosition = position,
                    birthDate = new DateTime(2000, 1, 1),
                },
                stats = new Stats(),
                state = new PlayerState { morale = 50, happiness = happiness },
                hiddenAttrs = new HiddenAttributes
                {
                    loyalty = loyalty,
                    professionalism = professionalism,
                },
                contract = new Contract
                {
                    weeklyWage = 1000,
                    startDate = new DateTime(2024, 1, 1),
                    endDate = new DateTime(2030, 1, 1),
                },
            };
        }

        private static Club NewClub(int id) =>
            new Club
            {
                id = id,
                name = $"Club{id}",
                leagueId = 1,
                reputation = 50,
            };

        private void AddLeagueWithMatch(GameState state, int clubId, DateTime matchDate, int playerStarted)
        {
            var match = new Match
            {
                id = 1,
                date = matchDate,
                type = CompetitionType.League,
                homeClubId = clubId,
                awayClubId = 999,
                result = new MatchResult
                {
                    homeScore = 1,
                    awayScore = 0,
                    homeStarting11 = new List<int> { playerStarted },
                    awayStarting11 = new List<int>(),
                    playerStats = new List<PlayerMatchStat>(),
                },
            };
            var league = new League
            {
                id = 1,
                clubIds = new List<int> { clubId, 999 },
                schedule = new List<Match> { match },
            };
            state.leagues.Add(league);
        }

        private Promise MakeActivePlaytime(GameState state, int playerId, int ratio)
        {
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.PlaytimeAgreement,
                madeAt = _madeAt,
                deadline = _madeAt.AddDays(60),
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int> { ["minPlayRatio"] = ratio },
            };
            state.activePromises.Add(promise);
            return promise;
        }

        private Promise MakeActiveRenewal(GameState state, int playerId)
        {
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.Renewal,
                madeAt = _madeAt,
                deadline = _madeAt.AddDays(60),
                status = PromiseStatus.Active,
            };
            state.activePromises.Add(promise);
            return promise;
        }

        private Promise MakeActiveTransferIn(
            GameState state,
            int playerId,
            int clubId,
            Position position
        )
        {
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.TransferIn,
                madeAt = _madeAt,
                deadline = _madeAt.AddDays(60),
                status = PromiseStatus.Active,
                targets = new Dictionary<string, int>
                {
                    ["clubId"] = clubId,
                    ["positionId"] = (int)position,
                    ["minCount"] = 1,
                },
            };
            state.activePromises.Add(promise);
            return promise;
        }
    }
}
