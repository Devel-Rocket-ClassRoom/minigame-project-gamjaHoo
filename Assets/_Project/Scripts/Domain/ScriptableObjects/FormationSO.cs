// FormationSO.cs
// 팀 포메이션 카탈로그 에셋 (design-decisions.md #32 / #45).
// V0.1 GameBalanceSO.FormationConfig → V1.0 FormationSO 추출.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "Formation", menuName = "FM-Lite/FormationSO")]
    public class FormationSO : ScriptableObject
    {
        public int id;
        public string displayName; // e.g. "4-4-2"

        // 11개 슬롯 포지션 (index 0 = GK ~ index 10 = 최전방)
        public Position[] slotPositions = new Position[11];
    }
}
