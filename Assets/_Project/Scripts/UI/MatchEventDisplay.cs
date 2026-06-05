// MatchEventDisplay.cs
// V1.0 Stage J.2 / J.5 — 매치 텍스트 이벤트의 표시/사운드 분류 (테스트 가능 순수 로직).
//
// J.2 (viewMode 폐기, 단일 모드): MatchSimulator 는 collectEvents 시 이미 핵심 이벤트만 emit 한다
// (성공 패스/태클/인터셉트/클리어런스 등은 통계만 누적, events 에 안 넣음). 본 분류기는 그 계약을
// UI 에서 명시적으로 고정 — 사소한(통계 전용) 타입이 혹시 흘러들어도 텍스트로 노출하지 않는다.
// v1.0-plan.md §3.6.6 / §3.9 / Q7.
//
// J.5 (SFX wire-up): 이벤트 타입 → SfxId 매핑. 실제 재생/에셋은 Stage Y. (design-decisions.md #60)

using FMLite.Application;
using FMLite.Domain;

namespace FMLite.UI
{
    public static class MatchEventDisplay
    {
        /// <summary>
        /// 통계 전용(텍스트로 노출하지 않는) 이벤트 타입. §3.6.6 "사소한 이벤트는 통계만".
        /// MatchSimulator 는 이들을 events 에 넣지 않지만, 계약을 UI 에서 명시적으로 보장.
        /// </summary>
        public static bool ShouldShowText(MatchEventType type)
        {
            switch (type)
            {
                case MatchEventType.PassCompleted:
                case MatchEventType.ShotOnTarget:
                case MatchEventType.ShotBlocked:
                case MatchEventType.Tackle:
                case MatchEventType.Interception:
                case MatchEventType.Clearance:
                case MatchEventType.ThrowIn: // 일반 스로인 = 사소한 flavor (LongThrow 세트피스만 표시)
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 이벤트 타입 → SFX. 대응 SFX 가 없으면 null. v1.0-tasks.md J.5 / Y.5
        /// (Goal → Goal / Card → CardYellow|CardRed / Injury / Substitution / KickOff / FullTime).
        /// </summary>
        public static SfxId? SfxFor(MatchEventType type)
        {
            switch (type)
            {
                case MatchEventType.Goal:
                case MatchEventType.PenaltyGoal:
                    return SfxId.Goal;
                case MatchEventType.YellowCard:
                case MatchEventType.SecondYellow:
                    return SfxId.CardYellow;
                case MatchEventType.RedCard:
                    return SfxId.CardRed;
                case MatchEventType.PenaltyAwarded:
                    // 파울 휘슬은 일반 파울(매분 다발)엔 안 울리고 — 페널티(드묾·중대) 에만.
                    // 일반 파울은 무음, 카드 동반 파울만 카드 휘슬로 표현.
                    return SfxId.Foul;
                case MatchEventType.Injury:
                    return SfxId.Injury;
                case MatchEventType.Substitution:
                    return SfxId.Substitution;
                case MatchEventType.KickOff:
                case MatchEventType.ExtraTimeKickOff:
                    return SfxId.MatchKickoff;
                case MatchEventType.FullTime:
                case MatchEventType.ExtraTimeEnd:
                    return SfxId.MatchFulltime;
                default:
                    return null;
            }
        }
    }
}
