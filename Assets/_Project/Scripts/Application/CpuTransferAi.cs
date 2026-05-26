// CpuTransferAi.cs
// V1.0 F.1 + F.2 — AI 구단 능동 영입 의사결정. Stateless (design-decisions.md #3 + #47).
// 매주 월요일 DailyProcessor 가 ScoutingSystem.UpdateKnowledge 다음 호출.
//
// 필요 기반 트리거 5종 (algorithms.md V1.0-5):
//   1. 약점 포지션 (최우선) — 4라인 평균 CA ratio < aiWeaknessRatioThreshold (0.95)
//   2. 부상자 발생 — 핵심 (CA 상위 70%) 가 aiCoreInjuryWeeksThreshold (4) 주+ 부상
//   3. 계약 잔여 6개월 — 핵심 FA 임박
//   4. 약속 미이행 위험 — 보드 영입 약속 임박 (V1.x 도메인 의존 — 안전 분기)
//   5. 명성 대비 자금 여유 — 자금 > clubReputation × aiSavingsThreshold
//
// 우선순위: 1 > 2 > 3 > 4 > 5. 같은 주 한 클럽 1 트리거만 (자금 분산 회피).
//
// 시드: state.randomSeed ^ club.id ^ currentDate.Ticks ^ trigger.type
// 결정성: 같은 시드 = 같은 트리거 / 같은 후보 선택.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using Random = System.Random;

namespace FMLite.Application
{
    public enum CpuTriggerType
    {
        None = 0,
        WeakLine = 1,
        CoreInjury = 2,
        FaImminent = 3,
        PromiseRisk = 4,
        SavingsHigh = 5,
    }

    public static class CpuTransferAi
    {
        public static void Run(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            foreach (var club in state.allClubs)
            {
                if (club.id == state.userClubId)
                    continue;
                if (club.facilities == null)
                    continue;

                var trigger = DetectTrigger(club, state, balance);
                if (trigger.type == CpuTriggerType.None)
                    continue;

                int seed =
                    state.randomSeed
                    ^ club.id
                    ^ unchecked((int)state.currentDate.Ticks)
                    ^ (int)trigger.type;
                var rng = new Random(seed);

                TryOfferForTrigger(club, trigger, state, balance, rng);
            }
        }

        // ── 트리거 식별 (우선순위 순서) ──────────────────────────────

        private struct Trigger
        {
            public CpuTriggerType type;
            public Line line;
            public Position position;
        }

        private static Trigger DetectTrigger(Club club, GameState state, GameBalanceSO balance)
        {
            // 1. 약점 포지션 (최우선)
            var weakLine = FindWeakLine(club, state, balance);
            if (weakLine.HasValue)
                return new Trigger
                {
                    type = CpuTriggerType.WeakLine,
                    line = weakLine.Value,
                    position = WeakestPositionInLine(weakLine.Value),
                };

            // 2. 부상자 발생 (도메인 부분 구현 — Stage I 매치 엔진 통합 시 본격 활성)
            var injuredCore = FindCoreInjury(club, state, balance);
            if (injuredCore != null)
                return new Trigger
                {
                    type = CpuTriggerType.CoreInjury,
                    line = StartingSquadGacha.LineOf(injuredCore.info.primaryPosition),
                    position = injuredCore.info.primaryPosition,
                };

            // 3. 계약 잔여 6개월 (핵심 FA 임박)
            var faImminent = FindFaImminentCore(club, state, balance);
            if (faImminent != null)
                return new Trigger
                {
                    type = CpuTriggerType.FaImminent,
                    line = StartingSquadGacha.LineOf(faImminent.info.primaryPosition),
                    position = faImminent.info.primaryPosition,
                };

            // 4. 보드 약속 — V1.x 의존 (boardPromises 빈 리스트 시 스킵)
            // (Club.season.boardPromises 가 Stage M.5 에서 도입 예정)

            // 5. 명성 대비 자금 여유
            if (
                club.finance != null
                && club.finance.money > club.reputation * balance.aiSavingsThreshold
            )
            {
                var anyLine = FindAnyWeakerLine(club, state, balance);
                return new Trigger
                {
                    type = CpuTriggerType.SavingsHigh,
                    line = anyLine,
                    position = WeakestPositionInLine(anyLine),
                };
            }

            return new Trigger { type = CpuTriggerType.None };
        }

        // ── 후보 선정 + 오퍼 제출 ────────────────────────────────────

        private static void TryOfferForTrigger(
            Club club,
            Trigger trigger,
            GameState state,
            GameBalanceSO balance,
            Random rng
        )
        {
            int budget = (int)((club.finance?.money ?? 0) * balance.aiBudgetRatio);
            if (budget <= 0)
                return;

            int lineAvg = LineAverageCa(club, state, trigger.line);

            // 후보: 스카우트 명단 + transferListed 선수 (공개 정보 — K.4 우선순위 ↑)
            var seenIds = new HashSet<int>(club.scoutingKnowledge?.Keys ?? Enumerable.Empty<int>());
            foreach (var p in state.allPlayers)
            {
                if (p?.state?.transferListed == true)
                    seenIds.Add(p.id);
            }

            var candidates = new List<Player>();
            foreach (var pid in seenIds)
            {
                var player = state.GetPlayer(pid);
                if (player == null)
                    continue;
                if (player.currentClubId == club.id)
                    continue;
                if (player.info == null)
                    continue;
                if (StartingSquadGacha.LineOf(player.info.primaryPosition) != trigger.line)
                    continue;
                if (player.currentAbility <= lineAvg)
                    continue;

                int mv = TransferSystem.CalculateMarketValue(player, state, balance);
                if (mv > budget)
                    continue;
                candidates.Add(player);
            }
            if (candidates.Count == 0)
                return;

            // CA 가중 추첨
            var target = WeightedSampleByCa(candidates, rng);
            int marketValue = TransferSystem.CalculateMarketValue(target, state, balance);
            float multiplier =
                balance.aiOfferAmountRandomMin
                + (float)rng.NextDouble()
                    * (balance.aiOfferAmountRandomMax - balance.aiOfferAmountRandomMin);
            int amount = (int)(marketValue * multiplier);

            var proposed = ProposeContract(target, state);

            try
            {
                TransferSystem.SubmitOffer(
                    target.id,
                    target.currentClubId,
                    club.id,
                    amount,
                    proposed,
                    state,
                    balance
                );
            }
            catch
            {
                // SubmitOffer 가 ArgumentException 던질 수 있음 (예: 자기 클럽 / 잘못된 인자). 안전 스킵.
            }
        }

        // ── 트리거 헬퍼 ──────────────────────────────────────────────

        private static Line? FindWeakLine(Club club, GameState state, GameBalanceSO balance)
        {
            foreach (Line line in new[] { Line.GK, Line.DF, Line.MF, Line.AT })
            {
                int avg = LineAverageCa(club, state, line);
                int expected = ExpectedMeanCa(club, balance);
                if (expected <= 0)
                    continue;
                float ratio = (float)avg / expected;
                if (ratio < balance.aiWeaknessRatioThreshold)
                    return line;
            }
            return null;
        }

        private static Player FindCoreInjury(Club club, GameState state, GameBalanceSO balance)
        {
            int threshold = balance.aiCoreInjuryWeeksThreshold * 7;
            int topQuantileCa = ClubTopCaQuantile(club, state, 0.70f);

            foreach (var pid in club.seniorSquadIds)
            {
                var player = state.GetPlayer(pid);
                if (player?.state?.injury == null)
                    continue;
                if (player.state.injury.injuryTypeId == -1)
                    continue;
                if (player.currentAbility < topQuantileCa)
                    continue;

                int remainingDays = (int)
                    (player.state.injury.expectedReturn - state.currentDate).TotalDays;
                if (remainingDays >= threshold)
                    return player;
            }
            return null;
        }

        private static Player FindFaImminentCore(Club club, GameState state, GameBalanceSO balance)
        {
            int topQuantileCa = ClubTopCaQuantile(club, state, 0.70f);

            foreach (var pid in club.seniorSquadIds)
            {
                var player = state.GetPlayer(pid);
                if (player?.contract == null)
                    continue;
                if (player.currentAbility < topQuantileCa)
                    continue;

                int days = (int)(player.contract.endDate - state.currentDate).TotalDays;
                if (days > 0 && days <= balance.aiContractFaThresholdDays)
                    return player;
            }
            return null;
        }

        private static Line FindAnyWeakerLine(Club club, GameState state, GameBalanceSO balance)
        {
            Line weakest = Line.GK;
            float weakestRatio = float.MaxValue;
            int expected = ExpectedMeanCa(club, balance);
            if (expected <= 0)
                return weakest;

            foreach (Line line in new[] { Line.GK, Line.DF, Line.MF, Line.AT })
            {
                int avg = LineAverageCa(club, state, line);
                float ratio = (float)avg / expected;
                if (ratio < weakestRatio)
                {
                    weakestRatio = ratio;
                    weakest = line;
                }
            }
            return weakest;
        }

        // ── 공용 헬퍼 ────────────────────────────────────────────────

        private static int LineAverageCa(Club club, GameState state, Line line)
        {
            int sum = 0;
            int count = 0;
            foreach (var pid in club.seniorSquadIds)
            {
                var player = state.GetPlayer(pid);
                if (player?.info == null)
                    continue;
                if (StartingSquadGacha.LineOf(player.info.primaryPosition) != line)
                    continue;
                sum += player.currentAbility;
                count++;
            }
            return count == 0 ? 0 : sum / count;
        }

        private static int ExpectedMeanCa(Club club, GameBalanceSO balance)
        {
            // ClubGen 패턴 — base + coeff × reputation
            return balance.caRepBase + (int)(balance.caRepCoeff * club.reputation);
        }

        private static int ClubTopCaQuantile(Club club, GameState state, float quantile)
        {
            var values = new List<int>();
            foreach (var pid in club.seniorSquadIds)
            {
                var p = state.GetPlayer(pid);
                if (p != null)
                    values.Add(p.currentAbility);
            }
            if (values.Count == 0)
                return 0;
            values.Sort();
            int idx = (int)(values.Count * quantile);
            idx = Math.Clamp(idx, 0, values.Count - 1);
            return values[idx];
        }

        private static Position WeakestPositionInLine(Line line) =>
            line switch
            {
                Line.GK => Position.GK,
                Line.DF => Position.CB,
                Line.MF => Position.CM,
                Line.AT => Position.ST,
                _ => Position.CM,
            };

        private static Player WeightedSampleByCa(List<Player> candidates, Random rng)
        {
            long totalWeight = 0;
            foreach (var p in candidates)
                totalWeight += Math.Max(1, p.currentAbility);

            long roll = (long)(rng.NextDouble() * totalWeight);
            long cumulative = 0;
            foreach (var p in candidates)
            {
                cumulative += Math.Max(1, p.currentAbility);
                if (roll < cumulative)
                    return p;
            }
            return candidates[candidates.Count - 1];
        }

        private static Contract ProposeContract(Player target, GameState state)
        {
            // V1.0 단순화 — 4년 계약 / 기존 주급 ×1.20 / release X
            var oldContract = target.contract;
            int newWage = oldContract != null ? (int)(oldContract.weeklyWage * 1.20) : 1000;
            return new Contract
            {
                weeklyWage = newWage,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(4),
                releaseClause = 0,
            };
        }
    }
}
