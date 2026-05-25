// GameDatabase.cs
// SO 인스턴스를 ID 로 조회하는 정적 레지스트리.
// 위치 결정: 원래 v0.1-tasks.md 엔 Core/ 였으나 FMLite.Core 가 FMLite.Domain 을 참조할
// 수 없어 (의존 방향 Domain→Core), 관리 대상 SO 와 같은 Domain 에 배치.
//
// 사용 흐름:
//   1) GameInitializer 시작 시 GameDatabase.LoadAll() 호출 → Resources/ 스캔
//   2) 이후 Get*(id) 로 SO 조회
//   3) 테스트 / 디버그 시 Register / Clear 로 in-memory 데이터 주입

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FMLite.Domain
{
    public static class GameDatabase
    {
        private static readonly Dictionary<int, TraitSO> _traits = new();
        private static readonly Dictionary<int, PositionSO> _positions = new();
        private static readonly Dictionary<int, CountrySO> _countries = new();
        private static readonly Dictionary<int, NamePoolSO> _namePools = new(); // keyed by countryId
        private static readonly Dictionary<int, LeagueConfigSO> _leagueConfigs = new();
        private static readonly List<FacilityLevelSO> _facilityLevels = new(); // keyed by (type, level)
        private static readonly Dictionary<int, FormationSO> _formations = new();
        private static readonly Dictionary<int, PlayerRoleSO> _playerRoles = new();
        private static readonly Dictionary<int, InjuryTypeSO> _injuryTypes = new();
        private static GameBalanceSO _gameBalance;
        private static LocalizationSO _localizationData;

        public static GameBalanceSO GameBalance => _gameBalance;
        public static LocalizationSO LocalizationData => _localizationData;

        public static void LoadAll()
        {
            Clear();
            foreach (var t in Resources.LoadAll<TraitSO>(string.Empty))
                _traits[t.id] = t;
            foreach (var p in Resources.LoadAll<PositionSO>(string.Empty))
                _positions[p.id] = p;
            foreach (var c in Resources.LoadAll<CountrySO>(string.Empty))
                _countries[c.id] = c;
            foreach (var n in Resources.LoadAll<NamePoolSO>(string.Empty))
                _namePools[n.countryId] = n;
            foreach (var l in Resources.LoadAll<LeagueConfigSO>(string.Empty))
                _leagueConfigs[l.id] = l;
            foreach (var f in Resources.LoadAll<FacilityLevelSO>(string.Empty))
                _facilityLevels.Add(f);
            foreach (var f in Resources.LoadAll<FormationSO>(string.Empty))
                _formations[f.id] = f;
            foreach (var r in Resources.LoadAll<PlayerRoleSO>(string.Empty))
                _playerRoles[r.id] = r;
            foreach (var i in Resources.LoadAll<InjuryTypeSO>(string.Empty))
                _injuryTypes[i.id] = i;
            _gameBalance = Resources.LoadAll<GameBalanceSO>(string.Empty).FirstOrDefault();
            _localizationData = Resources.LoadAll<LocalizationSO>(string.Empty).FirstOrDefault();
        }

        public static void Clear()
        {
            _traits.Clear();
            _positions.Clear();
            _countries.Clear();
            _namePools.Clear();
            _leagueConfigs.Clear();
            _facilityLevels.Clear();
            _formations.Clear();
            _playerRoles.Clear();
            _injuryTypes.Clear();
            _gameBalance = null;
            _localizationData = null;
        }

        // Register* : 테스트 / 디버그용 in-memory 등록.
        public static void Register(TraitSO trait) => _traits[trait.id] = trait;

        public static void Register(PositionSO position) => _positions[position.id] = position;

        public static void Register(CountrySO country) => _countries[country.id] = country;

        public static void Register(NamePoolSO namePool) =>
            _namePools[namePool.countryId] = namePool;

        public static void Register(LeagueConfigSO leagueConfig) =>
            _leagueConfigs[leagueConfig.id] = leagueConfig;

        public static void Register(FacilityLevelSO facilityLevel) =>
            _facilityLevels.Add(facilityLevel);

        public static void Register(FormationSO formation) => _formations[formation.id] = formation;

        public static void Register(PlayerRoleSO role) => _playerRoles[role.id] = role;

        public static void Register(InjuryTypeSO injuryType) =>
            _injuryTypes[injuryType.id] = injuryType;

        public static void Register(GameBalanceSO gameBalance) => _gameBalance = gameBalance;

        // Lookup
        public static TraitSO GetTrait(int id) => _traits.TryGetValue(id, out var v) ? v : null;

        public static PositionSO GetPosition(int id) =>
            _positions.TryGetValue(id, out var v) ? v : null;

        public static CountrySO GetCountry(int id) =>
            _countries.TryGetValue(id, out var v) ? v : null;

        public static NamePoolSO GetNamePool(int countryId) =>
            _namePools.TryGetValue(countryId, out var v) ? v : null;

        public static LeagueConfigSO GetLeagueConfig(int id) =>
            _leagueConfigs.TryGetValue(id, out var v) ? v : null;

        public static FacilityLevelSO GetFacilityLevel(FacilityType type, int level)
        {
            foreach (var f in _facilityLevels)
            {
                if (f.facilityType == type && f.level == level)
                    return f;
            }
            return null;
        }

        public static FormationSO GetFormation(int id) =>
            _formations.TryGetValue(id, out var v) ? v : null;

        public static PlayerRoleSO GetPlayerRole(int id) =>
            _playerRoles.TryGetValue(id, out var v) ? v : null;

        public static InjuryTypeSO GetInjuryType(int id) =>
            _injuryTypes.TryGetValue(id, out var v) ? v : null;

        // 전체 컬렉션 — 일부 시스템(가챠 등)에서 트레잇 풀 순회 시 사용.
        public static IEnumerable<TraitSO> AllTraits => _traits.Values;
        public static IEnumerable<PositionSO> AllPositions => _positions.Values;
        public static IEnumerable<CountrySO> AllCountries => _countries.Values;
        public static IEnumerable<FormationSO> AllFormations => _formations.Values;
        public static IEnumerable<PlayerRoleSO> AllPlayerRoles => _playerRoles.Values;
        public static IEnumerable<InjuryTypeSO> AllInjuryTypes => _injuryTypes.Values;
    }
}
