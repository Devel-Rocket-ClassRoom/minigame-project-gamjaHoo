// ScoutingVisibility.cs
// V0.5 E.3 — 검색 결과 가시성 판정 + 정성적 라벨 5단계.
// 명세: design-decisions.md #46 (이분법 + 정성적 라벨) / v0.5-plan.md §3.7.4.
//
// 책임:
//   - IsScouted(userClub, playerId, isDebugMode) → 정확 노출 여부 판정
//   - GetTier(value, max) → 5단계 라벨 (VeryHigh / High / Average / Low / VeryLow)
//   - GetTierLocalizationKey(tier) → key 기반 표시 (UI 가 Localization.Get 호출)
//
// 디버그 모드: 명단 무관 모두 정확 노출.
// 자기 구단 선수: ScoutingSystem 이 매주 자동 등록 (scoutLevel=100) — 그대로 명단 ∈ 처리.

using FMLite.Domain;

namespace FMLite.Application
{
    public enum ScoutTier
    {
        VeryHigh,
        High,
        Average,
        Low,
        VeryLow,
    }

    public static class ScoutingVisibility
    {
        // 정확 노출 가능 여부.
        public static bool IsScouted(Club userClub, int playerId, bool isDebugMode = false)
        {
            if (isDebugMode)
                return true;
            if (userClub?.scoutingKnowledge == null)
                return false;
            return userClub.scoutingKnowledge.ContainsKey(playerId);
        }

        // 비율 (value / max) 기반 5단계 라벨.
        // CA: max=200, stat: max=100, hidden: max=100 (단 hidden 은 명단 ∉ 시 표시 X — UI 가 처리).
        public static ScoutTier GetTier(int value, int max)
        {
            if (max <= 0)
                return ScoutTier.VeryLow;
            float ratio = (float)value / max;
            if (ratio >= 0.85f)
                return ScoutTier.VeryHigh;
            if (ratio >= 0.65f)
                return ScoutTier.High;
            if (ratio >= 0.45f)
                return ScoutTier.Average;
            if (ratio >= 0.25f)
                return ScoutTier.Low;
            return ScoutTier.VeryLow;
        }

        // Localization 키 반환 — UI 가 Localization.Get(key) 호출.
        public static string GetTierLocalizationKey(ScoutTier tier) =>
            tier switch
            {
                ScoutTier.VeryHigh => "scout_tier_very_high",
                ScoutTier.High => "scout_tier_high",
                ScoutTier.Average => "scout_tier_average",
                ScoutTier.Low => "scout_tier_low",
                ScoutTier.VeryLow => "scout_tier_very_low",
                _ => "scout_tier_average",
            };
    }
}
