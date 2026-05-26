// InterviewType.cs
// 면담 종류 (design-decisions.md #43 / algorithms.md V1.0-6 OnInterview).
// V1.0 G.1: Praise / Criticize 만 본격 처리. PromisePlaytime / PromiseRenewal 은 G.2 Promise 시스템 도입 시 본격 활용 (스텁).

namespace FMLite.Domain
{
    public enum InterviewType
    {
        Praise = 0, // "현재 성과 칭찬" — 즉시 Morale +5
        Criticize = 1, // "더 노력해야 한다" — Morale -3 (professionalism 높으면 완화)
        PromisePlaytime = 2, // PlaytimeAgreement Promise 생성 (G.2 도입)
        PromiseRenewal = 3, // Renewal Promise 생성 (G.2 도입)
    }
}
