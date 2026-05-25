// YouthSystem.cs
// algorithms.md #4 Youth Pool Generation 구현 + Reroll + Sign.
// V0.1: PA 진실값 / CA derived 역방향 모델. PlayerGenerator 부분 재활용 (stats/트레잇/인적사항)
//       + YouthSystem 이 PA/CA 덮어쓰기 (CA-Stats 분리 운영, design-decisions.md #24).
// 시드 = state.randomSeed ^ currentDate.Ticks ^ userActionHash ^ club.id ^ intake.id ^ rerollsUsed
//        (외부 마이닝 + 직플 영상 공유 둘 다 방어, design-decisions.md #35).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Utils;
using UnityEngine;
using Random = System.Random;

namespace FMLite.Application
{
    public static class YouthSystem
    {
        // ── GenerateIntake (Task 10.1, #39) ──────────────────────────

        public static YouthIntake GenerateIntake(
            Club club,
            GameState state,
            GameBalanceSO balance,
            LeagueConfigSO leagueConfig
        )
        {
            if (club == null)
                throw new ArgumentNullException(nameof(club));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (leagueConfig == null)
                throw new ArgumentNullException(nameof(leagueConfig));

            // 4단계: YouthIntake 빌드 (id 발급 우선 — 시드에 들어가야 함)
            var intake = new YouthIntake
            {
                id = state.nextIntakeId++,
                clubId = club.id,
                intakeDate = state.currentDate,
                candidatePlayerIds = new List<int>(),
                signedPlayerIds = new List<int>(),
                rejectedPlayerIds = new List<int>(),
                rerollsUsed = 0,
            };

            // 1~3단계: 시드 + 풀 사이즈 + 후보 생성
            PopulateCandidates(intake, club, state, balance, leagueConfig);

            club.intakeHistory.Add(intake);

            EventBus.Publish(
                new YouthIntakeAvailableEvent { intakeId = intake.id, clubId = club.id }
            );

            return intake;
        }

        // ── UseRerollToken (Task 10.2, #40) ──────────────────────────

        public static void UseRerollToken(
            YouthIntake intake,
            Club club,
            GameState state,
            GameBalanceSO balance,
            LeagueConfigSO leagueConfig
        )
        {
            if (intake == null)
                throw new ArgumentNullException(nameof(intake));
            if (state.rerollTokens <= 0)
                throw new InvalidOperationException(
                    "UseRerollToken: state.rerollTokens 가 0 이하 — UI 가 버튼 비활성화로 차단해야 함"
                );

            state.rerollTokens -= 1;
            intake.rerollsUsed += 1;

            // 영입 안 된 기존 후보 제거 — signed 는 유지
            var toRemove = intake
                .candidatePlayerIds.Where(id => !intake.signedPlayerIds.Contains(id))
                .ToList();
            foreach (var id in toRemove)
                state.RemovePlayer(id);

            intake.candidatePlayerIds.Clear();
            intake.candidatePlayerIds.AddRange(intake.signedPlayerIds);

            // 새 풀 — rerollsUsed 가 +1 됐으므로 시드 자동 변경 (algorithms.md #4 1단계)
            PopulateCandidates(intake, club, state, balance, leagueConfig);

            EventBus.Publish(
                new YouthRerolledEvent
                {
                    intakeId = intake.id,
                    remainingTokens = state.rerollTokens,
                }
            );
        }

        // ── SignPlayers (Task 10.3, #41) ─────────────────────────────

        public static void SignPlayers(
            YouthIntake intake,
            IList<int> playerIds,
            Club club,
            GameState state
        )
        {
            if (intake == null)
                throw new ArgumentNullException(nameof(intake));
            if (playerIds == null)
                throw new ArgumentNullException(nameof(playerIds));
            if (club == null)
                throw new ArgumentNullException(nameof(club));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            // 영입
            var signedSet = new HashSet<int>();
            foreach (var id in playerIds)
            {
                if (!intake.candidatePlayerIds.Contains(id))
                {
                    Debug.LogWarning(
                        $"[YouthSystem] SignPlayers: id={id} 는 candidatePlayerIds 외 — 스킵"
                    );
                    continue;
                }
                var player = state.GetPlayer(id);
                if (player == null)
                {
                    Debug.LogWarning(
                        $"[YouthSystem] SignPlayers: id={id} GameState 에서 not found — 스킵"
                    );
                    continue;
                }
                player.currentClubId = club.id;
                club.youthSquadIds.Add(id);
                intake.signedPlayerIds.Add(id);
                signedSet.Add(id);
            }

            // 미영입 처리 — V0.1: 모두 GameState 제거 + rejectedPlayerIds 에 ID 만 보관
            foreach (var id in intake.candidatePlayerIds)
            {
                if (signedSet.Contains(id))
                    continue;
                state.RemovePlayer(id);
                intake.rejectedPlayerIds.Add(id);
            }

            intake.candidatePlayerIds.Clear();

            EventBus.Publish(
                new YouthSignedEvent { intakeId = intake.id, signedPlayerIds = signedSet.ToList() }
            );
        }

        // ── 내부 — 후보 생성 (Logic 1~3단계) ──────────────────────────

        private static void PopulateCandidates(
            YouthIntake intake,
            Club club,
            GameState state,
            GameBalanceSO balance,
            LeagueConfigSO leagueConfig
        )
        {
            // 1단계: 시드 고정 — 외부 마이닝 + 직플 영상 공유 방어
            int userActionHash = ComputeUserActionHash(state);
            int seed =
                state.randomSeed
                ^ unchecked((int)state.currentDate.Ticks)
                ^ userActionHash
                ^ club.id
                ^ intake.id
                ^ intake.rerollsUsed;
            var rng = new Random(seed);

            // 2단계: 풀 사이즈 = FacilityLevelSO(YouthCoach).youthPoolSize
            var facility = GameDatabase.GetFacilityLevel(
                FacilityType.YouthCoach,
                club.facilities.youthCoachLevel
            );
            if (facility == null)
            {
                Debug.LogWarning(
                    $"[YouthSystem] FacilityLevelSO(YouthCoach, lv={club.facilities.youthCoachLevel}) not found — Lv1 폴백"
                );
                facility = GameDatabase.GetFacilityLevel(FacilityType.YouthCoach, 1);
                if (facility == null)
                {
                    Debug.LogError(
                        "[YouthSystem] FacilityLevelSO(YouthCoach, lv=1) 도 없음 — intake 빈 풀 반환"
                    );
                    return;
                }
            }
            int poolSize = facility.youthPoolSize;

            // 3단계: 후보 N명 생성
            int nextId = state.nextPlayerId;
            for (int i = 0; i < poolSize; i++)
            {
                int age = SampleYouthAge(rng, balance);
                string nat = SampleYouthNationality(rng, leagueConfig.countryCode, balance);
                Position position = (Position)rng.Next(0, 14); // V0.1: 균등 랜덤 (14개 포지션)
                int pa = SampleYouthPA(rng, facility, balance);
                int ca = DeriveCaFromPa(rng, pa, age, balance);

                // PlayerGenerator 호출 — stats/트레잇/인적사항/계약 알고리즘 재활용
                // (rng 같음, 결정성 유지). CA/PA 는 PlayerGen 결과 무시하고 덮어쓰기.
                var player = PlayerGenerator.Generate(
                    rng,
                    club.reputation,
                    position,
                    age,
                    nat,
                    clubId: -1, // 미소속
                    youthClubId: club.id, // 인스펙션 출처
                    origin: PlayerOrigin.YouthIntake,
                    state.currentDate,
                    balance
                );

                player.id = nextId++;
                player.potentialAbility = pa; // 덮어쓰기 (PA 진실값)
                player.currentAbility = ca; // 덮어쓰기 (CA derived from PA)

                state.AddPlayer(player);
                intake.candidatePlayerIds.Add(player.id);
            }
            state.nextPlayerId = nextId;
        }

        private static int ComputeUserActionHash(GameState state)
        {
            if (state.userClubId < 0)
                return 0;
            var userClub = state.GetClub(state.userClubId);
            if (userClub == null)
                return 0;
            return userClub.finance.money
                ^ (userClub.seniorSquadIds.Count * 7919)
                ^ (userClub.youthSquadIds.Count * 9973)
                ^ (state.rerollTokens * 16007);
        }

        // ── 샘플링 헬퍼 ───────────────────────────────────────────────

        private static int SampleYouthPA(
            Random rng,
            FacilityLevelSO facility,
            GameBalanceSO balance
        )
        {
            bool isStar = rng.NextDouble() < balance.youthStarPickProbability;
            double mu = facility.youthAvgPA + (isStar ? balance.youthStarPaBonus : 0.0);
            double rawPA = rng.NextNormal(mu, balance.youthPaStdDev);
            return Math.Clamp((int)Math.Round(rawPA), balance.minPA, balance.maxPA);
        }

        private static int DeriveCaFromPa(Random rng, int pa, int age, GameBalanceSO balance)
        {
            int span = balance.paGapZeroAge - balance.youthIntakeMinAge;
            double ageBlend =
                span > 0
                    ? Math.Clamp((double)(age - balance.youthIntakeMinAge) / span, 0.0, 1.0)
                    : 0.0;
            double caGap = Lerp((double)balance.paGapMaxMean, 0.0, ageBlend);
            double rawCA = pa - rng.NextNormal(caGap, balance.youthPaGapStdDev);
            return Math.Clamp((int)Math.Round(rawCA), balance.minCA, pa);
        }

        private static int SampleYouthAge(Random rng, GameBalanceSO balance)
        {
            // youthIntakeAgeWeights 가중 추첨 (16, 17, 18 순)
            var ages = new int[] { 16, 17, 18 };
            var weights = balance.youthIntakeAgeWeights;
            if (weights == null || weights.Length != 3)
            {
                Debug.LogWarning("[YouthSystem] youthIntakeAgeWeights 가 [3] 아님 — 균등 폴백");
                return ages[rng.Next(3)];
            }
            double total = weights[0] + weights[1] + weights[2];
            if (total <= 0)
                return ages[rng.Next(3)];
            double threshold = rng.NextDouble() * total;
            double cumulative = 0;
            for (int i = 0; i < 3; i++)
            {
                cumulative += weights[i];
                if (cumulative >= threshold)
                    return ages[i];
            }
            return ages[2];
        }

        private static string SampleYouthNationality(
            Random rng,
            string leagueCountry,
            GameBalanceSO balance
        )
        {
            if (rng.NextDouble() < balance.youthPrimaryNationalityRatio)
                return leagueCountry;
            var others = GameDatabase.AllCountries.Where(c => c.code != leagueCountry).ToList();
            if (others.Count == 0)
                return leagueCountry;
            return others[rng.Next(others.Count)].code;
        }

        // ── Lerp (PlayerGen 동일) ─────────────────────────────────────

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
