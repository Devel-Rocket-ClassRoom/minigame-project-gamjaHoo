// Enums.cs
// 도메인 enum 정의 모음. class-diagram.md 명세 기준.

namespace FMLite.Domain
{
    public enum Position
    {
        GK,
        CB, LB, RB, WB,
        DM, CM, AM, LM, RM,
        LW, RW, ST, CF,
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
        Negotiating,
        Accepted,
        Rejected,
        Completed,
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
}
