// LeagueConfigSO.cs
// 리그 구조 설정 (팀 수 / 강등 수 / 구단당 선수 수 / 구단명 목록).

using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "LeagueConfig", menuName = "FM-Lite/League Config")]
    public class LeagueConfigSO : ScriptableObject
    {
        public int id;
        public string displayName; // "Premier League"
        public string countryCode; // "ENG"
        public int clubCount = 20;
        public int relegationCount = 3;
        public int playersPerClub = 25;

        // 명성 내림차순 (algorithms.md #5 전제). Count 가 clubCount 와 일치해야 함.
        // 부족 시 ClubGenerator 가 $"Club {i+1}" 폴백 + 경고.
        public List<string> clubNames = new List<string>();

        // J.1 — ClubGenerator 가 구단 생성 시 랜덤 선택할 포메이션 풀 (J.7).
        // 비어있으면 ClubGenerator 가 GameBalanceSO.FormationConfig 폴백.
        public List<FormationSO> availableFormations = new List<FormationSO>();
    }
}
