// StatSnapshot.cs
// V1.0 — 월별 stats 스냅샷. Player.growthHistory 에 누적.
// design-decisions.md #68 / algorithms.md V1.0-11.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class StatSnapshot
    {
        public int year;
        public int month;
        public Stats stats; // Stats.Clone() — GrowthSystem.Tick 시작 시 캡처
    }
}
