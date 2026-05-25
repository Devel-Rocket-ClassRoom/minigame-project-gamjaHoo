// InjuryTypeSO.cs
// 부상 유형 카탈로그 에셋. InjurySystem 에서 id 기반 조회.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "InjuryType", menuName = "FM-Lite/InjuryTypeSO")]
    public class InjuryTypeSO : ScriptableObject
    {
        public int id;
        public string displayName;

        public int minDays; // 최소 회복 기간
        public int maxDays; // 최대 회복 기간

        [Range(0f, 10f)]
        public float weight = 1.0f; // 발생 확률 가중치 (높을수록 자주 발생)
    }
}
