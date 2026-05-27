// SeedV10Data.cs
// V1.0 SO 인스턴스 일괄 시드. FM-Lite > Seed > Generate V1.0 Data 에서 실행.
// 기존 에셋은 GUID 유지하며 필드만 덮어씀.

using System.Collections.Generic;
using FMLite.Domain;
using UnityEditor;
using UnityEngine;

namespace FMLite.Editor
{
    public static class SeedV10Data
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string Res = DataRoot + "/Resources";

        [MenuItem("FM-Lite/Seed/Generate V1.0 Data")]
        public static void GenerateAll()
        {
            EnsureFolders();
            GenerateFormations();
            GeneratePlayerRoles();
            GenerateInjuryTypes();
            GenerateFacilityLevelsV10();
            GenerateTraitsV10();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SeedV10Data] V1.0 seed data generated.");
        }

        // ── Folder helpers ────────────────────────────────────────────────

        private static void EnsureFolders()
        {
            foreach (
                var sub in new[]
                {
                    "Formations",
                    "Roles",
                    "Injuries",
                    "FacilitiesV10",
                }
            )
                EnsureFolder(Res, sub);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var full = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T CreateOrLoad<T>(string path)
            where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ── Formations (6) ────────────────────────────────────────────────

        private static void GenerateFormations()
        {
            // (id, name, 11 slot positions)
            var defs = new (int id, string name, Position[] slots)[]
            {
                (
                    1,
                    "4-4-2",
                    new[]
                    {
                        Position.GK,
                        Position.CB,
                        Position.CB,
                        Position.LB,
                        Position.RB,
                        Position.CM,
                        Position.CM,
                        Position.LM,
                        Position.RM,
                        Position.ST,
                        Position.ST,
                    }
                ),
                (
                    2,
                    "4-3-3",
                    new[]
                    {
                        Position.GK,
                        Position.CB,
                        Position.CB,
                        Position.LB,
                        Position.RB,
                        Position.DM,
                        Position.CM,
                        Position.CM,
                        Position.LW,
                        Position.RW,
                        Position.CF,
                    }
                ),
                (
                    3,
                    "3-5-2",
                    new[]
                    {
                        Position.GK,
                        Position.CB,
                        Position.CB,
                        Position.CB,
                        Position.LM,
                        Position.RM,
                        Position.CM,
                        Position.CM,
                        Position.DM,
                        Position.ST,
                        Position.ST,
                    }
                ),
                (
                    4,
                    "4-2-3-1",
                    new[]
                    {
                        Position.GK,
                        Position.CB,
                        Position.CB,
                        Position.LB,
                        Position.RB,
                        Position.DM,
                        Position.DM,
                        Position.LM,
                        Position.AM,
                        Position.RM,
                        Position.ST,
                    }
                ),
                (
                    5,
                    "4-4-1-1",
                    new[]
                    {
                        Position.GK,
                        Position.CB,
                        Position.CB,
                        Position.LB,
                        Position.RB,
                        Position.CM,
                        Position.CM,
                        Position.LM,
                        Position.RM,
                        Position.AM,
                        Position.ST,
                    }
                ),
                (
                    6,
                    "5-3-2",
                    new[]
                    {
                        Position.GK,
                        Position.CB,
                        Position.CB,
                        Position.CB,
                        Position.WB,
                        Position.WB,
                        Position.DM,
                        Position.CM,
                        Position.CM,
                        Position.ST,
                        Position.ST,
                    }
                ),
            };

            foreach (var (id, name, slots) in defs)
            {
                var path = Res + "/Formations/Formation_" + name.Replace("-", "") + ".asset";
                var so = CreateOrLoad<FormationSO>(path);
                so.id = id;
                so.displayName = name;
                so.slotPositions = slots;
                EditorUtility.SetDirty(so);
            }
        }

        // ── Player Roles (~40) ────────────────────────────────────────────

        private static void GeneratePlayerRoles()
        {
            // (id, name, duty, compatible positions)
            var defs = new (int id, string name, Duty duty, Position[] pos)[]
            {
                // GK
                (1, "Goalkeeper", Duty.Defend, new[] { Position.GK }),
                (2, "Sweeper Keeper", Duty.Attack, new[] { Position.GK }),
                (3, "Command GK", Duty.Defend, new[] { Position.GK }),
                // CB
                (4, "Central Defender", Duty.Defend, new[] { Position.CB }),
                (5, "Ball-Playing Defender", Duty.Support, new[] { Position.CB }),
                (6, "Libero", Duty.Attack, new[] { Position.CB }),
                (7, "Limited Defender", Duty.Defend, new[] { Position.CB, Position.WB }),
                // LB
                (8, "Full Back L", Duty.Support, new[] { Position.LB }),
                (9, "Wing Back L", Duty.Attack, new[] { Position.LB, Position.WB }),
                (10, "Inverted Full Back L", Duty.Support, new[] { Position.LB }),
                // RB
                (11, "Full Back R", Duty.Support, new[] { Position.RB }),
                (12, "Wing Back R", Duty.Attack, new[] { Position.RB, Position.WB }),
                (13, "Inverted Full Back R", Duty.Support, new[] { Position.RB }),
                // WB
                (14, "Wing Back", Duty.Attack, new[] { Position.WB, Position.LB, Position.RB }),
                (15, "Wide Midfielder Def", Duty.Support, new[] { Position.WB, Position.LM, Position.RM }),
                // DM
                (16, "Defensive Midfielder", Duty.Defend, new[] { Position.DM, Position.CM }),
                (17, "Deep Lying Playmaker", Duty.Support, new[] { Position.DM, Position.CM }),
                (18, "Ball-Winning Midfielder", Duty.Defend, new[] { Position.DM, Position.CM }),
                // CM
                (19, "Box to Box Midfielder", Duty.Support, new[] { Position.CM, Position.DM }),
                (20, "Central Midfielder", Duty.Support, new[] { Position.CM }),
                (21, "Mezzala", Duty.Attack, new[] { Position.CM, Position.AM }),
                (22, "Carrilero", Duty.Defend, new[] { Position.CM, Position.DM }),
                // AM
                (23, "Advanced Playmaker", Duty.Support, new[] { Position.AM, Position.CM }),
                (24, "Shadow Striker", Duty.Attack, new[] { Position.AM }),
                (25, "Enganche", Duty.Support, new[] { Position.AM }),
                // LM
                (26, "Wide Midfielder L", Duty.Support, new[] { Position.LM, Position.LW }),
                (27, "Winger L", Duty.Attack, new[] { Position.LM, Position.LW }),
                (28, "Wide Playmaker L", Duty.Support, new[] { Position.LM, Position.AM }),
                // RM
                (29, "Wide Midfielder R", Duty.Support, new[] { Position.RM, Position.RW }),
                (30, "Winger R", Duty.Attack, new[] { Position.RM, Position.RW }),
                (31, "Wide Playmaker R", Duty.Support, new[] { Position.RM, Position.AM }),
                // LW
                (32, "Winger LW", Duty.Attack, new[] { Position.LW, Position.LM }),
                (33, "Inside Forward L", Duty.Attack, new[] { Position.LW, Position.AM }),
                // RW
                (34, "Winger RW", Duty.Attack, new[] { Position.RW, Position.RM }),
                (35, "Inside Forward R", Duty.Attack, new[] { Position.RW, Position.AM }),
                // ST
                (36, "Advanced Forward", Duty.Attack, new[] { Position.ST, Position.CF }),
                (37, "Poacher", Duty.Attack, new[] { Position.ST }),
                (38, "Target Man", Duty.Support, new[] { Position.ST }),
                (39, "Pressing Forward", Duty.Support, new[] { Position.ST, Position.CF }),
                // CF
                (40, "False Nine", Duty.Support, new[] { Position.CF, Position.AM }),
            };

            // J.2 — algorithms.md V1.0-7 TacticImpact 에서 사용할 이벤트 가중치 (1.0 기준 편차만 기재).
            var modifiers = new Dictionary<int, (string et, float mult)[]>
            {
                [5] = new[] { ("keyPass", 1.2f) }, // Ball-Playing Defender
                [16] = new[] { ("tackle", 1.4f) }, // Defensive Midfielder
                [17] = new[] { ("keyPass", 1.3f) }, // Deep Lying Playmaker
                [18] = new[] { ("tackle", 1.5f) }, // Ball-Winning Midfielder
                [23] = new[] { ("keyPass", 1.4f) }, // Advanced Playmaker
                [27] = new[] { ("cross", 1.3f) }, // Winger L
                [30] = new[] { ("cross", 1.3f) }, // Winger R
                [32] = new[] { ("cross", 1.3f) }, // Winger LW
                [34] = new[] { ("cross", 1.3f) }, // Winger RW
                [36] = new[] { ("shot", 1.2f) }, // Advanced Forward
                [37] = new[] { ("shot", 1.5f) }, // Poacher — T1: ~2× vs Target Man
                [38] = new[] { ("shot", 0.75f) }, // Target Man — T1: ~2× vs Poacher
                [39] = new[] { ("tackle", 1.2f) }, // Pressing Forward
                [40] = new[] { ("keyPass", 1.3f) }, // False Nine
            };

            foreach (var (id, name, duty, pos) in defs)
            {
                var safeName = name.Replace(" ", "_").Replace("-", "");
                var path = Res + "/Roles/Role_" + id + "_" + safeName + ".asset";
                var so = CreateOrLoad<PlayerRoleSO>(path);
                so.id = id;
                so.displayName = name;
                so.defaultDuty = duty;
                so.compatiblePositions = new List<Position>(pos);
                so.eventModifiers = new List<MatchEventModifier>();
                if (modifiers.TryGetValue(id, out var mods))
                    foreach (var (et, mult) in mods)
                        so.eventModifiers.Add(new MatchEventModifier { eventType = et, multiplier = mult });
                EditorUtility.SetDirty(so);
            }
        }

        // ── Injury Types (15) ─────────────────────────────────────────────

        private static void GenerateInjuryTypes()
        {
            // (id, name, minDays, maxDays, weight)
            var defs = new (int id, string name, int min, int max, float w)[]
            {
                (1, "발목 염좌", 7, 14, 3.0f),
                (2, "햄스트링 파열", 14, 28, 2.5f),
                (3, "허벅지 근육 부상", 7, 21, 2.5f),
                (4, "종아리 부상", 7, 21, 2.0f),
                (5, "사타구니 염좌", 7, 21, 2.0f),
                (6, "무릎 타박상", 3, 14, 2.0f),
                (7, "등 부상", 14, 42, 1.5f),
                (8, "발 부상", 7, 28, 1.5f),
                (9, "복부 근육 부상", 7, 21, 1.5f),
                (10, "어깨 부상", 7, 21, 1.0f),
                (11, "정강이 통증", 7, 14, 1.5f),
                (12, "엉덩이 굴근 부상", 14, 21, 1.0f),
                (13, "중족골 골절", 42, 84, 0.5f),
                (14, "전방 십자 인대", 180, 270, 0.2f),
                (15, "뇌진탕", 7, 21, 0.5f),
            };

            foreach (var (id, name, min, max, w) in defs)
            {
                var path = Res + "/Injuries/Injury_" + id + ".asset";
                var so = CreateOrLoad<InjuryTypeSO>(path);
                so.id = id;
                so.displayName = name;
                so.minDays = min;
                so.maxDays = max;
                so.weight = w;
                EditorUtility.SetDirty(so);
            }
        }

        // ── Facility Levels V1.0 (8 types × 10 levels = 80) ──────────────

        private static void GenerateeFacilityLevelsV10Impl()
        {
            // 비용: 50K → ~5M (기하급수)
            var costs = new[]
            {
                50_000,
                100_000,
                200_000,
                400_000,
                800_000,
                1_500_000,
                2_500_000,
                3_500_000,
                5_000_000,
                7_000_000,
            };
            // 기간: 30 → 270일
            var days = new[] { 30, 45, 60, 75, 90, 105, 120, 150, 210, 270 };

            // Scout
            var scoutListSizes = new[] { 50, 100, 200, 400, 800, 1500, 3000, 5000, 8000, 15000 };
            var scoutMargins = new[] { 30, 25, 20, 15, 12, 10, 7, 5, 3, 2 };

            // Training
            var trainEff = new[]
            {
                1.00f,
                1.05f,
                1.10f,
                1.15f,
                1.20f,
                1.28f,
                1.36f,
                1.45f,
                1.55f,
                1.70f,
            };

            // YouthCoach
            var ycPABonus = new[] { 0, 3, 6, 9, 12, 15, 18, 22, 27, 35 };
            var ycTraitChance = new[]
            {
                0.05f,
                0.08f,
                0.11f,
                0.14f,
                0.18f,
                0.22f,
                0.27f,
                0.33f,
                0.40f,
                0.50f,
            };

            // YouthRecruitment
            var yrPoolSize = new[] { 4, 5, 6, 7, 8, 9, 10, 12, 14, 16 };

            // YouthFacility
            var yfGrowth = new[]
            {
                1.00f,
                1.05f,
                1.10f,
                1.15f,
                1.20f,
                1.28f,
                1.36f,
                1.45f,
                1.55f,
                1.70f,
            };

            // Medical
            var medInjuryRate = new[]
            {
                1.00f,
                0.96f,
                0.92f,
                0.88f,
                0.84f,
                0.79f,
                0.73f,
                0.67f,
                0.60f,
                0.50f,
            };
            var medRecovery = new[]
            {
                1.00f,
                1.05f,
                1.10f,
                1.15f,
                1.20f,
                1.28f,
                1.36f,
                1.45f,
                1.55f,
                1.70f,
            };

            // Stadium
            var stadTicket = new[]
            {
                50_000,
                75_000,
                110_000,
                160_000,
                220_000,
                300_000,
                400_000,
                550_000,
                750_000,
                1_000_000,
            };
            var stadRep = new[] { 0, 0, 1, 1, 2, 2, 3, 4, 5, 7 };

            // Gym
            var gymGrowth = new[]
            {
                1.00f,
                1.03f,
                1.06f,
                1.10f,
                1.14f,
                1.18f,
                1.23f,
                1.29f,
                1.36f,
                1.45f,
            };

            var types = new[]
            {
                FacilityType.Scout,
                FacilityType.Training,
                FacilityType.YouthCoach,
                FacilityType.YouthRecruitment,
                FacilityType.YouthFacility,
                FacilityType.Medical,
                FacilityType.Stadium,
                FacilityType.Gym,
            };

            foreach (var t in types)
            {
                for (int lv = 1; lv <= 10; lv++)
                {
                    var i = lv - 1;
                    var path = Res + "/FacilitiesV10/FacV10_" + t + "_Lv" + lv + ".asset";
                    var so = CreateOrLoad<FacilityLevelSO>(path);
                    so.facilityType = t;
                    so.level = lv;
                    so.upgradeCost = costs[i];
                    so.upgradeDurationDays = days[i];

                    switch (t)
                    {
                        case FacilityType.Scout:
                            so.scoutingListSize = scoutListSizes[i];
                            so.caAccuracyMargin = scoutMargins[i];
                            break;
                        case FacilityType.Training:
                            so.trainingEfficiency = trainEff[i];
                            break;
                        case FacilityType.YouthCoach:
                            so.youthAvgPABonus = ycPABonus[i];
                            so.traitGrantChance = ycTraitChance[i];
                            break;
                        case FacilityType.YouthRecruitment:
                            so.youthPoolSize = yrPoolSize[i];
                            break;
                        case FacilityType.YouthFacility:
                            so.youthGrowthRate = yfGrowth[i];
                            break;
                        case FacilityType.Medical:
                            so.injuryRateMultiplier = medInjuryRate[i];
                            so.recoverySpeedMultiplier = medRecovery[i];
                            break;
                        case FacilityType.Stadium:
                            so.ticketRevenueBase = stadTicket[i];
                            so.reputationBonus = stadRep[i];
                            break;
                        case FacilityType.Gym:
                            so.physicalGrowthBonus = gymGrowth[i];
                            break;
                    }

                    EditorUtility.SetDirty(so);
                }
            }
        }

        private static void GenerateFacilityLevelsV10() =>
            GenerateeFacilityLevelsV10Impl();

        // ── Traits V1.0 (C.2 + C.3) ──────────────────────────────────────
        // exclusionGroupId: 0=없음 / 1=DevelopmentSpeed / 2=Durability / 3=PressureMentality

        [MenuItem("FM-Lite/Seed/Generate V1.0 Traits")]
        public static void GenerateTraitsV10()
        {
            EnsureFolder(Res, "Traits");

            // (id, displayName, desc, weight, exclusionGroupId, effects[])
            // effect tuple: (type, value, targetStat)
            var defs = new (
                int id,
                string name,
                string desc,
                float weight,
                int group,
                (TraitEffectType type, float value, string target)[] fx
            )[]
            {
                // ── 기존 6개 effects 채움 ─────────────────────────────────────
                (
                    1,
                    "늦깎이형",
                    "성장이 늦지만 PA 가 높음",
                    1.0f,
                    1,
                    new[] { (TraitEffectType.GrowthRateModifier, 1.3f, "") }
                ),
                (
                    2,
                    "조숙형",
                    "어린 나이에 빠르게 성장",
                    1.0f,
                    1,
                    new[] { (TraitEffectType.GrowthRateModifier, 0.7f, "") }
                ),
                (
                    3,
                    "부상 취약",
                    "부상 발생 빈도가 높음",
                    0.7f,
                    0,
                    new[]
                    {
                        (TraitEffectType.InjuryRateModifier, 2.0f, ""),
                        (TraitEffectType.GrowthRateModifier, 30f, "hidden:injuryProneness"),
                    }
                ),
                (
                    4,
                    "멘탈 강자",
                    "큰 경기에 강함",
                    1.0f,
                    0,
                    new[] { (TraitEffectType.GrowthRateModifier, 15f, "hidden:professionalism") }
                ),
                (
                    5,
                    "빅매치형",
                    "강팀 상대 경기에서 활약",
                    0.8f,
                    3, // PressureMentality 그룹 (C.3)
                    new[] { (TraitEffectType.GrowthRateModifier, 20f, "hidden:pressureHandling") }
                ),
                (
                    6,
                    "만능형",
                    "여러 포지션을 소화 가능",
                    0.8f,
                    0,
                    new[] { (TraitEffectType.GrowthRateModifier, 20f, "hidden:versatility") }
                ),
                // ── 신규 14개 ─────────────────────────────────────────────────
                (
                    7,
                    "클러치형",
                    "결정적 순간에 강한 집중력",
                    0.6f,
                    0,
                    new[]
                    {
                        (TraitEffectType.GrowthRateModifier, 25f, "hidden:pressureHandling"),
                        (TraitEffectType.MatchModifier, 1.15f, "clutch_match"),
                    }
                ),
                (
                    8,
                    "무리한패스",
                    "위험한 패스를 자주 시도함",
                    0.7f,
                    0,
                    new[] { (TraitEffectType.MatchModifier, 1.2f, "risky_pass") }
                ),
                (
                    9,
                    "와이드플레이어",
                    "측면 공간을 적극 활용",
                    0.8f,
                    0,
                    new[] { (TraitEffectType.MatchModifier, 1.2f, "wide_play") }
                ),
                (
                    10,
                    "자국인우대",
                    "고향 구단 이적 선호",
                    0.5f,
                    0,
                    new[] { (TraitEffectType.MoralePropensity, -0.1f, "homesick") }
                ),
                (
                    11,
                    "유리몸",
                    "부상 발생률이 매우 높음",
                    0.5f,
                    2, // Durability 그룹 (C.3)
                    new[]
                    {
                        (TraitEffectType.InjuryRateModifier, 2.5f, ""),
                        (TraitEffectType.GrowthRateModifier, 30f, "hidden:injuryProneness"),
                        (TraitEffectType.MarketValueModifier, 0.8f, ""),
                    }
                ),
                (
                    12,
                    "철인",
                    "부상 발생률이 매우 낮음",
                    0.8f,
                    2, // Durability 그룹 (C.3)
                    new[]
                    {
                        (TraitEffectType.InjuryRateModifier, 0.4f, ""),
                        (TraitEffectType.GrowthRateModifier, -20f, "hidden:injuryProneness"),
                        (TraitEffectType.MarketValueModifier, 1.1f, ""),
                    }
                ),
                (
                    13,
                    "멘탈약자",
                    "압박 상황에서 쉽게 흔들림",
                    0.6f,
                    3, // PressureMentality 그룹 (C.3)
                    new[]
                    {
                        (TraitEffectType.GrowthRateModifier, -20f, "hidden:pressureHandling"),
                        (TraitEffectType.MoralePropensity, -0.2f, "fragile"),
                    }
                ),
                (
                    14,
                    "슈퍼유망주",
                    "잠재력이 매우 높음",
                    0.3f,
                    0,
                    new[]
                    {
                        (TraitEffectType.GrowthRateModifier, 1.5f, ""),
                        (TraitEffectType.MarketValueModifier, 1.2f, ""),
                    }
                ),
                (
                    15,
                    "멀티포지션",
                    "다수의 포지션을 자연스럽게 소화",
                    0.7f,
                    0,
                    new[] { (TraitEffectType.GrowthRateModifier, 20f, "hidden:versatility") }
                ),
                (
                    16,
                    "골결정력",
                    "득점 기회를 냉정하게 마무리",
                    0.6f,
                    0,
                    new[] { (TraitEffectType.MatchModifier, 1.25f, "clinical_finish") }
                ),
                (
                    17,
                    "수비형윙백",
                    "공격보다 수비에 치중하는 윙백",
                    0.7f,
                    0,
                    new[] { (TraitEffectType.MatchModifier, 1.1f, "defensive_wing") }
                ),
                (
                    18,
                    "정신적리더",
                    "팀 사기에 긍정적 영향",
                    0.5f,
                    0,
                    new[]
                    {
                        (TraitEffectType.GrowthRateModifier, 20f, "hidden:loyalty"),
                        (TraitEffectType.MoralePropensity, 0.15f, "leader"),
                    }
                ),
                (
                    19,
                    "페널티스페셜리스트",
                    "페널티킥 성공률이 높음",
                    0.5f,
                    0,
                    new[] { (TraitEffectType.MatchModifier, 1.3f, "penalty") }
                ),
                (
                    20,
                    "프리킥마이스터",
                    "프리킥 정확도가 탁월",
                    0.5f,
                    0,
                    new[] { (TraitEffectType.MatchModifier, 1.35f, "free_kick") }
                ),
            };

            foreach (var d in defs)
            {
                var path =
                    Res + "/Traits/Trait_" + d.id + "_" + Sanitize(d.name) + ".asset";
                var so = CreateOrLoad<TraitSO>(path);
                so.id = d.id;
                so.displayName = d.name;
                so.description = d.desc;
                so.weight = d.weight;
                so.exclusionGroupId = d.group;
                so.effects = new List<TraitEffect>();
                foreach (var (type, value, target) in d.fx)
                    so.effects.Add(
                        new TraitEffect
                        {
                            type = type,
                            value = value,
                            targetStat = target,
                        }
                    );
                EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SeedV10Data] 20 traits generated/updated.");
        }

        private static string Sanitize(string s) =>
            System.Text.RegularExpressions.Regex.Replace(s, @"[^a-zA-Z0-9가-힣]", "_");
    }
}
