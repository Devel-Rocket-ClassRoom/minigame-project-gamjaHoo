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

        // ── T13. Accepted(toClub=user) → Transfer/High ───────────────

        [Test]
        public void T13_OfferAccepted_ToUserClub_AddsTransferHigh()
        {
            var state = NewState(userClubId: 10);
            state.activeOffers.Add(
                new TransferOffer
                {
                    id = 1,
                    fromClubId = 20,
                    toClubId = 10,
                    playerId = 50,
                    status = OfferStatus.Accepted,
                }
            );
            InboxRouter.Wire(state);

            EventBus.Publish(new OfferRespondedEvent { offerId = 1, newStatus = OfferStatus.Accepted });

            Assert.AreEqual(1, state.inbox.Count, "T13: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Transfer, item.category, "T13: Transfer");
            Assert.AreEqual(InboxPriority.High, item.priority, "T13: High");
            Assert.AreEqual("inbox_offer_accepted_fmt", item.titleKey, "T13: titleKey");
            Assert.AreEqual(InboxAction.None, item.action, "T13: action None");
            Assert.IsNull(item.deadline, "T13: deadline 없음");
        }

        // ── T14. Rejected(toClub=user) → Transfer/Medium ─────────────

        [Test]
        public void T14_OfferRejected_ToUserClub_AddsTransferMedium()
        {
            var state = NewState(userClubId: 10);
            state.activeOffers.Add(
                new TransferOffer
                {
                    id = 1,
                    fromClubId = 20,
                    toClubId = 10,
                    playerId = 50,
                    status = OfferStatus.Rejected,
                }
            );
            InboxRouter.Wire(state);

            EventBus.Publish(new OfferRespondedEvent { offerId = 1, newStatus = OfferStatus.Rejected });

            Assert.AreEqual(1, state.inbox.Count, "T14: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Transfer, item.category, "T14: Transfer");
            Assert.AreEqual(InboxPriority.Medium, item.priority, "T14: Medium");
            Assert.AreEqual("inbox_offer_rejected_fmt", item.titleKey, "T14: titleKey");
        }

        // ── T15. Accepted(toClub≠user) → 생성 안 됨 ──────────────────

        [Test]
        public void T15_OfferAccepted_ToOtherClub_NoInboxItem()
        {
            var state = NewState(userClubId: 10);
            state.activeOffers.Add(
                new TransferOffer
                {
                    id = 1,
                    fromClubId = 10,
                    toClubId = 20,
                    playerId = 50,
                    status = OfferStatus.Accepted,
                }
            );
            InboxRouter.Wire(state);

            EventBus.Publish(new OfferRespondedEvent { offerId = 1, newStatus = OfferStatus.Accepted });

            Assert.AreEqual(0, state.inbox.Count, "T15: 유저 무관 오퍼 — 미생성");
        }

        // ── T16. Negotiating(toClub=user) → Transfer/RequiresAction + PlayerNegotiationScene ─

        [Test]
        public void T16_Negotiating_ToUserClub_AddsTransferRequiresAction()
        {
            var state = NewState(userClubId: 10);
            state.activeOffers.Add(
                new TransferOffer
                {
                    id = 1,
                    fromClubId = 20,
                    toClubId = 10,
                    playerId = 50,
                    status = OfferStatus.Negotiating,
                }
            );
            InboxRouter.Wire(state);

            EventBus.Publish(
                new OfferRespondedEvent { offerId = 1, newStatus = OfferStatus.Negotiating }
            );

            Assert.AreEqual(1, state.inbox.Count, "T16: 1개 생성");
            var item = state.inbox[0];
            Assert.AreEqual(InboxCategory.Transfer, item.category, "T16: Transfer");
            Assert.AreEqual(InboxPriority.RequiresAction, item.priority, "T16: RequiresAction");
            Assert.AreEqual("inbox_personal_negotiation_fmt", item.titleKey, "T16: titleKey");
            Assert.AreEqual(InboxAction.OpenScene, item.action, "T16: OpenScene");
            Assert.AreEqual(
                "PlayerNegotiationScene",
                item.actionTargetSceneOrDialogId,
                "T16: target"
            );
            Assert.AreEqual(_date.AddDays(7), item.deadline, "T16: deadline +7일");
        }

        // ════════════════════════════════════════════════════════════
        // R.5 (#76) 인박스 확장 — Tier1 / Tier2 / 추가 라우팅
        // ════════════════════════════════════════════════════════════

        // ── T17. PlayerInjuredEvent (유저 구단) → Match/Medium ─────────

        [Test]
        public void T17_PlayerInjured_UserClub_AddsMatchMedium()
        {
            var state = NewState(userClubId: 10);
            AddPlayer(state, id: 50, clubId: 10);
            InboxRouter.Wire(state);

            EventBus.Publish(new PlayerInjuredEvent { playerId = 50 });

            Assert.AreEqual(1, state.inbox.Count, "T17: 1개 생성");
            Assert.AreEqual(InboxCategory.Match, state.inbox[0].category, "T17: Match");
            Assert.AreEqual(InboxPriority.Medium, state.inbox[0].priority, "T17: Medium");
            Assert.AreEqual("inbox_player_injured_fmt", state.inbox[0].titleKey, "T17: titleKey");
        }

        // ── T18. PlayerInjuredEvent (타 구단) → 생성 안 됨 ────────────

        [Test]
        public void T18_PlayerInjured_OtherClub_NoInboxItem()
        {
            var state = NewState(userClubId: 10);
            AddPlayer(state, id: 50, clubId: 20);
            InboxRouter.Wire(state);

            EventBus.Publish(new PlayerInjuredEvent { playerId = 50 });

            Assert.AreEqual(0, state.inbox.Count, "T18: 타 구단 — 미생성");
        }

        // ── T19. PlayerInjuryRecoveredEvent (유저 구단) → Match/Low ────

        [Test]
        public void T19_PlayerRecovered_UserClub_AddsMatchLow()
        {
            var state = NewState(userClubId: 10);
            AddPlayer(state, id: 50, clubId: 10);
            InboxRouter.Wire(state);

            EventBus.Publish(new PlayerInjuryRecoveredEvent { playerId = 50 });

            Assert.AreEqual(1, state.inbox.Count, "T19: 1개 생성");
            Assert.AreEqual(InboxCategory.Match, state.inbox[0].category, "T19: Match");
            Assert.AreEqual(InboxPriority.Low, state.inbox[0].priority, "T19: Low");
        }

        // ── T20. PlayerStatChangedEvent (유저 유스) → Youth/Low ───────

        [Test]
        public void T20_YouthGrowth_UserYouth_AddsYouthLow()
        {
            var state = NewState(userClubId: 10);
            var club = AddClub(state, id: 10);
            AddPlayer(state, id: 50, clubId: 10);
            club.youthSquadIds.Add(50);
            InboxRouter.Wire(state);

            EventBus.Publish(
                new PlayerStatChangedEvent
                {
                    playerId = 50,
                    statName = "passing",
                    oldValue = 40,
                    newValue = 43,
                }
            );

            Assert.AreEqual(1, state.inbox.Count, "T20: 1개 생성");
            Assert.AreEqual(InboxCategory.Youth, state.inbox[0].category, "T20: Youth");
            Assert.AreEqual(InboxPriority.Low, state.inbox[0].priority, "T20: Low");
            Assert.AreEqual("passing", state.inbox[0].titleArgs["stat"], "T20: stat arg");
        }

        // ── T21. PlayerStatChangedEvent (1군 선수) → 생성 안 됨 ───────

        [Test]
        public void T21_YouthGrowth_SeniorPlayer_NoInboxItem()
        {
            var state = NewState(userClubId: 10);
            AddClub(state, id: 10); // youthSquadIds 비어있음
            AddPlayer(state, id: 50, clubId: 10);
            InboxRouter.Wire(state);

            EventBus.Publish(
                new PlayerStatChangedEvent { playerId = 50, statName = "passing" }
            );

            Assert.AreEqual(0, state.inbox.Count, "T21: 유스 아님 — 미생성");
        }

        // ── T22. AwardWonEvent (유저 구단 선수) → Award/Medium ────────

        [Test]
        public void T22_AwardWon_UserClubPlayer_AddsAwardMedium()
        {
            var state = NewState(userClubId: 10);
            AddPlayer(state, id: 50, clubId: 10);
            InboxRouter.Wire(state);

            EventBus.Publish(
                new AwardWonEvent { awardType = AwardType.TopScorer, playerId = 50 }
            );

            Assert.AreEqual(1, state.inbox.Count, "T22: 1개 생성");
            Assert.AreEqual(InboxCategory.Award, state.inbox[0].category, "T22: Award");
            Assert.AreEqual(InboxPriority.Medium, state.inbox[0].priority, "T22: Medium");
            Assert.AreEqual(
                ((int)AwardType.TopScorer).ToString(),
                state.inbox[0].titleArgs["award"],
                "T22: award arg"
            );
        }

        // ── T23. PlayerUnhappyEvent → Morale/Medium ──────────────────

        [Test]
        public void T23_PlayerUnhappy_AddsMoraleMedium()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(
                new PlayerUnhappyEvent { playerId = 7, happiness = 35, reasonKey = "x" }
            );

            Assert.AreEqual(1, state.inbox.Count, "T23: 1개 생성");
            Assert.AreEqual(InboxCategory.Morale, state.inbox[0].category, "T23: Morale");
            Assert.AreEqual(InboxPriority.Medium, state.inbox[0].priority, "T23: Medium");
            Assert.AreEqual("35", state.inbox[0].titleArgs["happiness"], "T23: happiness arg");
        }

        // ── T24. PlayerFatiguedEvent → Morale/Low ────────────────────

        [Test]
        public void T24_PlayerFatigued_AddsMoraleLow()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(new PlayerFatiguedEvent { playerId = 7, fatigue = 80 });

            Assert.AreEqual(1, state.inbox.Count, "T24: 1개 생성");
            Assert.AreEqual(InboxCategory.Morale, state.inbox[0].category, "T24: Morale");
            Assert.AreEqual(InboxPriority.Low, state.inbox[0].priority, "T24: Low");
        }

        // ── T25. StandingsChangedEvent → League/Low ──────────────────

        [Test]
        public void T25_StandingsChanged_AddsLeagueLow()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(
                new StandingsChangedEvent { clubId = 10, oldPosition = 5, newPosition = 3 }
            );

            Assert.AreEqual(1, state.inbox.Count, "T25: 1개 생성");
            Assert.AreEqual(InboxCategory.League, state.inbox[0].category, "T25: League");
            Assert.AreEqual(InboxPriority.Low, state.inbox[0].priority, "T25: Low");
            Assert.AreEqual("5", state.inbox[0].titleArgs["old"], "T25: old arg");
            Assert.AreEqual("3", state.inbox[0].titleArgs["new"], "T25: new arg");
        }

        // ── T26. ContractExpiringEvent → Transfer/Medium ─────────────

        [Test]
        public void T26_ContractExpiring_AddsTransferMedium()
        {
            var state = NewState();
            InboxRouter.Wire(state);

            EventBus.Publish(new ContractExpiringEvent { playerId = 7, monthsRemaining = 6 });

            Assert.AreEqual(1, state.inbox.Count, "T26: 1개 생성");
            Assert.AreEqual(InboxCategory.Transfer, state.inbox[0].category, "T26: Transfer");
            Assert.AreEqual(InboxPriority.Medium, state.inbox[0].priority, "T26: Medium");
            Assert.AreEqual("6", state.inbox[0].titleArgs["months"], "T26: months arg");
        }

        // ── T27. TransferCompletedEvent — 영입(toClub=user) / 방출(fromClub=user) ─

        [Test]
        public void T27_TransferCompleted_InAndOut()
        {
            var state = NewState(userClubId: 10);
            InboxRouter.Wire(state);

            EventBus.Publish(
                new TransferCompletedEvent { playerId = 1, fromClubId = 20, toClubId = 10 }
            );
            EventBus.Publish(
                new TransferCompletedEvent { playerId = 2, fromClubId = 10, toClubId = 30 }
            );
            EventBus.Publish(
                new TransferCompletedEvent { playerId = 3, fromClubId = 20, toClubId = 30 }
            );

            Assert.AreEqual(2, state.inbox.Count, "T27: 유저 관여 2건만");
            Assert.AreEqual("inbox_transfer_in_fmt", state.inbox[0].titleKey, "T27: 영입");
            Assert.AreEqual("inbox_transfer_out_fmt", state.inbox[1].titleKey, "T27: 방출");
        }

        // ── T28. LoanReturnedEvent (parentClub=user) → Transfer/Low ──

        [Test]
        public void T28_LoanReturned_ToUserClub_AddsTransferLow()
        {
            var state = NewState(userClubId: 10);
            InboxRouter.Wire(state);

            EventBus.Publish(
                new LoanReturnedEvent { playerId = 1, fromClubId = 20, parentClubId = 10 }
            );
            EventBus.Publish(
                new LoanReturnedEvent { playerId = 2, fromClubId = 20, parentClubId = 30 }
            );

            Assert.AreEqual(1, state.inbox.Count, "T28: 유저 복귀 1건만");
            Assert.AreEqual(InboxCategory.Transfer, state.inbox[0].category, "T28: Transfer");
            Assert.AreEqual(InboxPriority.Low, state.inbox[0].priority, "T28: Low");
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

        private static Player AddPlayer(GameState state, int id, int clubId)
        {
            var p = new Player
            {
                id = id,
                currentClubId = clubId,
                info = new PersonalInfo { firstName = "Test", lastName = $"P{id}" },
            };
            state.allPlayers.Add(p);
            return p;
        }

        private static Club AddClub(GameState state, int id)
        {
            var c = new Club { id = id, name = $"Club{id}" };
            state.allClubs.Add(c);
            return c;
        }
    }
}
