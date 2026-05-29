// MoraleSystem.cs
// V0.5 G.1 — 사기 (단기) + 행복도 (장기) 변동 트리거 일괄 적용.
// algorithms.md V0.5-6 / design-decisions.md #42.
// Stateless (design-decisions.md #3). state 입력받아 변경.
//
// 호출 시점:
//   - 매일  → DailyProcessor.Run 가 Tick(state, balance) 호출
//   - 매치 후 → MatchPostProcessor.Process 가 OnMatchFinished(state, result, balance) 호출
//   - 이적 체결 후 → TransferSystem.CompleteTransfer 가 OnTransferCompleted 호출 (Stage K)
//   - 계약 갱신 후 → TransferSystem.RenewContract 가 OnContractRenewed 호출 (Stage H.1)
//   - 약속 처리 후 → PromiseSystem.CheckProgress 가 OnPromiseFulfilled / OnPromiseBroken 호출 (G.2)
//   - 면담 후 → PlayerProfile UI 가 OnInterview 호출 (G.2 UI)
//
// G.1 스코프 한정:
//   - T1~T7 핸들러 + Hidden 보정 + Tick + MatchPostProcessor 통합
//   - T8 (라커룸 분위기 < 30 → 폼 -5) 는 G.3 (dressingRoomMood + MatchSimulator 입력)

using System;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class MoraleSystem
    {
        // ── Tick (매일) ────────────────────────────────────────────────

        public static void Tick(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            foreach (var player in state.allPlayers)
            {
                if (player.currentClubId == -1)
                    continue;
                if (player.state == null)
                    continue;
                RecoverMoraleDaily(player, balance);
            }

            // 매주 월요일 Promise 체크는 G.2 PromiseSystem.CheckProgress (DailyProcessor 직접 호출) 로 이관.

            // 매월 1일 — 라커룸 분위기 갱신 (V0.5 G.3)
            if (state.currentDate.Day == 1)
            {
                foreach (var club in state.allClubs)
                    UpdateDressingRoomMood(club, state, balance);
            }
        }

        // ── DressingRoomMood (V0.5 G.3) ──────────────────────────────

        // 1군 선수 happiness 평균 + 캡틴 leadership 가산. NewSeasonProcessor / 월 1회 호출.
        public static void UpdateDressingRoomMood(
            Club club,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (club?.season == null || club.seniorSquadIds == null)
                return;

            int sum = 0;
            int count = 0;
            foreach (var pid in club.seniorSquadIds)
            {
                var p = state.GetPlayer(pid);
                if (p?.state == null)
                    continue;
                sum += p.state.happiness;
                count++;
            }
            int avgHappiness = count == 0 ? 50 : sum / count;

            int captainBonus = 0;
            if (club.season.captainPlayerId != -1)
            {
                var captain = state.GetPlayer(club.season.captainPlayerId);
                if (captain?.stats?.mental != null)
                    captainBonus = (int)
                        Math.Round(
                            captain.stats.mental.leadership
                                * balance.dressingRoomCaptainLeadershipBonus
                        );
            }

            club.season.dressingRoomMood = Clamp(avgHappiness + captainBonus, 0, 100);
        }

        private static void RecoverMoraleDaily(Player player, GameBalanceSO balance)
        {
            const int target = 50;
            int diff = target - player.state.morale;
            if (diff == 0)
                return;

            int rawDelta =
                Math.Sign(diff) * Math.Min(Math.Abs(diff), balance.moraleDailyRecoveryRate);
            int delta = (int)Math.Round(ApplyProfessionalismFactor(rawDelta, player));
            // recovery 가 0 으로 절삭되면 절대 수렴 안 함 — 최소 1 보장 (방향 보존)
            if (delta == 0 && rawDelta != 0)
                delta = Math.Sign(rawDelta);
            player.state.morale = Clamp(player.state.morale + delta, 0, 100);
        }

        // ── OnMatchFinished ──────────────────────────────────────────

        public static void OnMatchFinished(
            GameState state,
            MatchResult result,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            bool homeWon = result.homeScore > result.awayScore;
            bool awayWon = result.awayScore > result.homeScore;
            bool isDraw = result.homeScore == result.awayScore;

            ApplyMatchMoraleToSide(state, result, result.homeStarting11, homeWon, isDraw, balance);
            ApplyMatchMoraleToSide(state, result, result.awayStarting11, awayWon, isDraw, balance);
        }

        private static void ApplyMatchMoraleToSide(
            GameState state,
            MatchResult result,
            System.Collections.Generic.List<int> starting11,
            bool isWinner,
            bool isDraw,
            GameBalanceSO balance
        )
        {
            if (starting11 == null)
                return;

            foreach (var pid in starting11)
            {
                var player = state.GetPlayer(pid);
                if (player?.state == null)
                    continue;

                int baseDelta;
                if (isWinner)
                    baseDelta = balance.moraleMatchWinBonus;
                else if (isDraw)
                    baseDelta = 0;
                else
                    baseDelta = -balance.moraleMatchLossPenalty;

                // 평점 보정 (PlayerMatchStat.rating)
                float rating = LookupRating(result, pid);
                if (rating >= balance.ratingHighThreshold)
                    baseDelta += balance.moraleHighRatingBonus;
                else if (rating > 0 && rating < balance.ratingLowThreshold)
                    baseDelta -= balance.moraleLowRatingPenalty;

                int delta = (int)Math.Round(ApplyProfessionalismFactor(baseDelta, player));
                player.state.morale = Clamp(player.state.morale + delta, 0, 100);
            }
        }

        private static float LookupRating(MatchResult result, int playerId)
        {
            if (result.playerStats == null)
                return 0f;
            foreach (var s in result.playerStats)
                if (s.playerId == playerId)
                    return s.rating;
            return 0f;
        }

        // ── OnTransferCompleted ──────────────────────────────────────

        public static void OnTransferCompleted(
            GameState state,
            TransferOffer offer,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (offer == null)
                throw new ArgumentNullException(nameof(offer));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player = state.GetPlayer(offer.playerId);
            if (player?.state == null)
                return;

            // 영입된 선수 환영 (spec 침묵 — 합리적 디폴트: morale + 환영 보너스, happiness 50 리셋)
            int delta = (int)
                Math.Round(
                    ApplyProfessionalismFactor(balance.transferCompletedIncomingMoraleBonus, player)
                );
            player.state.morale = Clamp(player.state.morale + delta, 0, 100);
            player.state.happiness = 50; // 새 환경 — 장기 만족도 리셋
        }

        // ── OnContractRenewed ────────────────────────────────────────

        public static void OnContractRenewed(GameState state, int playerId, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player = state.GetPlayer(playerId);
            if (player?.state == null)
                return;

            int moraleDelta = (int)
                Math.Round(ApplyProfessionalismFactor(balance.contractRenewalMoraleBoost, player));
            int happinessDelta = (int)
                Math.Round(ApplyHiddenLoyaltyFactor(balance.contractRenewalHappinessBoost, player));
            player.state.morale = Clamp(player.state.morale + moraleDelta, 0, 100);
            player.state.happiness = Clamp(player.state.happiness + happinessDelta, 0, 100);
        }

        // ── OnPromiseFulfilled / OnPromiseBroken ─────────────────────

        public static void OnPromiseFulfilled(
            GameState state,
            Promise promise,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (promise == null)
                throw new ArgumentNullException(nameof(promise));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player = state.GetPlayer(promise.playerId);
            if (player?.state == null)
                return;

            int delta = (int)
                Math.Round(
                    ApplyHiddenLoyaltyFactor(balance.promiseFulfilledHappinessBonus, player)
                );
            player.state.happiness = Clamp(player.state.happiness + delta, 0, 100);
        }

        public static void OnPromiseBroken(GameState state, Promise promise, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (promise == null)
                throw new ArgumentNullException(nameof(promise));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player = state.GetPlayer(promise.playerId);
            if (player?.state == null)
                return;

            int penalty = (int)
                Math.Round(ApplyHiddenLoyaltyFactor(balance.promiseBreakHappinessPenalty, player));
            player.state.happiness = Clamp(player.state.happiness - penalty, 0, 100);

            if (player.state.happiness < balance.transferRequestThreshold)
                EventBus.Publish(new TransferRequestEvent { playerId = player.id });
        }

        // ── OnInterview ──────────────────────────────────────────────

        public static void OnInterview(
            GameState state,
            int playerId,
            InterviewType type,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player = state.GetPlayer(playerId);
            if (player?.state == null)
                return;

            switch (type)
            {
                case InterviewType.Praise:
                {
                    int delta = (int)
                        Math.Round(
                            ApplyProfessionalismFactor(balance.interviewPraiseMoraleBonus, player)
                        );
                    player.state.morale = Clamp(player.state.morale + delta, 0, 100);
                    break;
                }
                case InterviewType.Criticize:
                {
                    int delta = (int)
                        Math.Round(
                            ApplyProfessionalismFactor(
                                balance.interviewCriticizeMoralePenalty,
                                player
                            )
                        );
                    player.state.morale = Clamp(player.state.morale - delta, 0, 100);
                    break;
                }
                case InterviewType.PromisePlaytime:
                    PromiseSystem.CreatePlaytimeAgreement(
                        state,
                        playerId,
                        balance.promisePlaytimeDefaultRatio,
                        balance
                    );
                    break;
                case InterviewType.PromiseRenewal:
                    PromiseSystem.CreateRenewal(state, playerId, balance);
                    break;
            }
        }

        // ── Hidden Attributes 헬퍼 ───────────────────────────────────

        // professionalism 높을수록 변동폭 ↓ (안정). 80 = ×0.91 / 50 = ×1.0 / 20 = ×1.09.
        // spec 의 "×0.7" 은 극단 (≥ 99) 시점. 선형 보간 ×0.3 폭으로 근사.
        private static float ApplyProfessionalismFactor(float delta, Player player)
        {
            int p = player?.hiddenAttrs?.professionalism ?? 50;
            float factor = 1.0f - (p - 50) / 100f * 0.3f;
            return delta * factor;
        }

        // loyalty 높을수록 부정 충격 완화. 80 = ×0.85 / 50 = ×1.0 / 20 = ×1.15.
        private static float ApplyHiddenLoyaltyFactor(float delta, Player player)
        {
            int l = player?.hiddenAttrs?.loyalty ?? 50;
            float factor = 1.0f - (l - 50) / 100f * 0.5f;
            return delta * factor;
        }

        // ── 공용 ─────────────────────────────────────────────────────

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
