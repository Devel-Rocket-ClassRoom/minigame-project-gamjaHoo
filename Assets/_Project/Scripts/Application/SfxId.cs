// SfxId.cs
// V1.0 SFX 카탈로그 — design-decisions.md #60.
// SoundManager.sfxClips 배열 인덱스 = (int)SfxId 와 1:1 매핑.
// Goal(3) 은 net + crowd 두 클립 레이어 (배열 슬롯 미사용, SoundManager 가 특수 처리).
// Foul(12) 은 Stage Y 추가 — 파울 휘슬 (whistle-match-foul).

namespace FMLite.Application
{
    public enum SfxId
    {
        ButtonClick = 0,
        ButtonHover = 1,
        InboxReceived = 2,
        Goal = 3,
        CardYellow = 4,
        CardRed = 5,
        Injury = 6,
        Substitution = 7,
        MatchKickoff = 8,
        MatchFulltime = 9,
        SaveComplete = 10,
        SeasonSummary = 11,
        Foul = 12,
    }
}
