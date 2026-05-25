// PlayerGenerator.cs
// algorithms.md #1 Player Generation 6-step 구현.
// 호출자가 포지션·나이·국적·구단을 지정 → 완성된 Player 객체 반환.
// Player.id 는 호출자가 부여 (algorithms.md Outputs 참조).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using FMLite.Utils;
using UnityEngine;
using Random = System.Random;

namespace FMLite.Application
{
    public static class PlayerGenerator
    {
        public static Player Generate(
            Random rng,
            int clubReputation,
            Position targetPosition,
            int age,
            string nationalityCode,
            int clubId,
            int youthClubId,
            PlayerOrigin origin,
            DateTime currentDate,
            GameBalanceSO balance
        )
        {
            clubReputation = Math.Clamp(clubReputation, 0, 100);
            if (age < balance.minAge || age > balance.maxAge)
                Debug.LogWarning(
                    $"[PlayerGenerator] age {age} out of range [{balance.minAge}, {balance.maxAge}]."
                );

            // 1~6단계: algorithms.md #1 Logic 순서 준수
            int ca = DetermineCA(rng, clubReputation, age, balance);
            int pa = DeterminePA(rng, ca, age, balance);
            var stats = DistributeStats(rng, ca, targetPosition, balance);
            var traitIds = SelectTraits(rng, balance);
            var info = BuildPersonalInfo(
                rng,
                targetPosition,
                age,
                nationalityCode,
                currentDate,
                balance
            );
            int faceSeed = rng.Next();
            var contract = BuildContract(rng, ca, currentDate, balance);
            var state = BuildState();

            return new Player
            {
                info = info,
                stats = stats,
                currentAbility = ca,
                potentialAbility = pa,
                traitIds = traitIds,
                currentClubId = clubId,
                youthClubId = youthClubId,
                origin = origin,
                contract = contract,
                state = state,
                career = new List<SeasonStat>(),
                faceSeed = faceSeed,
            };
        }

        // ── 1단계: CA 결정 ────────────────────────────────────────────

        private static int DetermineCA(Random rng, int rep, int age, GameBalanceSO b)
        {
            // meanCA: rep=0→60, rep=50→100, rep=100→140
            double meanCA = b.caRepBase + (double)b.caRepCoeff * rep;
            double rawCA = meanCA + rng.NextNormal(0, b.caStdDev);

            if (age < b.caPeakAge)
            {
                int span = b.caPeakAge - b.minAge;
                double youngBlend = span > 0 ? Clamp01((double)(age - b.minAge) / span) : 1.0;
                rawCA *= Lerp((double)b.caYoungMultiplier, 1.0, youngBlend);
            }

            return Math.Clamp((int)Math.Round(rawCA), b.minCA, b.maxCA);
        }

        // ── 2단계: PA 결정 ────────────────────────────────────────────

        private static int DeterminePA(Random rng, int ca, int age, GameBalanceSO b)
        {
            int span = b.paGapZeroAge - b.minAge;
            double ageBlend = span > 0 ? Clamp01((double)(age - b.minAge) / span) : 1.0;
            double meanGap = Lerp((double)b.paGapMaxMean, 0.0, ageBlend);
            double rawPA = ca + Math.Max(0.0, meanGap + rng.NextNormal(0, b.paGapStdDev));
            return Math.Clamp((int)Math.Round(rawPA), ca, b.maxPA);
        }

        // ── 3단계: 스탯 분배 ──────────────────────────────────────────

        private static Stats DistributeStats(Random rng, int ca, Position pos, GameBalanceSO b)
        {
            double caNorm = ca / (double)b.maxCA;
            double baseMean = Lerp((double)b.statMeanAtCAFloor, (double)b.statMeanAtCACeil, caNorm);
            var posSO = GetPositionSO(pos);
            var stats = new Stats();

            if (posSO != null && posSO.isGoalkeeper)
                FillGkProfile(rng, stats, baseMean, b);
            else
                FillOutfieldProfile(rng, stats, baseMean, posSO, b);

            return stats;
        }

        private static void FillGkProfile(Random rng, Stats s, double mean, GameBalanceSO b)
        {
            double sec = mean - b.gkSecondaryStatPenalty;
            double tech = mean - b.gkOutfieldStatPenalty;
            s.gk.ApplyToAll(_ => ClampStat(rng.NextNormal(mean, b.statStdDev)));
            s.mental.ApplyToAll(_ => ClampStat(rng.NextNormal(sec, b.statStdDev)));
            s.physical.ApplyToAll(_ => ClampStat(rng.NextNormal(sec, b.statStdDev)));
            s.technical.ApplyToAll(_ => ClampStat(rng.NextNormal(tech, b.statStdDev)));
        }

        private static void FillOutfieldProfile(
            Random rng,
            Stats s,
            double mean,
            PositionSO posSO,
            GameBalanceSO b
        )
        {
            double techMean =
                mean
                + (
                    posSO != null && posSO.emphasizesTechnical
                        ? b.statEmphasisBonus
                        : -b.statEmphasisPenalty
                );
            double mentMean =
                mean
                + (
                    posSO != null && posSO.emphasizesMental
                        ? b.statEmphasisBonus
                        : -b.statEmphasisPenalty
                );
            double physMean =
                mean
                + (
                    posSO != null && posSO.emphasizesPhysical
                        ? b.statEmphasisBonus
                        : -b.statEmphasisPenalty
                );
            s.technical.ApplyToAll(_ => ClampStat(rng.NextNormal(techMean, b.statStdDev)));
            s.mental.ApplyToAll(_ => ClampStat(rng.NextNormal(mentMean, b.statStdDev)));
            s.physical.ApplyToAll(_ => ClampStat(rng.NextNormal(physMean, b.statStdDev)));
            // 필드 플레이어의 GK 스탯은 고정 낮은 기저값, sigma 절반
            s.gk.ApplyToAll(_ =>
                ClampStat(rng.NextNormal(b.outfieldGkStatBase, b.statStdDev * 0.5))
            );
        }

        // ── 4단계: 트레잇 ─────────────────────────────────────────────

        private static List<int> SelectTraits(Random rng, GameBalanceSO b)
        {
            var selected = new List<int>();
            var usedGroups = new HashSet<int>();

            if (rng.NextDouble() < b.traitProbabilityPerPlayer)
            {
                TryAddTrait(rng, selected, usedGroups);
                // 추가 트레잇은 첫 트레잇 획득 시에만 시도 (분포: 0개=70%, 1개=25.5%, 2개=3.8%)
                while (rng.NextDouble() < b.additionalTraitProbability)
                    if (!TryAddTrait(rng, selected, usedGroups))
                        break;
            }

            return selected;
        }

        private static bool TryAddTrait(Random rng, List<int> selected, HashSet<int> usedGroups)
        {
            var pool = GameDatabase
                .AllTraits.Where(t =>
                    !selected.Contains(t.id)
                    && (t.exclusionGroupId == 0 || !usedGroups.Contains(t.exclusionGroupId))
                )
                .ToList();

            if (pool.Count == 0)
            {
                Debug.LogWarning("[PlayerGenerator] Trait pool exhausted.");
                return false;
            }

            var chosen = rng.WeightedSample(pool, t => (double)t.weight);
            selected.Add(chosen.id);
            if (chosen.exclusionGroupId != 0)
                usedGroups.Add(chosen.exclusionGroupId);
            return true;
        }

        // ── 5단계: 인적사항 ───────────────────────────────────────────

        private static PersonalInfo BuildPersonalInfo(
            Random rng,
            Position pos,
            int age,
            string nationalityCode,
            DateTime currentDate,
            GameBalanceSO b
        )
        {
            var namePool = GetNamePoolByCode(nationalityCode);
            string firstName =
                namePool != null && namePool.firstNames.Count > 0
                    ? namePool.firstNames[rng.Next(namePool.firstNames.Count)]
                    : "Unknown";
            string lastName =
                namePool != null && namePool.lastNames.Count > 0
                    ? namePool.lastNames[rng.Next(namePool.lastNames.Count)]
                    : "Player";

            var birthDate = new DateTime(currentDate.Year - age, rng.Next(1, 13), rng.Next(1, 29));
            var foot = SampleFoot(rng, b);
            var secondaryPositions = GenerateSecondaryPositions(rng, pos, b);

            return new PersonalInfo
            {
                firstName = firstName,
                lastName = lastName,
                birthDate = birthDate,
                nationalityCode = nationalityCode,
                primaryPosition = pos,
                secondaryPositions = secondaryPositions,
                preferredFoot = foot,
            };
        }

        private static Foot SampleFoot(Random rng, GameBalanceSO b)
        {
            var feet = new List<Foot> { Foot.Right, Foot.Left, Foot.Both };
            return rng.WeightedSample(
                feet,
                f =>
                    f switch
                    {
                        Foot.Right => (double)b.footRightRatio,
                        Foot.Left => (double)b.footLeftRatio,
                        Foot.Both => (double)b.footBothRatio,
                        _ => 0.0,
                    }
            );
        }

        private static List<Position> GenerateSecondaryPositions(
            Random rng,
            Position primaryPos,
            GameBalanceSO b
        )
        {
            var primarySO = GetPositionSO(primaryPos);
            if (primarySO != null && primarySO.isGoalkeeper)
                return new List<Position>();

            var result = new List<Position>();
            var exclude = new HashSet<Position> { primaryPos };

            if (rng.NextDouble() < b.secondaryPositionProbability)
            {
                var pick = PickSecondaryPosition(rng, primarySO, exclude);
                result.Add(pick);
                exclude.Add(pick);

                if (rng.NextDouble() < b.thirdPositionProbability)
                    result.Add(PickSecondaryPosition(rng, primarySO, exclude));
            }

            return result;
        }

        private static Position PickSecondaryPosition(
            Random rng,
            PositionSO primarySO,
            HashSet<Position> exclude
        )
        {
            var pool = GameDatabase
                .AllPositions.Where(p => !exclude.Contains(p.position) && !p.isGoalkeeper)
                .ToList();

            if (pool.Count == 0)
                return primarySO?.position ?? Position.CM;

            return rng.WeightedSample(
                pool,
                p =>
                {
                    if (primarySO == null)
                        return 0.05;
                    var aff = primarySO.affinities.FirstOrDefault(a => a.position == p.position);
                    return aff != null
                        ? (double)aff.weight
                        : (double)primarySO.fallbackAffinityWeight;
                }
            ).position;
        }

        // ── 6단계: 계약 · 상태 ────────────────────────────────────────

        private static Contract BuildContract(
            Random rng,
            int ca,
            DateTime currentDate,
            GameBalanceSO b
        )
        {
            return new Contract
            {
                weeklyWage = EstimateInitialWage(ca, b),
                startDate = currentDate,
                endDate = currentDate.AddYears(rng.Next(1, 5)), // 1~4년
                releaseClause = 0,
            };
        }

        private static int EstimateInitialWage(int ca, GameBalanceSO b)
        {
            float raw = b.wageBaseAtMinCA + (ca - b.minCA) * b.wagePerCAPoint;
            int rounded = (int)(Math.Round(raw / 100.0) * 100);
            return Math.Max(b.wageFloor, rounded);
        }

        private static PlayerState BuildState() =>
            new PlayerState
            {
                fatigue = 0,
                morale = 50,
                form = 50,
                injury = new InjuryInfo { injuryTypeId = -1 },
                transferListed = false,
                seasonAppearances = 0,
            };

        // ── Helpers ───────────────────────────────────────────────────

        private static PositionSO GetPositionSO(Position pos) =>
            GameDatabase.AllPositions.FirstOrDefault(p => p.position == pos);

        private static NamePoolSO GetNamePoolByCode(string code)
        {
            var country = GameDatabase.AllCountries.FirstOrDefault(c => c.code == code);
            if (country == null)
            {
                Debug.LogWarning(
                    $"[PlayerGenerator] Country '{code}' not found, falling back to ENG."
                );
                country = GameDatabase.AllCountries.FirstOrDefault(c => c.code == "ENG");
                if (country == null)
                    return null;
            }
            var pool = GameDatabase.GetNamePool(country.id);
            if (pool == null)
                Debug.LogWarning(
                    $"[PlayerGenerator] No NamePool for country '{code}' (id {country.id})."
                );
            return pool;
        }

        private static int ClampStat(double x) => Math.Clamp((int)Math.Round(x), 1, 100);

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double Clamp01(double t) =>
            t < 0.0 ? 0.0
            : t > 1.0 ? 1.0
            : t;
    }
}
