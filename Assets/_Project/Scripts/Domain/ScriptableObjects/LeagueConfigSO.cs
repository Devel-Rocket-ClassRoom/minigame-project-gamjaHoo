// LeagueConfigSO.cs
// 리그 구조 설정 (팀 수 / 강등 수 / 구단당 선수 수).

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "LeagueConfig", menuName = "FM-Lite/League Config")]
    public class LeagueConfigSO : ScriptableObject
    {
        public int id;
        public string displayName;          // "Premier League"
        public string countryCode;          // "ENG"
        public int clubCount = 20;
        public int relegationCount = 3;
        public int playersPerClub = 25;
    }
}
