// FormationMatchupSO.cs
// V1.0 G.4 — 전술(포메이션) 상성 매트릭스 (algorithms.md V1.0-9, #474/#478).
// Formation × Formation → home strength 곱셈 보너스. 비대칭 (홈/원정 자체 영향) — away 는 별도 entry.
// MatchSimulator: homeTeamMod ×= Get(homeFormationId, awayFormationId), awayTeamMod ×= Get(awayFormationId, homeFormationId).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    [Serializable]
    public class MatchupEntry
    {
        public int homeFormationId;
        public int awayFormationId;
        public float homeBonus = 1.0f; // 1.05 = home 5% strength 보너스
    }

    [CreateAssetMenu(fileName = "FormationMatchup", menuName = "FM-Lite/FormationMatchupSO")]
    public class FormationMatchupSO : ScriptableObject
    {
        public List<MatchupEntry> matchups = new List<MatchupEntry>();

        // (homeId vs awayId) → homeBonus. 미정의 / 동일 포메이션 → 1.0 (무영향).
        public float Get(int homeFormationId, int awayFormationId)
        {
            if (matchups != null)
            {
                for (int i = 0; i < matchups.Count; i++)
                {
                    var m = matchups[i];
                    if (
                        m.homeFormationId == homeFormationId
                        && m.awayFormationId == awayFormationId
                    )
                        return m.homeBonus;
                }
            }
            return 1.0f;
        }
    }
}
