// SfxId.cs
// V1.0 SFX 카탈로그 — design-decisions.md #60 12종.
// SoundManager.sfxClips 배열 인덱스 = (int)SfxId 와 1:1 매핑.

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
    }
}
