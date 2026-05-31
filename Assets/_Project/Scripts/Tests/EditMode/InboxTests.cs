// InboxTests.cs
// Task A.1 Sub-B DoD:
//   T1  직렬화 라운드트립 — InboxItem nullable deadline, Dictionary, enum 보존
//   T2  Router: PromiseCreatedEvent → InboxItem(Morale/Medium)
//   T3  Router: TransferRequestEvent → InboxItem(Morale/High/OpenDialog)
//   T4  Router: OfferRespondedEvent(CounterOffer, toClub=user) → Transfer/RequiresAction + deadline
//   T5  Router: OfferRespondedEvent(CounterOffer, toClub≠user) → 생성 안 됨
//   T6  Router: YouthIntakeAvailableEvent → Youth/RequiresAction
//   T7  Router: ContractRenewalRejectedEvent → Transfer/High
//   T8  SeasonEndProcessor — isRead=true 삭제 / isRead=false 유지
//   T9  nextInboxId 단조증가 확인

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class InboxTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _date = new DateTime(2026, 9, 1);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            EventBus.Clear();
            InboxRouter.Unwire(); // 이전 테스트 잔재 정리
        }

        [TearDown]
        public void TearDown()
        {
            InboxRouter.Unwire();
            EventBus.Clear();
        }

        // ── T1. 직렬화 라운드트립 ─────────────────────────────────────

        [Test]
        public void T1_InboxItem_SerializesAndDeserializes()
        {
            var state = NewState();
            state.inbox.Add(new InboxItem
            {
                id = 1,
                category = InboxCategory.Transfer,
                priority = InboxPriority.RequiresAction,
                createdAt = _date,
                deadline = _date.AddDays(7),
                isRead = false,
                titleKey = "inbox_counter_offer_fmt",
                titleArgs = new Dictionary<string, string> { { "offerId", "99" } },
                bodyKey = string.Empty,
                bodyArgs = new Dictionary<string, string>(),
                action = InboxAction.OpenScene,
                actionTargetSceneOrDialogId = "NegotiationScene",
            });
            state.nextInboxId = 2;

            var json = JsonConvert.SerializeObject(state);
            var loaded = JsonConvert.DeserializeObject<GameState>(json);

            Assert.AreEqual(1, loaded.inbox.Count, "T1: inbox 1개");
            var item = loaded.inbox[0];
            Assert.AreEqual(InboxCategory.Transfer, item.category, "T1: category");
            Assert.AreEqual(InboxPriority.RequiresAction, item.priority, "T1: priority");
            Assert.AreEqual(_date.AddDays(7), item.deadline, "T1: deadline nullable");
            Assert.AreEqual("99", item.titleArgs["offerId"], "T1: titleArgs Dictionary");
            Assert.AreEqual(InboxAction.OpenScene, item.action, "T1: action enum");
            Assert.AreEqual("NegotiationScene", item.actionTargetSceneOrDialogId, "T1: target");
            Assert.AreEqual(2, loaded.nextInboxId, "T1: nextInboxId");
        }

        // ── T2. PromiseCreatedEvent → Morale/Medium ─────────────────

        [Test]
        public void T2_PromiseCreated_AddsInboxMoraleMedium()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(new PromiseCreatedEvent { promiseId = 5 });

            Assert.AreEqual(1, state.inbox.Count, "T2: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Morale, item.category, "T2: category Morale");
            Assert.AreEqual(InboxPriority.Medium, item.priority, "T2: priority Medium");
            Assert.AreEqual("inbox_promise_created_fmt", item.titleKey, "T2: titleKey");
            Assert.AreEqual("5", item.titleArgs["id"], "T2: args id");
            Assert.IsNull(item.deadline, "T2: deadline null");
            Assert.AreEqual(InboxAction.None, item.action, "T2: action None");
        }

        // ── T3. TransferRequestEvent → Morale/High/OpenDialog ───────

        [Test]
        public void T3_TransferRequest_AddsInboxMoraleHighDialog()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(new TransferRequestEvent { playerId = 77 });

            Assert.AreEqual(1, state.inbox.Count, "T3: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Morale, item.category, "T3: Morale");
            Assert.AreEqual(InboxPriority.High, item.priority, "T3: High");
            Assert.AreEqual(InboxAction.OpenDialog, item.action, "T3: OpenDialog");
            Assert.AreEqual("TransferRequestDialog", item.actionTargetSceneOrDialogId, "T3: target");
        }

        // ── T4. CounterOffer(toClub=user) → Transfer/RequiresAction + deadline ─

        [Test]
        public void T4_CounterOffer_ToUserClub_AddsTransferRequiresAction()
        {
            var state = NewState(userClubId: 10);
            var offer = new TransferOffer
            {
                id = 1,
                fromClubId = 20,
                toClubId = 10, // 유저가 구매자(toClub)
                playerId = 50,
                status = OfferStatus.CounterOffer,
            };
            state.activeOffers.Add(offer);
            InboxRouter.Wire(state);

            EventBus.Publish(new OfferRespondedEvent { offerId = 1, newStatus = OfferStatus.CounterOffer });

            Assert.AreEqual(1, state.inbox.Count, "T4: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Transfer, item.category, "T4: Transfer");
            Assert.AreEqual(InboxPriority.RequiresAction, item.priority, "T4: RequiresAction");
            Assert.AreEqual(_date.AddDays(7), item.deadline, "T4: deadline +7일");
            Assert.AreEqual(InboxAction.OpenScene, item.action, "T4: OpenScene");
            Assert.AreEqual("NegotiationScene", item.actionTargetSceneOrDialogId, "T4: target");
        }

        // ── T5. CounterOffer(toClub≠user) → 생성 안 됨 ──────────────

        [Test]
        public void T5_CounterOffer_ToOtherClub_NoInboxItem()
        {
            var state = NewState(userClubId: 10);
            var offer = new TransferOffer
            {
                id = 1,
                fromClubId = 10, // 유저가 판매자(fromClub) — 이 분기는 인박스 대상 아님
                toClubId = 20,
                playerId = 50,
                status = OfferStatus.CounterOffer,
            };
            state.activeOffers.Add(offer);
            InboxRouter.Wire(state);

            EventBus.Publish(new OfferRespondedEvent { offerId = 1, newStatus = OfferStatus.CounterOffer });

            Assert.AreEqual(0, state.inbox.Count, "T5: 생성 안 됨");
        }

        // ── T6. YouthIntakeAvailableEvent → Youth/RequiresAction ─────

        [Test]
        public void T6_YouthIntake_AddsYouthRequiresAction()
        {
            var state = NewState(userClubId: 1);
            InboxRouter.Wire(state);

            EventBus.Publish(new YouthIntakeAvailableEvent { intakeId = 1, clubId = 1 });

            Assert.AreEqual(1, state.inbox.Count, "T6: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Youth, item.category, "T6: Youth");
            Assert.AreEqual(InboxPriority.RequiresAction, item.priority, "T6: RequiresAction");
            Assert.AreEqual(InboxAction.OpenScene, item.action, "T6: OpenScene");
            Assert.AreEqual("YouthScene", item.actionTargetSceneOrDialogId, "T6: YouthScene");
        }

        // ── T7. ContractRenewalRejectedEvent → Transfer/High ─────────

        [Test]
        public void T7_ContractRenewalRejected_AddsTransferHigh()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(new ContractRenewalRejectedEvent { playerId = 33 });

            Assert.AreEqual(1, state.inbox.Count, "T7: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Transfer, item.category, "T7: Transfer");
            Assert.AreEqual(InboxPriority.High, item.priority, "T7: High");
            Assert.AreEqual("33", item.titleArgs["playerId"], "T7: playerId arg");
        }

        // ── T8. SeasonEndProcessor — 읽은 항목 삭제 ──────────────────

        [Test]
        public void T8_SeasonEnd_RemovesReadItems_KeepsUnread()
        {
            var state = NewState();
            state.inbox.Add(new InboxItem { id = 1, isRead = true, titleKey = "a" });
            state.inbox.Add(new InboxItem { id = 2, isRead = false, titleKey = "b" });
            state.inbox.Add(new InboxItem { id = 3, isRead = true, titleKey = "c" });

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(1, state.inbox.Count, "T8: 미읽은 1개만 남음");
            Assert.AreEqual(2, state.inbox[0].id, "T8: id=2 유지");
        }

        // ── T9. nextInboxId 단조증가 ──────────────────────────────────

        [Test]
        public void T9_NextInboxId_Monotonic()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(new PromiseCreatedEvent { promiseId = 1 });
            EventBus.Publish(new PromiseFulfilledEvent { promiseId = 1 });
            EventBus.Publish(new PromiseBrokenEvent { promiseId = 2 });

            Assert.AreEqual(3, state.inbox.Count, "T9: 3개 생성");
            Assert.AreEqual(1, state.inbox[0].id, "T9: id=1");
            Assert.AreEqual(2, state.inbox[1].id, "T9: id=2");
            Assert.AreEqual(3, state.inbox[2].id, "T9: id=3");
            Assert.AreEqual(4, state.nextInboxId, "T9: nextInboxId=4");
        }

        // ── T10. Wire 멱등성 — 두 번 호출 시 핸들러 중복 X ──────────

        [Test]
        public void T10_Wire_Idempotent_NoDuplicateHandlers()
        {
            var state = NewState();
            InboxRouter.Wire(state);
            InboxRouter.Wire(state); // 두 번째 호출 — 기존 핸들러 정리 후 새로 구독

            EventBus.Publish(new PromiseCreatedEvent { promiseId = 1 });

            Assert.AreEqual(1, state.inbox.Count, "T10: 중복 없이 1개만 생성");
        }

        // ── T11. Wire(state2) 후 state1 영향 없음 ────────────────────

        [Test]
        public void T11_Wire_NewState_OldStateNotAffected()
        {
            var state1 = NewState();
            InboxRouter.Wire(state1);

            var state2 = NewState();
            InboxRouter.Wire(state2); // state2 로 갱신

            EventBus.Publish(new PromiseCreatedEvent { promiseId = 99 });

            Assert.AreEqual(0, state1.inbox.Count, "T11: state1 미영향");
            Assert.AreEqual(1, state2.inbox.Count, "T11: state2 만 추가");
        }

        // ── T12. Unwire 후 이벤트 발화 시 추가 안 됨 ─────────────────

        [Test]
        public void T12_Unwire_NoNewInboxItems()
        {
            var state = NewState();
            InboxRouter.Wire(state);
            InboxRouter.Unwire();

            EventBus.Publish(new PromiseCreatedEvent { promiseId = 1 });

            Assert.AreEqual(0, state.inbox.Count, "T12: Unwire 후 미생성");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState NewState(int userClubId = -1) =>
            new GameState
            {
                currentDate = _date,
                randomSeed = 42,
                userClubId = userClubId,
                nextPlayerId = 1,
            };
    }
}
