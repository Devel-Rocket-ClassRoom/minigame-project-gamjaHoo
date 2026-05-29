// ScoutingSystem.cs
// V0.5 E.2 — 스카우트 명단 관리. Stateless (design-decisions.md #3 + #46).
// 매주 (월요일) DailyProcessor 가 UpdateKnowledge 호출.
//
// 책임:
//   1. 자기 구단 선수 자동 등록 (scoutLevel=100, 정확 estimate)
//   2. Scout 시설 등급에 따라 명단 확장 (자기 리그 외 클럽 선수 무작위)
//   3. 기존 명단의 scoutLevel 누적 (매주 +growthRate, max 100) + accuracy margin ↓
//
// 명세: design-decisions.md #46 / v0.5-plan.md §3.7.1-3.
// V0.5 단일 리그라 "자기 리그 vs 타 리그" 분기 단순화 — 자기 클럽 외 모든 클럽 = 후보.
//   다중 리그 V1.0 도입 시 분기 추가.

using System;
using System.Collections.Generic;
using FMLite.Core;
using FMLite.Domain;
using Random = System.Random;

namespace FMLite.Application
{
    public static class ScoutingSystem
    {
        public static void UpdateKnowledge(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var rng = new Random(
                state.randomSeed
                    ^ unchecked((int)state.currentDate.Ticks)
                    ^ state.currentDate.Day * 1000
            );

            foreach (var club in state.allClubs)
            {
                if (club.scoutingKnowledge == null)
                    club.scoutingKnowledge = new Dictionary<int, ScoutReport>();

                RegisterOwnSquad(club, state);
                ExpandScoutingPool(club, state, balance, rng);
                AccumulateKnowledge(club, state, balance);
            }
        }

        // 1. 자기 구단 senior + youth 자동 등록 — scoutLevel=100, 정확 estimate
        private static void RegisterOwnSquad(Club club, GameState state)
        {
            RegisterClubSquad(club, state, club.seniorSquadIds);
            RegisterClubSquad(club, state, club.youthSquadIds);
        }

        private static void RegisterClubSquad(Club club, GameState state, List<int> squadIds)
        {
            if (squadIds == null)
                return;
            foreach (var pid in squadIds)
            {
                var player = state.GetPlayer(pid);
                if (player == null)
                    continue;

                club.scoutingKnowledge[pid] = new ScoutReport
                {
                    playerId = pid,
                    scoutLevel = 100,
                    lastUpdated = state.currentDate,
                    caEstimate = new CaPaEstimate { estimate = player.currentAbility, margin = 0 },
                    paEstimate = new CaPaEstimate
                    {
                        estimate = player.potentialAbility,
                        margin = 0,
                    },
                };
            }
        }

        // 2. Scout 시설 등급 기반 명단 확장
        private static void ExpandScoutingPool(
            Club club,
            GameState state,
            GameBalanceSO balance,
            Random rng
        )
        {
            int scoutLevel = club.facilities?.scoutLevel ?? 1;
            var scoutSO = GameDatabase.GetFacilityLevel(FacilityType.Scout, scoutLevel);
            int targetPoolSize = scoutSO?.scoutingListSize ?? 50;
            int initialMargin = scoutSO?.caAccuracyMargin ?? 30;

            // 외부 명단 현재 크기 (자기 구단 제외)
            int currentExternalCount = CountExternalScouted(club, state);
            int toAdd = Math.Max(0, targetPoolSize - currentExternalCount);
            if (toAdd == 0)
                return;

            // 후보: 다른 클럽의 senior 선수, 아직 명단에 없는 선수
            var candidates = new List<int>();
            foreach (var otherClub in state.allClubs)
            {
                if (otherClub.id == club.id)
                    continue;
                foreach (var pid in otherClub.seniorSquadIds)
                {
                    if (!club.scoutingKnowledge.ContainsKey(pid))
                        candidates.Add(pid);
                }
            }
            if (candidates.Count == 0)
                return;

            Shuffle(candidates, rng);
            int actualAdd = Math.Min(toAdd, candidates.Count);

            for (int i = 0; i < actualAdd; i++)
            {
                int pid = candidates[i];
                var player = state.GetPlayer(pid);
                if (player == null)
                    continue;

                club.scoutingKnowledge[pid] = new ScoutReport
                {
                    playerId = pid,
                    scoutLevel = Math.Max(1, scoutLevel * 10), // Lv1=10, Lv10=100
                    lastUpdated = state.currentDate,
                    caEstimate = MakeEstimate(player.currentAbility, initialMargin, rng),
                    paEstimate = MakeEstimate(player.potentialAbility, initialMargin, rng),
                };
            }
        }

        // 3. 기존 외부 명단 누적 — scoutLevel ↑, accuracy margin ↓
        private static void AccumulateKnowledge(Club club, GameState state, GameBalanceSO balance)
        {
            int gain = balance.scoutWeeklyLevelGain > 0 ? balance.scoutWeeklyLevelGain : 5;

            // 키 collection 복사 후 순회 (foreach 중 mutation 회피)
            var pids = new List<int>(club.scoutingKnowledge.Keys);
            foreach (var pid in pids)
            {
                var report = club.scoutingKnowledge[pid];
                if (report.scoutLevel >= 100)
                    continue; // 정확 명단 — 자기 구단 또는 완전 누적

                var player = state.GetPlayer(pid);
                if (player == null)
                    continue;

                int newLevel = Math.Min(100, report.scoutLevel + gain);
                report.scoutLevel = newLevel;
                report.lastUpdated = state.currentDate;

                // accuracy margin 점점 줄음 — scoutLevel 100 도달 시 0
                int marginAfter = Math.Max(0, ((100 - newLevel) * 30) / 100);
                report.caEstimate = new CaPaEstimate
                {
                    estimate = ShiftToward(report.caEstimate.estimate, player.currentAbility, gain),
                    margin = marginAfter,
                };
                report.paEstimate = new CaPaEstimate
                {
                    estimate = ShiftToward(
                        report.paEstimate.estimate,
                        player.potentialAbility,
                        gain
                    ),
                    margin = marginAfter,
                };
            }
        }

        private static int CountExternalScouted(Club club, GameState state)
        {
            int count = 0;
            foreach (var kv in club.scoutingKnowledge)
            {
                var p = state.GetPlayer(kv.Key);
                if (p != null && p.currentClubId != club.id)
                    count++;
            }
            return count;
        }

        private static CaPaEstimate MakeEstimate(int actualValue, int margin, Random rng)
        {
            if (margin <= 0)
                return new CaPaEstimate { estimate = actualValue, margin = 0 };
            int noise = rng.Next(-margin, margin + 1);
            return new CaPaEstimate
            {
                estimate = Math.Max(1, Math.Min(200, actualValue + noise)),
                margin = margin,
            };
        }

        // 누적 시 estimate 가 실제 값에 가까워지도록 보정
        private static int ShiftToward(int current, int target, int step)
        {
            int diff = target - current;
            int shift = Math.Sign(diff) * Math.Min(Math.Abs(diff), step);
            return current + shift;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
