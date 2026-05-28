// FormationSO.cs
// 팀 포메이션 카탈로그 에셋 (design-decisions.md #32 / #45).
// V0.1 GameBalanceSO.FormationConfig → V1.0 FormationSO 추출.
// J.7: formationStyle (0=Solid/1=Balanced/2=Flamboyant) + squadConfig 추가.

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

        // J.7: 구단 명성 기반 가중 추첨 (0=Solid 약체, 1=Balanced 중간, 2=Flamboyant 강팀)
        public int formationStyle = 1;

        // J.7: 25인 스쿼드 분배표 — null 시 GameBalanceSO.formation 폴백
        public FormationConfig squadConfig;
    }
}
