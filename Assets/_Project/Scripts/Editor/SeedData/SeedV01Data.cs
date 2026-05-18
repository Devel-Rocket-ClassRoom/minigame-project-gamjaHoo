// SeedV01Data.cs
// V0.1 SO 인스턴스 일괄 시드. Editor 메뉴(FM-Lite > Seed > Generate V0.1 Data) 에서 실행.
// 기존 에셋은 새 필드만 덮어쓰고 GUID 유지 — 시드 재실행해도 참조 안 깨짐.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FMLite.Domain;

namespace FMLite.Editor
{
    public static class SeedV01Data
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string Resources = DataRoot + "/Resources";

        [MenuItem("FM-Lite/Seed/Generate V0.1 Data")]
        public static void GenerateAll()
        {
            EnsureFolders();
            GenerateGameBalance();
            GeneratePositions();
            GenerateTraits();
            GenerateCountries();
            GenerateNamePools();
            GenerateLeagueConfigs();
            GenerateFacilityLevels();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SeedV01Data] V0.1 seed data generated.");
        }

        // ----- folder setup -----

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project", "Data");
            EnsureFolder(DataRoot, "Resources");
            foreach (var sub in new[] { "Balance", "Positions", "Traits", "Countries", "NamePools", "Leagues", "Facilities" })
            {
                EnsureFolder(Resources, sub);
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            var full = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ----- generators -----

        private static void GenerateGameBalance()
        {
            var balance = CreateOrLoad<GameBalanceSO>(Resources + "/Balance/GameBalance.asset");
            // 기본값은 SO 정의에 있음. 필요 시 시드에서 덮어쓰기 가능.
            EditorUtility.SetDirty(balance);
        }

        private static void GeneratePositions()
        {
            // (Position, displayName, isGoalkeeper, emphasizesTechnical, emphasizesMental, emphasizesPhysical)
            var rows = new (Position pos, string name, bool gk, bool tech, bool mental, bool phys)[]
            {
                (Position.GK, "골키퍼",            true,  false, true,  true ),
                (Position.CB, "센터백",            false, false, true,  true ),
                (Position.LB, "레프트백",          false, true,  true,  true ),
                (Position.RB, "라이트백",          false, true,  true,  true ),
                (Position.WB, "윙백",              false, true,  true,  true ),
                (Position.DM, "수비형 미드필더",   false, true,  true,  true ),
                (Position.CM, "센트럴 미드필더",   false, true,  true,  true ),
                (Position.AM, "공격형 미드필더",   false, true,  true,  false),
                (Position.LM, "레프트 미드필더",   false, true,  true,  true ),
                (Position.RM, "라이트 미드필더",   false, true,  true,  true ),
                (Position.LW, "레프트 윙",         false, true,  false, true ),
                (Position.RW, "라이트 윙",         false, true,  false, true ),
                (Position.ST, "스트라이커",        false, true,  true,  true ),
                (Position.CF, "센터 포워드",       false, true,  true,  true ),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var so = CreateOrLoad<PositionSO>(Resources + "/Positions/Position_" + r.pos + ".asset");
                so.id = i + 1;
                so.position = r.pos;
                so.displayName = r.name;
                so.isGoalkeeper = r.gk;
                so.emphasizesTechnical = r.tech;
                so.emphasizesMental = r.mental;
                so.emphasizesPhysical = r.phys;
                EditorUtility.SetDirty(so);
            }
        }

        private static void GenerateTraits()
        {
            var rows = new (int id, string name, string desc, float weight)[]
            {
                (1, "늦깎이형",   "성장이 늦지만 PA 가 높음",          1.0f),
                (2, "조숙형",     "어린 나이에 빠르게 성장",            1.0f),
                (3, "부상 취약",  "부상 발생 빈도가 높음",              0.7f),
                (4, "멘탈 강자",  "큰 경기에 강함",                     1.0f),
                (5, "빅매치형",   "강팀 상대 경기에서 활약",            0.8f),
                (6, "만능형",     "여러 포지션을 소화 가능",            0.8f),
            };
            foreach (var r in rows)
            {
                var so = CreateOrLoad<TraitSO>(Resources + "/Traits/Trait_" + r.id + "_" + Sanitize(r.name) + ".asset");
                so.id = r.id;
                so.displayName = r.name;
                so.description = r.desc;
                so.weight = r.weight;
                EditorUtility.SetDirty(so);
            }
        }

        private static void GenerateCountries()
        {
            var rows = new (int id, string code, string name, Color primary, Color secondary)[]
            {
                (1,  "ENG", "잉글랜드",     Color.white,                       Color.red),
                (2,  "FRA", "프랑스",       new Color(0f, 0.34f, 0.69f),       Color.white),
                (3,  "GER", "독일",         Color.black,                       new Color(1f, 0.81f, 0f)),
                (4,  "ESP", "스페인",       new Color(0.78f, 0.06f, 0.18f),    new Color(1f, 0.79f, 0f)),
                (5,  "ITA", "이탈리아",     new Color(0f, 0.55f, 0.27f),       Color.white),
                (6,  "BRA", "브라질",       new Color(0f, 0.61f, 0.28f),       new Color(1f, 0.86f, 0f)),
                (7,  "ARG", "아르헨티나",   new Color(0.45f, 0.71f, 0.85f),    Color.white),
                (8,  "NED", "네덜란드",     new Color(1f, 0.49f, 0f),          Color.white),
                (9,  "POR", "포르투갈",     new Color(0.78f, 0.06f, 0.18f),    new Color(0f, 0.5f, 0.25f)),
                (10, "KOR", "대한민국",     Color.white,                       Color.red),
            };
            foreach (var r in rows)
            {
                var so = CreateOrLoad<CountrySO>(Resources + "/Countries/Country_" + r.code + ".asset");
                so.id = r.id;
                so.code = r.code;
                so.displayName = r.name;
                so.flagPrimaryColor = r.primary;
                so.flagSecondaryColor = r.secondary;
                EditorUtility.SetDirty(so);
            }
        }

        private static void GenerateNamePools()
        {
            var pools = new Dictionary<int, (string code, string[] firstNames, string[] lastNames)>
            {
                [1] = ("ENG", new[]{ "James","John","Robert","Michael","William","David","Richard","Joseph","Thomas","Charles","Daniel","Matthew","Anthony","Mark","Steven" },
                              new[]{ "Smith","Johnson","Williams","Brown","Jones","Miller","Davis","Wilson","Anderson","Taylor","Thomas","Moore","Jackson","Martin","Lee" }),
                [2] = ("FRA", new[]{ "Pierre","Jean","Jacques","Michel","Philippe","Nicolas","Alain","Bernard","Daniel","Christian","Patrick","Marc","André","Yves","Olivier" },
                              new[]{ "Martin","Bernard","Dubois","Thomas","Robert","Petit","Richard","Durand","Moreau","Laurent","Simon","Michel","Lefebvre","Leroy","Roux" }),
                [3] = ("GER", new[]{ "Hans","Michael","Stefan","Klaus","Wolfgang","Thomas","Peter","Andreas","Christian","Manfred","Werner","Jürgen","Helmut","Dieter","Frank" },
                              new[]{ "Müller","Schmidt","Schneider","Fischer","Weber","Meyer","Wagner","Becker","Schulz","Hoffmann","Schäfer","Koch","Bauer","Richter","Klein" }),
                [4] = ("ESP", new[]{ "Antonio","José","Manuel","Francisco","David","Juan","Javier","Daniel","Jesús","Carlos","Alejandro","Miguel","Rafael","Pedro","Sergio" },
                              new[]{ "García","Rodríguez","González","Fernández","López","Martínez","Sánchez","Pérez","Gómez","Martín","Jiménez","Ruiz","Hernández","Díaz","Moreno" }),
                [5] = ("ITA", new[]{ "Marco","Andrea","Luca","Alessandro","Stefano","Francesco","Matteo","Davide","Antonio","Giuseppe","Roberto","Luigi","Paolo","Riccardo","Federico" },
                              new[]{ "Rossi","Russo","Ferrari","Esposito","Bianchi","Romano","Colombo","Ricci","Marino","Greco","Bruno","Gallo","Conti","De Luca","Costa" }),
                [6] = ("BRA", new[]{ "João","José","Antonio","Francisco","Carlos","Paulo","Pedro","Lucas","Luiz","Marcos","Luis","Gabriel","Rafael","Daniel","Marcelo" },
                              new[]{ "Silva","Santos","Oliveira","Souza","Rodrigues","Ferreira","Alves","Pereira","Lima","Gomes","Costa","Ribeiro","Martins","Carvalho","Almeida" }),
                [7] = ("ARG", new[]{ "Juan","José","Carlos","Luis","Miguel","Jorge","Roberto","Daniel","Pablo","Diego","Alejandro","Eduardo","Sergio","Marcelo","Fernando" },
                              new[]{ "González","Rodríguez","Gómez","Fernández","López","Díaz","Martínez","Pérez","García","Sánchez","Romero","Sosa","Álvarez","Torres","Ruiz" }),
                [8] = ("NED", new[]{ "Daan","Sem","Lucas","Levi","Bram","Tim","Mees","Thijs","Jesse","Stijn","Finn","Sven","Noah","Liam","Lars" },
                              new[]{ "De Jong","Jansen","De Vries","Van den Berg","Van Dijk","Bakker","Janssen","Visser","Smit","Meijer","De Boer","Mulder","De Groot","Bos","Vos" }),
                [9] = ("POR", new[]{ "João","Pedro","Tiago","Diogo","Rui","Miguel","Bruno","Luís","Carlos","André","Daniel","Ricardo","Filipe","Hugo","Rafael" },
                              new[]{ "Silva","Santos","Pereira","Ferreira","Oliveira","Costa","Rodrigues","Martins","Sousa","Fernandes","Gonçalves","Lopes","Marques","Almeida","Ribeiro" }),
                [10]= ("KOR", new[]{ "민준","서준","도윤","예준","시우","주원","하준","지호","지후","준서","준우","현우","도현","우진","건우" },
                              new[]{ "김","이","박","최","정","강","조","윤","장","임","한","오","서","신","권" }),
            };
            foreach (var kv in pools)
            {
                var (code, first, last) = kv.Value;
                var so = CreateOrLoad<NamePoolSO>(Resources + "/NamePools/NamePool_" + code + ".asset");
                so.countryId = kv.Key;
                so.firstNames = new List<string>(first);
                so.lastNames = new List<string>(last);
                EditorUtility.SetDirty(so);
            }
        }

        private static void GenerateLeagueConfigs()
        {
            var so = CreateOrLoad<LeagueConfigSO>(Resources + "/Leagues/League_EPL.asset");
            so.id = 1;
            so.displayName = "Premier League";
            so.countryCode = "ENG";
            so.clubCount = 20;
            so.relegationCount = 3;
            so.playersPerClub = 25;
            EditorUtility.SetDirty(so);
        }

        private static void GenerateFacilityLevels()
        {
            // 5 단계: 비용/기간 기하급수, Youth 수치는 선형 증가
            var costs    = new[] { 50_000, 100_000, 200_000, 400_000, 800_000 };
            var days     = new[] { 30, 60, 90, 120, 150 };
            var poolSize = new[] { 5, 6, 7, 8, 9 };       // Youth only
            var avgPA    = new[] { 100, 115, 130, 145, 160 }; // Youth only

            foreach (FacilityType t in System.Enum.GetValues(typeof(FacilityType)))
            {
                for (int level = 1; level <= 5; level++)
                {
                    var so = CreateOrLoad<FacilityLevelSO>(Resources + "/Facilities/Facility_" + t + "_Lv" + level + ".asset");
                    so.facilityType = t;
                    so.level = level;
                    so.upgradeCost = costs[level - 1];
                    so.upgradeDurationDays = days[level - 1];
                    so.youthPoolSize = (t == FacilityType.Youth) ? poolSize[level - 1] : 0;
                    so.youthAvgPA   = (t == FacilityType.Youth) ? avgPA[level - 1]   : 0;
                    EditorUtility.SetDirty(so);
                }
            }
        }

        private static string Sanitize(string raw)
        {
            // 파일명에 한글이 들어가도 문제는 없으나 OS 호환 위해 공백 제거.
            return raw.Replace(" ", string.Empty);
        }
    }
}
