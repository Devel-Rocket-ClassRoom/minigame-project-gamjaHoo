// BgmId.cs
// V1.0 BGM 카탈로그 — design-decisions.md #60 3곡.
// SoundManager.bgmClips 배열 인덱스 = (int)BgmId 와 1:1 매핑.

namespace FMLite.Application
{
    public enum BgmId
    {
        MainMenu = 0,
        Dashboard = 1,
        Match = 2,
    }
}
