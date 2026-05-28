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
            GameState state,
            GameBalanceSO balance = null
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

            // 시설 등급 기반 maxSign 검증 (algorithms.md V1.0-4)
            var recruitFacility = GameDatabase.GetFacilityLevel(
                FacilityType.YouthRecruitment,
                club.facilities.youthRecruitmentLevel
            );
            if (recruitFacility != null && recruitFacility.signRatio < 1.0f)
            {
                int poolSize = intake.candidatePlayerIds.Count;
                int maxSign = Math.Max(1, (int)Math.Round(poolSize * (double)recruitFacility.signRatio));
                if (playerIds.Count > maxSign)
                    throw new ArgumentException(
                        $"SignPlayers: 시설 등급 초과 — maxSign={maxSign}, requested={playerIds.Count}"
                    );
            }

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

            // 미영입 처리 — V1.0 L.6: 일부 AI 다른 구단 영입, 나머지 제거 + ID 보존
            float aiRatio = balance?.youthRejectedToOtherClubRatio ?? 0f;
            var otherClubs = state.allClubs.Where(c => c.id != club.id).ToList();
            var rng = new Random(state.randomSeed ^ intake.id ^ 0xFEED);

            foreach (var id in intake.candidatePlayerIds)
            {
                if (signedSet.Contains(id))
                    continue;

                if (aiRatio > 0f && otherClubs.Count > 0 && rng.NextDouble() < aiRatio)
                {
                    var otherClub = otherClubs[rng.Next(otherClubs.Count)];
                    var player = state.GetPlayer(id);
                    if (player != null)
                    {
                        player.currentClubId = otherClub.id;
                        otherClub.youthSquadIds.Add(id);
                    }
                    intake.rejectedPlayerIds.Add(id);
                    EventBus.Publish(new YouthSignedByOtherEvent { playerId = id, otherClubId = otherClub.id });
                }
                else
                {
                    state.RemovePlayer(id);
                    intake.rejectedPlayerIds.Add(id);
                }
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

            // 2단계: 풀 사이즈 — YouthRecruitment (fallback Youth)
            var recruitFacility =
                GameDatabase.GetFacilityLevel(
                    FacilityType.YouthRecruitment,
                    club.facilities.youthRecruitmentLevel
                ) ?? GameDatabase.GetFacilityLevel(FacilityType.Youth, club.facilities.youthRecruitmentLevel);
            if (recruitFacility == null)
            {
                Debug.LogWarning(
                    $"[YouthSystem] YouthRecruitment/Youth lv={club.facilities.youthRecruitmentLevel} not found — Lv1 폴백"
                );
                recruitFacility =
                    GameDatabase.GetFacilityLevel(FacilityType.YouthRecruitment, 1)
                    ?? GameDatabase.GetFacilityLevel(FacilityType.Youth, 1);
                if (recruitFacility == null)
                {
                    Debug.LogError(
                        "[YouthSystem] YouthRecruitment/Youth Lv1 도 없음 — intake 빈 풀 반환"
                    );
                    return;
                }
            }
            int poolSize = recruitFacility.youthPoolSize;

            // YouthCoach 시설 — PA + 트레잇 (fallback Youth)
            var coachFacility =
                GameDatabase.GetFacilityLevel(
                    FacilityType.YouthCoach,
                    club.facilities.youthCoachLevel
                ) ?? GameDatabase.GetFacilityLevel(FacilityType.Youth, club.facilities.youthCoachLevel);
            if (coachFacility == null)
            {
                Debug.LogWarning(
                    $"[YouthSystem] YouthCoach/Youth lv={club.facilities.youthCoachLevel} not found — Lv1 폴백"
                );
                coachFacility =
                    GameDatabase.GetFacilityLevel(FacilityType.YouthCoach, 1)
                    ?? GameDatabase.GetFacilityLevel(FacilityType.Youth, 1);
            }

            // 3단계: 후보 N명 생성 (V1.0 L.7: 인스펙션별 포지션 가중치)
            var allPositions = (Position[])Enum.GetValues(typeof(Position));
            double[] posWeights = SamplePositionWeights(rng, balance?.youthPositionWeightVolatility ?? 0f);
            int nextId = state.nextPlayerId;
            for (int i = 0; i < poolSize; i++)
            {
                int age = SampleYouthAge(rng, balance);
                string nat = SampleYouthNationality(rng, leagueConfig.countryCode, balance);
                Position position = rng.WeightedSample(allPositions, p => posWeights[(int)p]);
                int pa = SampleYouthPA(rng, coachFacility, balance);
                int ca = DeriveCaFromPa(rng, pa, balance);

                // PlayerGenerator 호출 — stats/트레잇/인적사항/계약 알고리즘 재활용
                // (rng 같음, 결정성 유지). CA/PA 는 PlayerGen 결과 무시하고 덮어쓰기.
                var player = PlayerGenerator.Generate(
                    rng,
                    club.reputation,
                    position,
                    age,
                    nat,
                    clubId: -1,
                    youthClubId: club.id,
                    origin: PlayerOrigin.YouthIntake,
                    state.currentDate,
                    balance,
                    forceCa: ca,
                    forcePa: pa
                );

                player.id = nextId++;

                // V1.0 L.3: YouthCoach 가산 트레잇
                if (coachFacility != null && coachFacility.traitGrantChance > 0
                    && rng.NextDouble() < coachFacility.traitGrantChance)
                    TryGrantExtraTrait(player, rng);

                state.AddPlayer(player);
                intake.candidatePlayerIds.Add(player.id);
            }
            state.nextPlayerId = nextId;
        }

        private static void TryGrantExtraTrait(Player player, Random rng)
        {
            var usedGroups = new HashSet<int>();
            foreach (var tid in player.traitIds)
            {
                var t = GameDatabase.GetTrait(tid);
                if (t != null && t.exclusionGroupId != 0)
                    usedGroups.Add(t.exclusionGroupId);
            }
            var pool = GameDatabase
                .AllTraits.Where(t =>
                    !player.traitIds.Contains(t.id)
                    && (t.exclusionGroupId == 0 || !usedGroups.Contains(t.exclusionGroupId))
                )
                .ToList();
            if (pool.Count == 0)
                return;
            var chosen = rng.WeightedSample(pool, t => (double)t.weight);
            player.traitIds.Add(chosen.id);
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
            FacilityLevelSO coachFacility,
            GameBalanceSO balance
        )
        {
            bool isStar = rng.NextDouble() < balance.youthStarPickProbability;
            int paBonus = coachFacility?.youthAvgPABonus ?? 0;
            double mu =
                balance.youthBaseAvgPA
                + paBonus
                + (isStar ? balance.youthStarPaBonus : 0.0);
            double rawPA = rng.NextNormal(mu, balance.youthPaStdDev);
            return Math.Clamp((int)Math.Round(rawPA), balance.minPA, balance.maxPA);
        }

        // V1.0: 고정 갭 모델 + CA 캡 (algorithms.md V1.0-4).
        private static int DeriveCaFromPa(Random rng, int pa, GameBalanceSO balance)
        {
            double gap = Math.Max(0, rng.NextNormal(balance.youthCaGapMean, balance.youthPaGapStdDev));
            double rawCA = pa - gap;
            return Math.Clamp((int)Math.Round(rawCA), balance.youthMinCa, balance.youthMaxCa);
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

        // ── CheckPromotionCandidates (Task L.5, #235) ────────────────

        public static void CheckPromotionCandidates(GameState state, GameBalanceSO balance)
        {
            foreach (var club in state.allClubs)
            {
                var pending = club.season.pendingPromotionPlayerIds;
                float clubAvgCa = ComputeSeniorAvgCa(club, state);

                foreach (var pid in club.youthSquadIds.ToList())
                {
                    var player = state.GetPlayer(pid);
                    if (player == null)
                        continue;
                    int age = ComputeAge(player.info.birthDate, state.currentDate);
                    if (age < balance.youthPromotionAge)
                        continue;
                    if (player.currentAbility < clubAvgCa * balance.youthPromotionCaRatio)
                        continue;
                    if (pending.Contains(pid))
                        continue;

                    pending.Add(pid);
                    EventBus.Publish(new YouthPromotionSuggestedEvent { playerId = pid, clubId = club.id });
                }
            }
        }

        public static void PromotePlayer(int playerId, GameState state)
        {
            var player = state.GetPlayer(playerId);
            if (player == null)
                throw new ArgumentException($"PromotePlayer: player {playerId} not found");

            var club = state.GetClub(player.currentClubId);
            if (club == null)
                throw new InvalidOperationException($"PromotePlayer: club {player.currentClubId} not found");

            club.youthSquadIds.Remove(playerId);
            if (!club.seniorSquadIds.Contains(playerId))
                club.seniorSquadIds.Add(playerId);
            club.season.pendingPromotionPlayerIds.Remove(playerId);
        }

        public static void DeclinePromotion(int playerId, GameState state)
        {
            var player = state.GetPlayer(playerId);
            if (player == null)
                return;
            var club = state.GetClub(player.currentClubId);
            club?.season.pendingPromotionPlayerIds.Remove(playerId);
        }

        private static float ComputeSeniorAvgCa(Club club, GameState state)
        {
            if (club.seniorSquadIds.Count == 0)
                return 0f;
            float sum = 0f;
            int count = 0;
            foreach (var id in club.seniorSquadIds)
            {
                var p = state.GetPlayer(id);
                if (p == null)
                    continue;
                sum += p.currentAbility;
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        private static int ComputeAge(DateTime birthDate, DateTime today)
        {
            int age = today.Year - birthDate.Year;
            if (today.Month < birthDate.Month || (today.Month == birthDate.Month && today.Day < birthDate.Day))
                age--;
            return age;
        }

        // ── SamplePositionWeights (L.7) ──────────────────────────────
        // volatility=0 → 균등, volatility=1 → 극단 편중. 인스펙션 단위로 1회 호출.
        public static double[] SamplePositionWeights(Random rng, float volatility)
        {
            int n = 14;
            var weights = new double[n];
            double sum = 0;
            double uniform = 1.0 / n;
            for (int i = 0; i < n; i++)
            {
                weights[i] = uniform * (1.0 - volatility) + rng.NextDouble() * volatility;
                sum += weights[i];
            }
            for (int i = 0; i < n; i++)
                weights[i] /= sum;
            return weights;
        }

        // ── Lerp (PlayerGen 동일) ─────────────────────────────────────

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
