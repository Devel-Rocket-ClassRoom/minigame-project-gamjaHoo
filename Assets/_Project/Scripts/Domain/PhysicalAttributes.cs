// PhysicalAttributes.cs
// V1.0 신체 조건 도메인 (design-decisions.md #67 / algorithms.md V1.0-8.1).
// Player.physical 에 컴포지션으로 포함.
// 주의: Player.stats.physical (PhysicalStats — pace/agility 등 능력치) 과 다른 클래스.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class PhysicalAttributes
    {
        public int height; // cm, [165, 205]
        public int weight; // kg, [60, 100]
        public Foot preferredFoot; // Left / Right / Both
        public int weakFootAbility; // 1-5 (별점, 약발 능숙도)
    }
}
