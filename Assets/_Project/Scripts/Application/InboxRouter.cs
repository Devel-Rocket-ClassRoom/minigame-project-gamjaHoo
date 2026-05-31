// InboxRouter.cs
// V1.0 — V0.5 EventBus 이벤트 10종을 GameState.inbox InboxItem 생성으로 흡수.
// design-decisions.md #66 / algorithms.md V1.0-7.3.
//
// 호출:
//   GameInitializer.NewGame 직후 (신규 게임)
//   SaveSystem.Load 직후 (세이브 로드)
// Wire(state) 는 멱등 — 재호출 시 기존 핸들러 정리 후 새로 구독. 이를 통해:
//   1. 로드 후 새 state 로 갱신 (이전 state 캡처한 람다 제거)
//   2. 호출자가 Wire 여러 번 불러도 핸들러 누적 X
//
// Stateless 원칙 (design-decisions.md #3) 의 인프라성 예외 — GameTime / EventBus 패턴.
// 정적 _unsubscribers 보유는 EventBus 어댑터 성격으로 정당화. 테스트 격리 시 Unwire() 호출.

using System;
using System.Collections.Generic;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class InboxRouter
    {
        private static readonly List<Action> _unsubscribers = new();

        public static void Wire(GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            Unwire(); // 기존 구독 정리 (멱등성)

            // ── Morale ───────────────────────────────────────────────────
            Subscribe<PromiseCreatedEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.Medium,
                    "inbox_promise_created_fmt",
                    new Dictionary<string, string> { { "id", e.promiseId.ToString() } }
                )
            );

            Subscribe<PromiseFulfilledEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.Low,
                    "inbox_promise_fulfilled_fmt",
                    new Dictionary<string, string> { { "id", e.promiseId.ToString() } }
                )
            );

            Subscribe<PromiseBrokenEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.High,
                    "inbox_promise_broken_fmt",
                    new Dictionary<string, string> { { "id", e.promiseId.ToString() } }
                )
            );

            Subscribe<PromiseDeadlineApproachingEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.Medium,
                    "inbox_promise_deadline_fmt",
                    new Dictionary<string, string>
                    {
                        { "id", e.promiseId.ToString() },
                        { "days", e.daysRemaining.ToString() },
                    }
                )
            );

            Subscribe<TransferRequestEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.High,
                    "inbox_transfer_request_fmt",
                    new Dictionary<string, string> { { "playerId", e.playerId.ToString() } },
                    action: InboxAction.OpenDialog,
                    target: "TransferRequestDialog"
                )
            );

            // ── Transfer ─────────────────────────────────────────────────
            // CounterOffer → 강제 NegotiationScene 라우팅 폐기 (design-decisions.md #66)
            Subscribe<OfferRespondedEvent>(e =>
            {
                var offer = state.activeOffers.Find(o => o.id == e.offerId);
                if (
                    offer != null
                    && offer.toClubId == state.userClubId
                    && e.newStatus == OfferStatus.CounterOffer
                )
                {
                    AddInbox(
                        state,
                        InboxCategory.Transfer,
                        InboxPriority.RequiresAction,
                        "inbox_counter_offer_fmt",
                        new Dictionary<string, string> { { "offerId", e.offerId.ToString() } },
                        deadline: state.currentDate.AddDays(7),
                        action: InboxAction.OpenScene,
                        target: "NegotiationScene"
                    );
                }
            });

            Subscribe<ContractRenewedEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Transfer,
                    InboxPriority.Low,
                    "inbox_contract_renewed_fmt",
                    new Dictionary<string, string> { { "playerId", e.playerId.ToString() } }
                )
            );

            Subscribe<ContractRenewalRejectedEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Transfer,
                    InboxPriority.High,
                    "inbox_contract_rejected_fmt",
                    new Dictionary<string, string> { { "playerId", e.playerId.ToString() } }
                )
            );

            // ── Youth ────────────────────────────────────────────────────
            // YouthIntakeAvailableEvent: 게임 정지 제거 → InboxItem 전환 (design-decisions.md #66 Q3)
            Subscribe<YouthIntakeAvailableEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Youth,
                    InboxPriority.RequiresAction,
                    "inbox_youth_intake_fmt",
                    new Dictionary<string, string> { { "clubId", e.clubId.ToString() } },
                    action: InboxAction.OpenScene,
                    target: "YouthScene"
                )
            );

            // YouthPromotionSuggestedEvent: InboxItem 만 — 유저 승인 패턴 (B.2 Q9)
            Subscribe<YouthPromotionSuggestedEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Youth,
                    InboxPriority.Medium,
                    "inbox_youth_promotion_fmt",
                    new Dictionary<string, string> { { "playerId", e.playerId.ToString() } },
                    action: InboxAction.OpenScene,
                    target: "SquadScene"
                )
            );
        }

        /// <summary>모든 InboxRouter 구독 해제. Wire 재호출 + 테스트 격리에 사용.</summary>
        public static void Unwire()
        {
            foreach (var u in _unsubscribers)
                u();
            _unsubscribers.Clear();
        }

        private static void Subscribe<T>(Action<T> handler)
        {
            EventBus.Subscribe(handler);
            _unsubscribers.Add(() => EventBus.Unsubscribe(handler));
        }

        private static void AddInbox(
            GameState state,
            InboxCategory cat,
            InboxPriority pri,
            string titleKey,
            Dictionary<string, string> args = null,
            DateTime? deadline = null,
            InboxAction action = InboxAction.None,
            string target = null
        )
        {
            state.inbox.Add(
                new InboxItem
                {
                    id = state.nextInboxId++,
                    category = cat,
                    priority = pri,
                    createdAt = state.currentDate,
                    deadline = deadline,
                    isRead = false,
                    titleKey = titleKey,
                    titleArgs = args ?? new Dictionary<string, string>(),
                    bodyKey = string.Empty,
                    bodyArgs = new Dictionary<string, string>(),
                    action = action,
                    actionTargetSceneOrDialogId = target ?? string.Empty,
                }
            );
        }
    }
}
