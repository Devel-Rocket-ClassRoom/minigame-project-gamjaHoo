// Enums.cs
// 도메인 enum 정의 모음. class-diagram.md 명세 기준.

namespace FMLite.Domain
{
    public enum Position
    {
        GK,
        CB,
        LB,
        RB,
        WB,
        DM,
        CM,
        AM,
        LM,
        RM,
        LW,
        RW,
        ST,
        CF,
    }

    public enum Foot
    {
        Left,
        Right,
        Both,
    }

    public enum CompetitionType
    {
        League,
        FACup,
        CarabaoCup,
    }

    public enum OfferStatus
    {
        Pending,
        Negotiating, // 구단 승인 후 선수 개인 협상 단계 (design-decisions.md #48)
        CounterOffer, // 판매 구단 역제안 단계
        Accepted,
        Rejected,
        Completed,
    }

    // V0.5 K.1 — RespondToCounterOffer 유저 응답 옵션.
    public enum CounterResponse
    {
        Accept, // counterAmount 수락 → Accepted
        Reject, // 협상 결렬 → Rejected
        ReCounter, // 새 금액 역제안 → AiRespondToOffer 재호출
    }

    public enum PlayerOrigin
    {
        InitialRoster,
        YouthIntake,
        Regen,
    }

    public enum CupTarget
    {
        None,
        GroupStage,
        Round16,
        QuarterFinal,
        SemiFinal,
        Final,
        Win,
    }

    public enum FacilityType
    {
        Scout,
        Training,
        Youth, // V0.1 호환 유지 — V0.5에서 YouthCoach/YouthRecruitment/YouthFacility 로 분리됨
        YouthCoach, // 유스 평균 PA + 트레잇 가중치 (design-decisions.md #49)
        YouthRecruitment, // 유스 풀 크기 + 인스펙션 빈도
        YouthFacility, // 유스 성장률 + 콜업 적응
        Medical, // 부상 회복 속도 + 발생률 ↓
        Stadium, // 입장료 수입 + 명성 가산
        Gym, // 피지컬 성장률 + 부상 회복
    }
}
