// PromiseSystem.cs
// V1.0 G.2 — 매니저-선수 약속 4종 라이프사이클.
// algorithms.md V1.0-6 PromiseSystem.CheckProgress / design-decisions.md #43.
// Stateless (design-decisions.md #3). 모든 상태는 GameState.activePromises.
//
// 호출 시점:
//   - 매주 월요일 → DailyProcessor 가 CheckProgress(state, balance) 호출 (deadline 도래 시 status 확정)
//   - 면담 / 협상 → MoraleSystem.OnInterview / TransferSystem 등이 Create* 헬퍼 호출
//
// Promise 4종 (design-decisions.md #43):
//   - PlaytimeAgreement — targets["minPlayRatio"] (0-100). 시즌 매치 출전 비율 ≥ 목표 → Fulfilled.
//   - TransferIn        — targets["clubId"] + ["positionId"] + ["minCount"]. 약속 후 영입된 그 포지션 인원.
//   - Renewal           — player.contract.startDate ≥ promise.madeAt → Fulfilled.
//   - TransferOut       — targets["originalClubId"]. player.currentClubId != originalClubId → Fulfilled.
//
// 처리 흐름 (deadline 도래 후 CheckProgress):
//   1. Evaluate (4종 분기)
//   2. promise.status = Fulfilled / Broken
//   3. MoraleSystem.OnPromiseFulfilled / OnPromiseBroken 직접 호출 (사기 변동)
//   4. PromiseFulfilledEvent / PromiseBrokenEvent 발행 (UI 인박스 — Sub-B 구독)

using System;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class PromiseSystem
    {
        // ── CheckProgress (DailyProcessor 매주 호출) ──────────────────

        public static void CheckProgress(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (state.activePromises == null)
                return;

            // snapshot — CheckProgress 가 list 변경 X (status 만 갱신) 이지만 안전 패턴.
            int count = state.activePromises.Count;
            for (int i = 0; i < count; i++)
            {
                var promise = state.activePromises[i];
                if (promise == null)
                    continue;
                if (promise.status != PromiseStatus.Active)
                    continue;
                if (state.currentDate < promise.deadline)
                    continue;

                bool fulfilled = EvaluatePromise(promise, state);
                if (fulfilled)
                {
                    promise.status = PromiseStatus.Fulfilled;
                    MoraleSystem.OnPromiseFulfilled(state, promise, balance);
                    EventBus.Publish(new PromiseFulfilledEvent { promiseId = promise.id });
                }
                else
                {
                    promise.status = PromiseStatus.Broken;
                    MoraleSystem.OnPromiseBroken(state, promise, balance);
                    EventBus.Publish(new PromiseBrokenEvent { promiseId = promise.id });
                }
            }
        }

        // ── 4종 Evaluator ────────────────────────────────────────────

        private static bool EvaluatePromise(Promise promise, GameState state)
        {
            switch (promise.type)
            {
                case PromiseType.PlaytimeAgreement:
                    return EvaluatePlaytimeAgreement(promise, state);
                case PromiseType.TransferIn:
                    return EvaluateTransferIn(promise, state);
                case PromiseType.Renewal:
                    return EvaluateRenewal(promise, state);
                case PromiseType.TransferOut:
                    return EvaluateTransferOut(promise, state);
                default:
                    return false;
            }
        }

        private static bool EvaluatePlaytimeAgreement(Promise promise, GameState state)
        {
            int minRatio = GetTarget(promise, "minPlayRatio", 50);
            var player = state.GetPlayer(promise.playerId);
            if (player?.state == null)
                return false;

            int clubId = player.currentClubId;
            if (clubId == -1)
                return false; // 무소속 → 약속 무효 → Broken 처리

            // 약속 이후 매치 중 본인 출전 매치 비율.
            int totalClubMatches = 0;
            int playerStarted = 0;
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var match in league.schedule)
                {
                    if (match?.result == null)
                        continue;
                    if (match.date < promise.madeAt)
                        continue;
                    bool involvesClub = match.homeClubId == clubId || match.awayClubId == clubId;
                    if (!involvesClub)
                        continue;
                    totalClubMatches++;
                    if (
                        (
                            match.result.homeStarting11 != null
                            && match.result.homeStarting11.Contains(player.id)
                        )
                        || (
                            match.result.awayStarting11 != null
                            && match.result.awayStarting11.Contains(player.id)
                        )
                    )
                        playerStarted++;
                }
            }

            if (totalClubMatches == 0)
                return true; // 시즌 매치 자체 X → vacuously fulfilled
            int actualPct = playerStarted * 100 / totalClubMatches;
            return actualPct >= minRatio;
        }

        private static bool EvaluateTransferIn(Promise promise, GameState state)
        {
            int clubId = GetTarget(promise, "clubId", -1);
            int positionId = GetTarget(promise, "positionId", -1);
            int minCount = GetTarget(promise, "minCount", 1);
            if (clubId == -1 || positionId == -1)
                return false;

            var club = state.GetClub(clubId);
            if (club?.seniorSquadIds == null)
                return false;

            int signed = 0;
            foreach (var pid in club.seniorSquadIds)
            {
                var p = state.GetPlayer(pid);
                if (p?.info == null || p.contract == null)
                    continue;
                if ((int)p.info.primaryPosition != positionId)
                    continue;
                if (p.contract.startDate < promise.madeAt)
                    continue;
                signed++;
            }
            return signed >= minCount;
        }

        private static bool EvaluateRenewal(Promise promise, GameState state)
        {
            var player = state.GetPlayer(promise.playerId);
            if (player?.contract == null)
                return false;
            return player.contract.startDate >= promise.madeAt;
        }

        private static bool EvaluateTransferOut(Promise promise, GameState state)
        {
            int originalClubId = GetTarget(promise, "originalClubId", -1);
            if (originalClubId == -1)
                return false;
            var player = state.GetPlayer(promise.playerId);
            if (player == null)
                return false;
            return player.currentClubId != originalClubId;
        }

        // ── Create 헬퍼 ──────────────────────────────────────────────

        public static Promise CreatePlaytimeAgreement(
            GameState state,
            int playerId,
            int minPlayRatio,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            DateTime deadline = ComputeNextSeasonEnd(state.currentDate, balance);
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.PlaytimeAgreement,
                madeAt = state.currentDate,
                deadline = deadline,
                status = PromiseStatus.Active,
            };
            promise.targets["minPlayRatio"] = minPlayRatio;
            state.activePromises.Add(promise);
            EventBus.Publish(new PromiseCreatedEvent { promiseId = promise.id });
            return promise;
        }

        public static Promise CreateRenewal(GameState state, int playerId, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            DateTime deadline = ComputeNextSeasonStart(state.currentDate, balance);
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.Renewal,
                madeAt = state.currentDate,
                deadline = deadline,
                status = PromiseStatus.Active,
            };
            state.activePromises.Add(promise);
            EventBus.Publish(new PromiseCreatedEvent { promiseId = promise.id });
            return promise;
        }

        public static Promise CreateTransferIn(
            GameState state,
            int playerId,
            int clubId,
            int positionId,
            int minCount,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            DateTime deadline = ComputeNextTransferWindowEnd(state.currentDate, balance);
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.TransferIn,
                madeAt = state.currentDate,
                deadline = deadline,
                status = PromiseStatus.Active,
            };
            promise.targets["clubId"] = clubId;
            promise.targets["positionId"] = positionId;
            promise.targets["minCount"] = minCount;
            state.activePromises.Add(promise);
            EventBus.Publish(new PromiseCreatedEvent { promiseId = promise.id });
            return promise;
        }

        public static Promise CreateTransferOut(
            GameState state,
            int playerId,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player = state.GetPlayer(playerId);
            int originalClubId = player?.currentClubId ?? -1;

            DateTime deadline = ComputeNextTransferWindowEnd(state.currentDate, balance);
            var promise = new Promise
            {
                id = state.nextPromiseId++,
                playerId = playerId,
                type = PromiseType.TransferOut,
                madeAt = state.currentDate,
                deadline = deadline,
                status = PromiseStatus.Active,
            };
            promise.targets["originalClubId"] = originalClubId;
            state.activePromises.Add(promise);
            EventBus.Publish(new PromiseCreatedEvent { promiseId = promise.id });
            return promise;
        }

        // ── Deadline 계산 ────────────────────────────────────────────

        private static DateTime ComputeNextSeasonEnd(DateTime now, GameBalanceSO balance)
        {
            var thisYearEnd = new DateTime(
                now.Year,
                balance.promisePlaytimeDeadlineMonth,
                balance.promisePlaytimeDeadlineDay
            );
            return now <= thisYearEnd ? thisYearEnd : thisYearEnd.AddYears(1);
        }

        private static DateTime ComputeNextSeasonStart(DateTime now, GameBalanceSO balance)
        {
            var thisYearStart = new DateTime(
                now.Year,
                balance.promiseRenewalDeadlineMonth,
                balance.promiseRenewalDeadlineDay
            );
            return now <= thisYearStart ? thisYearStart : thisYearStart.AddYears(1);
        }

        private static DateTime ComputeNextTransferWindowEnd(DateTime now, GameBalanceSO balance)
        {
            // 여름 (8/31) 또는 겨울 (1/31) 중 가까운 다음 종료일.
            var summerEnd = new DateTime(
                now.Year,
                balance.promiseTransferDeadlineMonthSummerEnd,
                balance.promiseTransferDeadlineDaysSummerEnd
            );
            var winterEnd = new DateTime(
                now.Year,
                balance.promiseTransferDeadlineMonthWinterEnd,
                balance.promiseTransferDeadlineDaysWinterEnd
            );
            if (now <= winterEnd)
                return winterEnd;
            if (now <= summerEnd)
                return summerEnd;
            // 둘 다 지났으면 다음 해 겨울
            return winterEnd.AddYears(1);
        }

        // ── 공용 ─────────────────────────────────────────────────────

        private static int GetTarget(Promise promise, string key, int defaultValue)
        {
            if (promise.targets == null)
                return defaultValue;
            return promise.targets.TryGetValue(key, out var v) ? v : defaultValue;
        }
    }
}
