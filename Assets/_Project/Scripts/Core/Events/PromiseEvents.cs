// PromiseEvents.cs
// V1.0 G.2 — Promise 라이프사이클 이벤트.
// event-bus-catalog.md / design-decisions.md #43.

namespace FMLite.Core
{
    // PromiseSystem.Create* 헬퍼가 신규 Promise 등록 직후 발행. UI 인박스 알림용.
    public class PromiseCreatedEvent
    {
        public int promiseId;
    }

    // PromiseSystem.CheckProgress 가 deadline 도래 + 조건 충족 시 발행. MoraleSystem.OnPromiseFulfilled 별도 직접 호출.
    public class PromiseFulfilledEvent
    {
        public int promiseId;
    }

    // PromiseSystem.CheckProgress 가 deadline 도래 + 조건 미충족 시 발행. MoraleSystem.OnPromiseBroken 별도 직접 호출.
    public class PromiseBrokenEvent
    {
        public int promiseId;
    }
}
