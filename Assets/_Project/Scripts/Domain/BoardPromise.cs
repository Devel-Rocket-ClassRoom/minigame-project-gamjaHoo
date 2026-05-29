// BoardPromise.cs
// 보드와의 약속. SeasonState.boardPromises 에서 관리.
// V0.5 M.5: TransferIn (특정 포지션 영입 요구).

using System;

namespace FMLite.Domain
{
    public enum BoardPromiseType
    {
        TransferIn, // 특정 포지션 영입 요구
    }

    public enum BoardPromiseStatus
    {
        PendingReview, // 아직 매니저가 수락/거절 안 함
        Active, // 수락됨 — 이행 중
        Fulfilled, // 이행 완료
        Broken, // 거절 또는 미이행
    }

    [Serializable]
    public class BoardPromise
    {
        public int id;
        public BoardPromiseType type;
        public BoardPromiseStatus status;
        public Position targetPosition; // TransferIn: 요구 포지션
        public DateTime deadline;
        public DateTime madeAt;
    }
}
