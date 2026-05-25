// ClubGenerator.cs
// algorithms.md #5 Club Generation 3단계 구현.
// 호출자가 LeagueConfigSO + 시드를 넘기면 Clubs + Players 일괄 반환.
// id 할당은 ClubGenerator 가 (startClubId/startPlayerId 부터 단조증가).
// GameState 등록 / userClub 선정은 호출자(GameInitializer).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using FMLite.Utils;
using UnityEngine;
using Random = System.Random;

namespace FMLite.Application
{
    public static class ClubGenerator
    {
        public static ClubGenerationResult Generate(
            Random rng,
            LeagueConfigSO leagueConfig,
            GameBalanceSO balance,
            DateTime currentDate,
            int leagueId,
            int startClubId,
            int startPlayerId
        )
        {
            var result = new ClubGenerationResult
            {
                Clubs = new List<Club>(),
                Players = new List<Player>(),
            };

            int clubCount = leagueConfig.clubCount;
            if (clubCount <= 0)
            {
                Debug.LogWarning($"[ClubGenerator] clubCount = {clubCount} → 빈 결과 반환");
                return result;
            }

            // 1단계: 명성 분배
            int[] tierCounts = AllocateTierCounts(balance.tierClubRatios, clubCount);
            int[] ranks = AssignReputations(rng, tierCounts, balance);

            // 2단계: Club 인스턴스
            for (int i = 0; i < clubCount; i++)
                result.Clubs.Add(
                    BuildClub(
                        rng,
                        i,
                        ranks[i],
                        leagueConfig,
                        balance,
                        currentDate,
                        leagueId,
                        startClubId
                    )
                );

            // 3단계: 스쿼드 생성 (구단마다 분배표 동적 — FormationConfig 필수 + 랜덤 2자리)
            int nextPlayerId = startPlayerId;
            foreach (var club in result.Clubs)
            {
                var players = RegenerateSquad(
                    rng,
                    club,
                    leagueConfig,
                    balance,
                    currentDate,
                    nextPlayerId
                );
                foreach (var player in players)
                {
                    result.Players.Add(player);
                    club.seniorSquadIds.Add(player.id);
                }
                nextPlayerId += players.Count;
            }

            return result;
        }

        // 한 구단의 스쿼드 25명만 재생성 (algorithms.md #6 Reroll 정책 + ClubGen 3단계 공용).
        // 호출자가 club.seniorSquadIds 관리 (Clear / Add). 이 메서드는 순수 생성만.
        // StartingSquadGacha.RerollSquad 와 ClubGen.Generate 가 공용으로 사용.
        public static List<Player> RegenerateSquad(
            Random rng,
            Club club,
            LeagueConfigSO leagueConfig,
            GameBalanceSO balance,
            DateTime currentDate,
            int startPlayerId
        )
        {
            var players = new List<Player>();
            int nextId = startPlayerId;
            var composition = BuildSquadComposition(rng, balance);
            foreach (var (pos, count) in composition)
            {
                for (int j = 0; j < count; j++)
                {
                    var player = GeneratePlayer(
                        rng,
                        club,
                        pos,
                        leagueConfig.countryCode,
                        currentDate,
                        balance
                    );
                    player.id = nextId++;
                    players.Add(player);
                }
            }
            return players;
        }

        // ── 1단계: 명성 분배 ──────────────────────────────────────────

        private static int[] AllocateTierCounts(float[] ratios, int n)
        {
            // ratio × n 의 floor + fractional part 큰 순서대로 잔여 +1.
            // ratio 합 ≠ 1.0 에도 라운드 보정으로 흡수.
            int tierCount = ratios.Length;
            var raw = new double[tierCount];
            var counts = new int[tierCount];
            for (int i = 0; i < tierCount; i++)
            {
                raw[i] = ratios[i] * n;
                counts[i] = (int)Math.Floor(raw[i]);
            }

            int remainder = n - counts.Sum();
            if (remainder > 0)
            {
                var order = Enumerable
                    .Range(0, tierCount)
                    .OrderByDescending(i => raw[i] - counts[i])
                    .ToArray();
                for (int k = 0; k < remainder; k++)
                    counts[order[k]] += 1;
            }
            else if (remainder < 0)
            {
                // ratio 합 > 1.0 같은 비정상. 끝 티어부터 -1.
                for (int k = 0; k < -remainder; k++)
                {
                    int idx = counts.Length - 1 - k;
                    if (idx >= 0 && counts[idx] > 0)
                        counts[idx]--;
                }
            }
            return counts;
        }

        private static int[] AssignReputations(Random rng, int[] tierCounts, GameBalanceSO b)
        {
            var ranks = new List<int>();
            for (int t = 0; t < tierCounts.Length; t++)
            for (int k = 0; k < tierCounts[t]; k++)
                ranks.Add(rng.Next(b.tierRepMin[t], b.tierRepMax[t] + 1));
            return ranks.ToArray();
        }

        // ── 2단계: Club 인스턴스 ─────────────────────────────────────

        private static Club BuildClub(
            Random rng,
            int idx,
            int rep,
            LeagueConfigSO cfg,
            GameBalanceSO b,
            DateTime currentDate,
            int leagueId,
            int startClubId
        )
        {
            // Finance — base + repCoeff*rep + 15% σ 노이즈
            double moneyMu = b.financeBaseMoney + (double)b.financeRepCoeff * rep;
            double noiseSigma = moneyMu * b.financeNoiseSigma;
            int money = Math.Max(
                b.financeFloor,
                (int)Math.Round(moneyMu + rng.NextNormal(0, noiseSigma))
            );

            // Facilities — rep/20 + 노이즈, clamp [1, maxFacilityLevel]
            double repLv = rep / 20.0;
            int scoutLv = SampleFacilityLevel(rng, repLv, b);
            int trainLv = SampleFacilityLevel(rng, repLv, b);
            int youthCoachLv = SampleFacilityLevel(rng, repLv, b);
            int youthRecLv = SampleFacilityLevel(rng, repLv, b);
            int youthFacLv = SampleFacilityLevel(rng, repLv, b);
            int medLv = SampleFacilityLevel(rng, repLv, b);
            int stadLv = SampleFacilityLevel(rng, repLv, b);
            int gymLv = SampleFacilityLevel(rng, repLv, b);

            int foundYr = currentDate.Year - rng.Next(b.clubMinAgeYears, b.clubMaxAgeYears + 1);
            string name = (idx < cfg.clubNames.Count) ? cfg.clubNames[idx] : $"Club {idx + 1}";

            return new Club
            {
                id = startClubId + idx,
                name = name,
                foundedYear = foundYr,
                leagueId = leagueId,
                reputation = rep,
                finance = new Finance
                {
                    money = money,
                    debt = 0,
                    transferBudget = (int)Math.Round(money * b.transferBudgetRatio),
                    wageBudget = (int)Math.Round(money * b.wageBudgetRatio),
                },
                facilities = new Facilities
                {
                    scoutLevel = scoutLv,
                    trainingLevel = trainLv,
                    youthCoachLevel = youthCoachLv,
                    youthRecruitmentLevel = youthRecLv,
                    youthFacilityLevel = youthFacLv,
                    medicalLevel = medLv,
                    stadiumLevel = stadLv,
                    gymLevel = gymLv,
                },
                seniorSquadIds = new List<int>(),
                youthSquadIds = new List<int>(),
                intakeHistory = new List<YouthIntake>(),
                season = new SeasonState
                {
                    targetLeaguePosition = idx + 1, // 명성 순위 = 기본 목표
                    cupTarget = CupTarget.None,
                    boardConfidence = b.initialBoardConfidence,
                },
                isActiveSimulation = false, // GameInitializer 가 userClub 결정 후 갱신
            };
        }

        private static int SampleFacilityLevel(Random rng, double mu, GameBalanceSO b) =>
            Math.Clamp(
                (int)Math.Round(mu + rng.NextNormal(0, b.facilityNoiseSigma)),
                b.minFacilityLevel,
                b.maxFacilityLevel
            );

        // ── 3단계: 스쿼드 ──────────────────────────────────────────────

        // FormationConfig 기반 분배표 동적 생성 (design-decisions.md #28).
        // 필수 인원 (그룹 합 균등 분배) + randomSlots 시드 기반 추첨.
        // V0.1 4-4-2 기본: GK 3 + 23 필수 + 2 랜덤 = 25.
        // V1.0 에서 FormationSO 추출 시 이 메서드가 FormationConfig 입력만 받는 형태로 일관.
        private static List<(Position pos, int count)> BuildSquadComposition(
            Random rng,
            GameBalanceSO b
        )
        {
            var f = b.formation;
            var counts = new Dictionary<Position, int>
            {
                [Position.GK] = f.gk,
                [Position.CB] = f.cbMin,
                [Position.LB] = f.lbMin,
                [Position.RB] = f.rbMin,
                // 그룹 — 내부 균등 분배 (홀수면 두 번째 포지션이 +1)
                [Position.DM] = f.dmCmGroupMin / 2,
                [Position.CM] = f.dmCmGroupMin - (f.dmCmGroupMin / 2),
                [Position.LM] = f.lmLwGroupMin / 2,
                [Position.LW] = f.lmLwGroupMin - (f.lmLwGroupMin / 2),
                [Position.RM] = f.rmRwGroupMin / 2,
                [Position.RW] = f.rmRwGroupMin - (f.rmRwGroupMin / 2),
                [Position.ST] = f.stCfGroupMin / 2,
                [Position.CF] = f.stCfGroupMin - (f.stCfGroupMin / 2),
            };

            // 랜덤 자리 — 12개 필드 포지션 중 균등 추첨 (GK 제외 — 서드키퍼까지 이미 보장)
            var randomTargets = new[]
            {
                Position.CB,
                Position.LB,
                Position.RB,
                Position.DM,
                Position.CM,
                Position.LM,
                Position.LW,
                Position.RM,
                Position.RW,
                Position.ST,
                Position.CF,
            };
            for (int i = 0; i < f.randomSlots; i++)
            {
                var pick = randomTargets[rng.Next(randomTargets.Length)];
                counts[pick] = counts[pick] + 1;
            }

            // 결정적 순서로 변환 (Position enum 순서 따라). count == 0 은 제외.
            var ordered = new[]
            {
                Position.GK,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.DM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.LW,
                Position.RW,
                Position.ST,
                Position.CF,
            };
            var result = new List<(Position, int)>(ordered.Length);
            foreach (var p in ordered)
                if (counts.TryGetValue(p, out int c) && c > 0)
                    result.Add((p, c));
            return result;
        }

        private static Player GeneratePlayer(
            Random rng,
            Club club,
            Position pos,
            string leagueCountry,
            DateTime currentDate,
            GameBalanceSO b
        )
        {
            int age = SampleAge(rng, b);
            string nat = SampleNationality(rng, leagueCountry, b);
            bool homegrown = rng.NextDouble() < b.homegrownRatio;
            int youthClubId = homegrown ? club.id : -1;

            return PlayerGenerator.Generate(
                rng,
                club.reputation,
                pos,
                age,
                nat,
                club.id,
                youthClubId,
                PlayerOrigin.InitialRoster,
                currentDate,
                b
            );
        }

        private static int SampleAge(Random rng, GameBalanceSO b)
        {
            // 3 구간 (youth / prime / veteran) WeightedSample. WeightedSample 가 0/음수 weight 폴백 처리.
            var buckets = new[] { 0, 1, 2 };
            int bucket = rng.WeightedSample(
                buckets,
                i =>
                    i switch
                    {
                        0 => (double)b.youthAgeRatio,
                        1 => (double)b.primeAgeRatio,
                        2 => (double)b.veteranAgeRatio,
                        _ => 0.0,
                    }
            );
            return bucket switch
            {
                0 => rng.Next(b.youthAgeMin, b.youthAgeMax + 1),
                1 => rng.Next(b.primeAgeMin, b.primeAgeMax + 1),
                2 => rng.Next(b.veteranAgeMin, b.veteranAgeMax + 1),
                _ => b.primeAgeMin,
            };
        }

        private static string SampleNationality(Random rng, string leagueCountry, GameBalanceSO b)
        {
            if (rng.NextDouble() < b.primaryNationalityRatio)
                return leagueCountry;
            var others = GameDatabase.AllCountries.Where(c => c.code != leagueCountry).ToList();
            if (others.Count == 0)
                return leagueCountry; // 폴백
            return others[rng.Next(others.Count)].code;
        }
    }

    public class ClubGenerationResult
    {
        public List<Club> Clubs;
        public List<Player> Players;
    }
}
