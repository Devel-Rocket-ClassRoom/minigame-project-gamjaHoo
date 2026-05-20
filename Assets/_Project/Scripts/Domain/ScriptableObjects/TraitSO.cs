// TraitSO.cs
// 선수 트레잇 정의 (늦깎이형 / 빅매치형 등). 효과는 시스템 로직에서 id 기반 분기.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "Trait", menuName = "FM-Lite/Trait")]
    public class TraitSO : ScriptableObject
    {
        public int id;
        public string displayName;

        [TextArea(2, 4)]
        public string description;
        public float weight = 1.0f; // PlayerGenerator 부여 확률 가중치

        // 충돌 그룹 (design-decisions.md #25). 같은 그룹의 트레잇은 한 선수에 동시 부여 불가.
        // 0 = 충돌 없음 (기본). e.g. 늦깎이형/조숙형 → 그룹 1.
        public int exclusionGroupId = 0;
    }
}
