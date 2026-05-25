// TraitEffect.cs
// 트레잇 효과 정의 (design-decisions.md #41). TraitSO.effects 에서 참조.

using System;

namespace FMLite.Domain
{
    public enum TraitEffectType
    {
        MatchModifier, // 매치 시뮬 이벤트 가중치 보정
        InjuryRateModifier, // 부상 발생률 곱셈
        GrowthRateModifier, // 성장률 곱셈
    }

    [Serializable]
    public class TraitEffect
    {
        public TraitEffectType type;
        public float value; // 곱셈(×) 또는 가산(+) — 종류별 해석 다름
        public string targetStat; // 선택적: 적용 대상 스탯/시나리오 식별자
    }
}
