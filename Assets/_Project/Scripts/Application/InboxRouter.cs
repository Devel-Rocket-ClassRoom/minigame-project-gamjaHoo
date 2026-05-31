// InboxRouter.cs
// V1.0 — V0.5 EventBus 이벤트 10종을 GameState.inbox InboxItem 생성으로 흡수.
// design-decisions.md #66 / algorithms.md V1.0-7.3.
//
// 호출: GameInitializer.NewGame 이후 + SaveSystem.Load 이후 호출자(UI)가 Wire(state) 호출.
// 테스트: 명시적으로 Wire(state) 호출 후 이벤트 발행 → inbox 확인.
//
// Stateless (design-decisions.md #3) — state 를 파라미터로 받아 변경.

using System;
using System.Collections.Generic;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class InboxRouter
    {
        public static void Wire(GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            // ── Morale ───────────────────────────────────────────────────
            EventBus.Subscribe<PromiseCreatedEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.Medium,
                    "inbox_promise_created_fmt",
                    new Dictionary<string, string> { { "id", e.promiseId.ToString() } }
                )
            );

            EventBus.Subscribe<PromiseFulfilledEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.Low,
                    "inbox_promise_fulfilled_fmt",
                    new Dictionary<string, string> { { "id", e.promiseId.ToString() } }
                )
            );

            EventBus.Subscribe<PromiseBrokenEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Morale,
                    InboxPriority.High,
                    "inbox_promise_broken_fmt",
                    new Dictionary<string, string> { { "id", e.promiseId.ToString() } }
                )
            );

            EventBus.Subscribe<PromiseDeadlineApproachingEvent>(e =>
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

            EventBus.Subscribe<TransferRequestEvent>(e =>
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
            EventBus.Subscribe<OfferRespondedEvent>(e =>
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

            EventBus.Subscribe<ContractRenewedEvent>(e =>
                AddInbox(
                    state,
                    InboxCategory.Transfer,
                    InboxPriority.Low,
                    "inbox_contract_renewed_fmt",
                    new Dictionary<string, string> { { "playerId", e.playerId.ToString() } }
                )
            );

            EventBus.Subscribe<ContractRenewalRejectedEvent>(e =>
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
            EventBus.Subscribe<YouthIntakeAvailableEvent>(e =>
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
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(e =>
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
