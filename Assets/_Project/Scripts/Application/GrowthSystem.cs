// GrowthSystem.cs
// V0.5 D.4 Sub-B — Player Growth System (algorithms.md V0.5-10).
// Stateless 시스템 (design-decisions.md #3) — state 입력받아 변경.
// 매월 1일 DailyProcessor.Run 이 Tick 호출.
//
// 모델: 2단계 (발생 확률 + size 추첨)
//   - 발생: growthBaseChance × ageFactor × absoluteFactor × trainingBonus × gymBonus(피지컬) × paFactor
//   - size: [+1, +2, +3] 분포 [75, 20, 5] / peak youth [60, 30, 10]
// CA-PA gap = 캡 (CA == PA 시 성장 X, decline 만 가능).
// Absolute stat (10) = ×0.10 페널티. Gym 보정은 Physical stat 8 만.
// 결정성 시드 = state.randomSeed ^ player.id ^ (year × 12 + month).

using System;
using System.Reflection;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Utils;
using Random = System.Random;

namespace FMLite.Application
{
    public static class GrowthSystem
    {
        // 매월 1일 호출. DailyProcessor 의 Day == 1 분기.
        public static void Tick(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            int year = state.currentDate.Year;
            int month = state.currentDate.Month;

            foreach (var club in state.allClubs)
            {
                if (club.facilities == null)
                    continue;
                int trainingLevel = club.facilities.trainingLevel;
                int gymLevel = club.facilities.gymLevel;

                if (club.seniorSquadIds == null)
                    continue;

                foreach (var pid in club.seniorSquadIds)
                {
                    var player = state.GetPlayer(pid);
                    if (player?.stats == null || player.info == null)
                        continue;

                    // V1.0 #68: 성장 처리 전 스냅샷 저장
                    player.growthHistory.Add(
                        new StatSnapshot
                        {
                            year = year,
                            month = month,
                            stats = player.stats.Clone(),
                        }
                    );

                    int seed = state.randomSeed ^ player.id ^ (year * 12 + month);
                    var rng = new Random(seed);

                    int age = ComputeAge(player.info.birthDate, state.currentDate);
                    float ageFactor = ComputeAgeFactor(age, balance);
                    int paGap = player.potentialAbility - player.currentAbility;
                    float paFactor =
                        (paGap > 0) ? Math.Min(1f, paGap / balance.growthPaGapNormalizer) : 0f;

                    ProcessCategory(
                        player,
                        player.stats.technical,
                        rng,
                        ageFactor,
                        paFactor,
                        trainingLevel,
                        gymLevel,
                        balance
                    );
                    ProcessCategory(
                        player,
                        player.stats.mental,
                        rng,
                        ageFactor,
                        paFactor,
                        trainingLevel,
                        gymLevel,
                        balance
                    );
                    ProcessCategory(
                        player,
                        player.stats.physical,
                        rng,
                        ageFactor,
                        paFactor,
                        trainingLevel,
                        gymLevel,
                        balance
                    );
                    ProcessCategory(
                        player,
                        player.stats.gk,
                        rng,
                        ageFactor,
                        paFactor,
                        trainingLevel,
                        gymLevel,
                        balance
                    );
                }

                // L.3: 유스 스쿼드 — YouthFacility.youthGrowthRate 적용
                if (club.youthSquadIds == null)
                    continue;

                var youthFacility =
                    GameDatabase.GetFacilityLevel(
                        FacilityType.YouthFacility,
                        club.facilities.youthFacilityLevel
                    )
                    ?? GameDatabase.GetFacilityLevel(
                        FacilityType.Youth,
                        club.facilities.youthFacilityLevel
                    );
                float youthGrowthRate = youthFacility?.youthGrowthRate ?? 1f;

                foreach (var pid in club.youthSquadIds)
                {
                    var player = state.GetPlayer(pid);
                    if (player?.stats == null || player.info == null)
                        continue;

                    // V1.0 #68: 성장 처리 전 스냅샷 저장
                    player.growthHistory.Add(
                        new StatSnapshot
                        {
                            year = year,
                            month = month,
                            stats = player.stats.Clone(),
                        }
                    );

                    int seed = state.randomSeed ^ player.id ^ (year * 12 + month);
                    var rng = new Random(seed);

                    int age = ComputeAge(player.info.birthDate, state.currentDate);
                    float ageFactor = ComputeAgeFactor(age, balance);
                    int paGap = player.potentialAbility - player.currentAbility;
                    float paFactor =
                        (paGap > 0) ? Math.Min(1f, paGap / balance.growthPaGapNormalizer) : 0f;

                    ProcessCategory(
                        player,
                        player.stats.technical,
                        rng,
                        ageFactor,
                        paFactor,
                        0,
                        0,
                        balance,
                        youthGrowthRate
                    );
                    ProcessCategory(
                        player,
                        player.stats.mental,
                        rng,
                        ageFactor,
                        paFactor,
                        0,
                        0,
                        balance,
                        youthGrowthRate
                    );
                    ProcessCategory(
                        player,
                        player.stats.physical,
                        rng,
                        ageFactor,
                        paFactor,
                        0,
                        0,
                        balance,
                        youthGrowthRate
                    );
                    ProcessCategory(
                        player,
                        player.stats.gk,
                        rng,
                        ageFactor,
                        paFactor,
                        0,
                        0,
                        balance,
                        youthGrowthRate
                    );
                }
            }
        }

        private static void ProcessCategory(
            Player player,
            object category,
            Random rng,
            float ageFactor,
            float paFactor,
            int trainingLevel,
            int gymLevel,
            GameBalanceSO balance,
            float facilityGrowthRate = 1f
        )
        {
            if (category == null)
                return;

            // 결정성 — reflection 필드 순회는 declared order (CLR spec 보장 X) 라 name 으로 정렬.
            var fields = category.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(int))
                    continue;

                string statName = field.Name;
                int value = (int)field.GetValue(category);

                bool isAbsolute = StatMetadata.IsAbsolute(statName);
                bool isPhysical = StatMetadata.IsPhysical(statName);

                float absoluteFactor = isAbsolute ? balance.growthAbsoluteFactor : 1f;
                float trainingBonus = 1f + trainingLevel * balance.growthTrainingCoeff;
                float gymBonus = isPhysical ? 1f + gymLevel * balance.growthGymCoeff : 1f;

                if (ageFactor < 0)
                {
                    // decline — paFactor 무관
                    float declineChance =
                        Math.Abs(ageFactor) * balance.growthBaseChance * absoluteFactor;
                    if (rng.NextDouble() < declineChance)
                    {
                        int size = SampleGrowthSize(rng, Math.Abs(ageFactor), balance);
                        int newValue = Math.Max(1, value - size);
                        field.SetValue(category, newValue);
                        if (size >= 2)
                            EventBus.Publish(
                                new PlayerStatChangedEvent
                                {
                                    playerId = player.id,
                                    statName = statName,
                                    oldValue = value,
                                    newValue = newValue,
                                }
                            );
                    }
                }
                else
                {
                    float growthChance =
                        balance.growthBaseChance
                        * ageFactor
                        * absoluteFactor
                        * trainingBonus
                        * gymBonus
                        * paFactor
                        * facilityGrowthRate;
                    if (rng.NextDouble() < growthChance)
                    {
                        int size = SampleGrowthSize(rng, ageFactor, balance);
                        int newValue = Math.Min(100, value + size);
                        field.SetValue(category, newValue);
                        if (size >= 2)
                            EventBus.Publish(
                                new PlayerStatChangedEvent
                                {
                                    playerId = player.id,
                                    statName = statName,
                                    oldValue = value,
                                    newValue = newValue,
                                }
                            );
                    }
                }
            }
        }

        // 16-22세 peak / 23-26세 prime / 27-30세 정체 / 31세+ decline
        public static float ComputeAgeFactor(int age, GameBalanceSO balance)
        {
            if (age <= balance.growthYouthPeakAge)
                return balance.growthYouthFactor - (age - 16) * 0.1f;
            if (age <= balance.growthPrimePeakAge)
                return 1f - (age - balance.growthYouthPeakAge) * 0.1f;
            if (age <= balance.growthDeclineStartAge)
                return 0f;
            return -Math.Min((age - balance.growthDeclineStartAge) * 0.2f, 1f);
        }

        // [+1, +2, +3] 추첨. peak youth (ageFactor ≥ 1.3) = 큰 점프 ↑.
        private static int SampleGrowthSize(Random rng, float ageFactor, GameBalanceSO balance)
        {
            var weights =
                ageFactor >= balance.growthBigJumpAgeThreshold
                    ? balance.growthSizePeakWeights
                    : balance.growthSizeWeights;
            return WeightedSample(rng, weights);
        }

        private static int WeightedSample(Random rng, float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];
            float roll = (float)(rng.NextDouble() * total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                    return i + 1; // size = 1, 2, 3
            }
            return weights.Length; // safety
        }

        private static int ComputeAge(DateTime birthDate, DateTime today)
        {
            int age = today.Year - birthDate.Year;
            if (
                today.Month < birthDate.Month
                || (today.Month == birthDate.Month && today.Day < birthDate.Day)
            )
                age--;
            return age;
        }

        // ── V1.0 #68: 직전 N개월 stat 변화량 헬퍼 ────────────────────
        // statFieldPath = "technical.passing" / "mental.vision" / "physical.pace" / "gk.reflexes"
        // growthHistory 가 monthsBack 미만이면 0 반환.
        public static int GetStatChange(Player player, string statFieldPath, int monthsBack = 3)
        {
            if (player?.growthHistory == null || player.growthHistory.Count < monthsBack)
                return 0;

            var oldSnapshot = player.growthHistory[player.growthHistory.Count - monthsBack];
            int oldVal = ReadStatField(oldSnapshot.stats, statFieldPath);
            int curVal = ReadStatField(player.stats, statFieldPath);
            return curVal - oldVal;
        }

        private static int ReadStatField(Stats stats, string fieldPath)
        {
            if (stats == null)
                return 0;

            var parts = fieldPath.Split('.');
            if (parts.Length != 2)
                return 0;

            object sub = parts[0] switch
            {
                "technical" => stats.technical,
                "mental" => stats.mental,
                "physical" => stats.physical,
                "gk" => stats.gk,
                _ => null,
            };

            if (sub == null)
                return 0;

            var field = sub.GetType().GetField(parts[1]);
            return field != null ? (int)field.GetValue(sub) : 0;
        }
    }
}
