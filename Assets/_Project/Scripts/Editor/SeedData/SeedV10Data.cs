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

            foreach (var (id, name, duty, pos) in defs)
            {
                var safeName = name.Replace(" ", "_").Replace("-", "");
                var path = Res + "/Roles/Role_" + id + "_" + safeName + ".asset";
                var so = CreateOrLoad<PlayerRoleSO>(path);
                so.id = id;
                so.displayName = name;
                so.defaultDuty = duty;
                so.compatiblePositions = new List<Position>(pos);
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
    }
}
