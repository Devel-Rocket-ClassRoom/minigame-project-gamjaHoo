// TransferSystem.cs
// algorithms.md #3 Market Value + #3.1 Transfer Flow 구현.
// Stateless (design-decisions.md #3). DailyProcessor 가 ProcessOffers 매일 호출.
//
// V0.1 정책 (design-decisions.md #37):
//   - 이적시장 (검색·오퍼·협상) 상시 — 시점 제약 X
//   - 이적시장 활성화 기간 (체결) 6/1~8/31 + 1/1~1/31 — 체결만 시기 제약
//   - 단일 라운드 AI 응답 (Accept/Reject) / 선수 자동 통과 / AI 영입 미구현 (사용자 클럽만)
//
// V0.5 H.1 (design-decisions.md #48):
//   - RenewContract — 상시 재계약. 시점 제약 X. algorithms.md V0.5-3.1.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Utils;
using UnityEngine;
using Random = System.Random;

namespace FMLite.Application
{
    public static class TransferSystem
    {
        // ── CalculateMarketValue (algorithms.md #3) ──────────────────

        public static int CalculateMarketValue(
            Player player,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            int ca = player.currentAbility;
            int pa = player.potentialAbility;
            int age = GetAge(player, state.currentDate);

            double caFactor = Math.Pow(ca / 100.0, balance.marketValueCaExponent);
            double paGapBonus = Math.Max(0, pa - ca) * balance.marketValuePaCoeff;
            double ageFactor = AgeCurve(age, balance);
            int remainingYears = ContractRemainingYears(player.contract, state.currentDate);
            double contractFactor = ContractCurve(remainingYears, balance);
            var line = StartingSquadGacha.LineOf(player.info.primaryPosition);
            double positionFactor = PositionFactor(line, balance);
            double injuryFactor =
                (
                    player.state != null
                    && player.state.injury != null
                    && player.state.injury.injuryTypeId == -1
                )
                    ? 1.0
                    : balance.marketValueInjuryFactor;

            double rawValue =
                (balance.marketValueBase * caFactor + paGapBonus)
                * ageFactor
                * contractFactor
                * positionFactor
                * injuryFactor;
            if (rawValue < 0)
                rawValue = 0;

            // transferListed → 시장가 할인 (algorithms.md K.4)
            if (player.state != null && player.state.transferListed)
                rawValue *= balance.transferListedDiscount;

            return Round100k((int)Math.Round(rawValue));
        }

        // ── SubmitOffer (algorithms.md #3.1 [3]) ─────────────────────

        public static TransferOffer SubmitOffer(
            int playerId,
            int fromClubId,
            int toClubId,
            int amount,
            Contract proposed,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (proposed == null)
                throw new ArgumentNullException(nameof(proposed));
            if (amount <= 0)
                throw new ArgumentException("amount must be > 0", nameof(amount));
            if (fromClubId == toClubId)
                throw new ArgumentException("from / to club must differ", nameof(toClubId));

            var player =
                state.GetPlayer(playerId)
                ?? throw new ArgumentException($"player id={playerId} not found");
            var fromClub =
                state.GetClub(fromClubId)
                ?? throw new ArgumentException($"fromClub id={fromClubId} not found");
            var toClub =
                state.GetClub(toClubId)
                ?? throw new ArgumentException($"toClub id={toClubId} not found");
            if (player.currentClubId != fromClubId)
                throw new ArgumentException(
                    $"player id={playerId} not in fromClub id={fromClubId} (actual currentClubId={player.currentClubId})"
                );

            var offer = new TransferOffer
            {
                id = state.nextOfferId++,
                playerId = playerId,
                fromClubId = fromClubId,
                toClubId = toClubId,
                amount = amount,
                proposed = proposed,
                status = OfferStatus.Pending,
            };

            // Release clause 발동: amount ≥ clause → 판매 구단 강제 합의 → 선수 개인협상(Negotiating).
            // (선수 협상은 그대로 진행 — design-decisions.md #48/#69)
            if (
                player.contract != null
                && player.contract.releaseClause > 0
                && amount >= player.contract.releaseClause
            )
            {
                offer.status = OfferStatus.Negotiating;
                offer.releaseClauseActivated = true;
            }

            state.activeOffers.Add(offer);

            EventBus.Publish(new OfferSubmittedEvent { offerId = offer.id });
            // 구단 합의(release clause) → 개인협상 단계 인박스 라우팅 트리거.
            if (offer.status == OfferStatus.Negotiating)
                EventBus.Publish(
                    new OfferRespondedEvent
                    {
                        offerId = offer.id,
                        newStatus = OfferStatus.Negotiating,
                    }
                );
            return offer;
        }

        // ── ProcessOffers (DailyProcessor 가 매일 호출) ─────────────

        public static void ProcessOffers(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            // ToList — 처리 중 activeOffers 변경 가능성 방지
            foreach (var offer in state.activeOffers.ToList())
            {
                if (offer == null)
                    continue;

                switch (offer.status)
                {
                    case OfferStatus.Pending:
                        AiRespondToOffer(offer, state, balance);
                        break;

                    case OfferStatus.Negotiating:
                        // AI 구매 구단은 개인협상 UI 가 없음 → proposed 조건으로 자동 평가
                        // (V0.1 AI 이적 완결성 유지). 유저 클럽은 PlayerNegotiationScene 대기.
                        if (offer.toClubId != state.userClubId)
                            AutoResolveAiPersonalTerms(offer, state, balance);
                        break;

                    case OfferStatus.Accepted:
                        if (IsTransferWindowOpen(state.currentDate, balance))
                            CompleteTransfer(offer, state);
                        // 활성화 기간 외 — Accepted 대기 유지
                        break;

                    // CounterOffer — 유저 RespondToCounterOffer 호출 대기
                    // Rejected / Completed: skip
                }
            }
        }

        private static void AiRespondToOffer(
            TransferOffer offer,
            GameState state,
            GameBalanceSO balance
        )
        {
            var player = state.GetPlayer(offer.playerId);
            if (player == null)
            {
                Debug.LogWarning($"[TransferSystem] offer.id={offer.id} player 누락 — Rejected");
                offer.status = OfferStatus.Rejected;
                EventBus.Publish(
                    new OfferRespondedEvent { offerId = offer.id, newStatus = OfferStatus.Rejected }
                );
                return;
            }

            int marketValue = CalculateMarketValue(player, state, balance);
            // 시드: state.randomSeed ^ offer.id ^ currentDate.Ticks — 결정성 (design-decisions.md #17)
            int seed = state.randomSeed ^ offer.id ^ unchecked((int)state.currentDate.Ticks);
            var rng = new Random(seed);

            // AI 평가 ±10% noise — 정확한 가치 평가 어려움 표현
            double noise = rng.NextNormal(1.0, balance.aiValueNoiseSigma);
            if (noise < 0.5)
                noise = 0.5;
            double aiPerceivedValue = marketValue * noise;
            double ratio = aiPerceivedValue > 0 ? offer.amount / aiPerceivedValue : 0;

            // V0.5 K.1/K.2 4분기 응답 (algorithms.md V0.5-3.1 [3-a], V1.0 #469 정합화)
            // 구단 이적료 수락(ratio≥1.30) → Negotiating (선수 개인협상 단계, PlayerNegotiationScene).
            // 위장 CounterOffer(counterAmount=amount) 폐기 — 좋은 오퍼가 매번 역제안으로 뜨던 결함 정정.
            if (ratio >= balance.aiAcceptThreshold)
            {
                offer.status = OfferStatus.Negotiating;
            }
            else if (ratio >= balance.aiCounterOfferThreshold)
            {
                offer.status = OfferStatus.CounterOffer;
                offer.counterAmount = (int)
                    Math.Round(aiPerceivedValue * balance.aiCounterOfferFactor);
                offer.negotiationRound++;
            }
            else if (ratio >= balance.aiMockingThreshold)
            {
                offer.status = OfferStatus.Rejected;
            }
            else
            {
                // 모욕적 오퍼 — Rejected + 사기 감소
                offer.status = OfferStatus.Rejected;
                if (player.state != null)
                    player.state.morale = Math.Clamp(
                        player.state.morale - balance.aiMockingMoralePenalty,
                        0,
                        100
                    );
            }

            // #384 — AI 응답 도착 일자 기록 (EventScheduler 의 stopRequested 트리거용).
            offer.lastResponseDate = state.currentDate;

            EventBus.Publish(
                new OfferRespondedEvent { offerId = offer.id, newStatus = offer.status }
            );
        }

        // ── 선수 개인 협상 (algorithms.md V0.5-3.1 [4] / design-decisions.md #48, #69) ──

        // 선수 수락 확률 (순수 — RNG·부수효과 없음). UI 반응 미리보기 + RespondToPersonalTerms 공용.
        // loyalty ↑ → 거절 / ambition ↑ → 수락 / includesPlaytimeAgreement → +playtimeAgreementBonus.
        // includeHidden=false → 공개 정보(주급/출전약속)만 — UI 반응 미리보기용.
        // includeHidden=true → 숨은 능력치(loyalty/ambition) 포함 — 실제 수락 판정용.
        private static double ComputePlayerAcceptChance(
            Player player,
            int weeklyWage,
            bool includesPlaytimeAgreement,
            GameBalanceSO balance,
            bool includeHidden = true
        )
        {
            int fairWage = EstimateInitialWage(player.currentAbility, balance);
            double wageRatio = fairWage > 0 ? (double)weeklyWage / fairWage : 1.0;

            double acceptChance = 0.5 + (wageRatio - 1.0) * 0.5;

            if (includeHidden)
            {
                int loyalty = player.hiddenAttrs != null ? player.hiddenAttrs.loyalty : 50;
                int ambition = player.hiddenAttrs != null ? player.hiddenAttrs.ambition : 50;
                acceptChance -= (loyalty - 50) / 100.0 * 0.3; // loyalty ↑ = 거절
                acceptChance += (ambition - 50) / 100.0 * 0.3; // ambition ↑ = 수락
            }

            if (includesPlaytimeAgreement)
                acceptChance += balance.playtimeAgreementBonus;

            return acceptChance;
        }

        // UI 반응 미리보기 — 공개 정보(주급)만 반영한 대략적 수락 확률 (0~1 clamp).
        // 숨은 능력치(loyalty/ambition)는 제외 → 실제 제안 시 불확실성이 남아 재협상 라운드가 유의미.
        public static double EstimatePlayerAcceptChance(
            int playerId,
            int weeklyWage,
            bool includesPlaytimeAgreement,
            GameState state,
            GameBalanceSO balance
        )
        {
            var player = state?.GetPlayer(playerId);
            if (player == null || balance == null)
                return 0.0;
            return Math.Clamp(
                ComputePlayerAcceptChance(
                    player,
                    weeklyWage,
                    includesPlaytimeAgreement,
                    balance,
                    includeHidden: false
                ),
                0.0,
                1.0
            );
        }

        // 공정 주급 추정 (선수 개인협상 씬 기본값/안내용). PlayerGenerator 와 동일 공식.
        public static int SuggestFairWage(Player player, GameBalanceSO balance)
        {
            if (player == null || balance == null)
                return 0;
            return EstimateInitialWage(player.currentAbility, balance);
        }

        // ── RespondToPersonalTerms (V1.0 #469 — Negotiating 단계 인터랙티브 협상) ──

        // 구단 이적료 합의(Negotiating) 후 유저가 개인 조건(주급/계약기간/출전약속)을 제안.
        // 선수 수락 → Accepted (이적창 열리면 CompleteTransfer).
        // 거절 → personalNegotiationRound++. max 초과 시 Rejected, 아니면 Negotiating 유지(재제안).
        // 결정성 시드는 round 미포함 — 같은 날 같은 조건 재제안=동일 결과, 조건 상향 시 수락 확률↑.
        public static PersonalTermsResult RespondToPersonalTerms(
            int offerId,
            Contract proposed,
            bool includesPlaytimeAgreement,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (proposed == null)
                throw new ArgumentNullException(nameof(proposed));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var offer =
                state.activeOffers.Find(o => o != null && o.id == offerId)
                ?? throw new ArgumentException($"offer id={offerId} not found");

            if (offer.status != OfferStatus.Negotiating)
                throw new InvalidOperationException(
                    $"offer id={offerId} status={offer.status} — Negotiating 상태가 아님"
                );

            offer.proposed = proposed;
            offer.includesPlaytimeAgreement = includesPlaytimeAgreement;

            var player = state.GetPlayer(offer.playerId);
            if (player == null)
            {
                offer.status = OfferStatus.Rejected;
                EventBus.Publish(
                    new OfferRespondedEvent { offerId = offer.id, newStatus = OfferStatus.Rejected }
                );
                return PersonalTermsResult.Rejected;
            }

            int seed =
                state.randomSeed
                ^ (offer.playerId * 397)
                ^ offer.id
                ^ unchecked((int)state.currentDate.Ticks);
            var rng = new Random(seed);

            double acceptChance = ComputePlayerAcceptChance(
                player,
                proposed.weeklyWage,
                includesPlaytimeAgreement,
                balance
            );

            if (rng.NextDouble() < acceptChance)
            {
                offer.status = OfferStatus.Accepted;
                EventBus.Publish(
                    new OfferRespondedEvent { offerId = offer.id, newStatus = OfferStatus.Accepted }
                );
                return PersonalTermsResult.Accepted;
            }

            offer.personalNegotiationRound++;
            if (offer.personalNegotiationRound >= balance.maxPersonalNegotiationRounds)
            {
                offer.status = OfferStatus.Rejected;
                EventBus.Publish(
                    new OfferRespondedEvent { offerId = offer.id, newStatus = OfferStatus.Rejected }
                );
                return PersonalTermsResult.Rejected;
            }

            // Negotiating 유지 — 재제안 가능 (이벤트 미발행: 인박스 스팸 방지)
            return PersonalTermsResult.StillNegotiating;
        }

        // AI 구매 구단의 개인협상 자동 처리 — proposed 조건으로 1회 평가 (재협상 없음).
        // ProcessOffers 가 Negotiating + toClubId != userClubId 일 때 호출.
        private static void AutoResolveAiPersonalTerms(
            TransferOffer offer,
            GameState state,
            GameBalanceSO balance
        )
        {
            var player = state.GetPlayer(offer.playerId);
            if (player == null || offer.proposed == null)
            {
                offer.status = OfferStatus.Rejected;
                EventBus.Publish(
                    new OfferRespondedEvent { offerId = offer.id, newStatus = OfferStatus.Rejected }
                );
                return;
            }

            int seed =
                state.randomSeed
                ^ (offer.playerId * 397)
                ^ offer.id
                ^ unchecked((int)state.currentDate.Ticks);
            var rng = new Random(seed);

            double acceptChance = ComputePlayerAcceptChance(
                player,
                offer.proposed.weeklyWage,
                offer.includesPlaytimeAgreement,
                balance
            );

            offer.status =
                rng.NextDouble() < acceptChance ? OfferStatus.Accepted : OfferStatus.Rejected;
            EventBus.Publish(
                new OfferRespondedEvent { offerId = offer.id, newStatus = offer.status }
            );
        }

        // ── RespondToCounterOffer (algorithms.md V0.5-3.1 [3-b]) ─────

        // 유저가 CounterOffer 에 응답.
        // Accept → offer.amount = counterAmount → Accepted
        // Reject → Rejected
        // ReCounter → offer.amount = newAmount → AiRespondToOffer 재호출 (negotiationRound++)
        //   단 negotiationRound > maxNegotiationRounds 시 강제 Rejected.
        public static void RespondToCounterOffer(
            int offerId,
            CounterResponse response,
            int newAmount,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var offer =
                state.activeOffers.Find(o => o != null && o.id == offerId)
                ?? throw new ArgumentException($"offer id={offerId} not found");

            if (offer.status != OfferStatus.CounterOffer)
                throw new InvalidOperationException(
                    $"offer id={offerId} status={offer.status} — CounterOffer 상태가 아님"
                );

            switch (response)
            {
                case CounterResponse.Accept:
                    offer.amount = offer.counterAmount;
                    // 구단 이적료 합의 → 선수 개인협상 단계 (PlayerNegotiationScene)
                    offer.status = OfferStatus.Negotiating;
                    EventBus.Publish(
                        new OfferRespondedEvent
                        {
                            offerId = offer.id,
                            newStatus = OfferStatus.Negotiating,
                        }
                    );
                    break;

                case CounterResponse.Reject:
                    offer.status = OfferStatus.Rejected;
                    EventBus.Publish(
                        new OfferRespondedEvent
                        {
                            offerId = offer.id,
                            newStatus = OfferStatus.Rejected,
                        }
                    );
                    break;

                case CounterResponse.ReCounter:
                    if (offer.negotiationRound >= balance.maxNegotiationRounds)
                    {
                        offer.status = OfferStatus.Rejected;
                        EventBus.Publish(
                            new OfferRespondedEvent
                            {
                                offerId = offer.id,
                                newStatus = OfferStatus.Rejected,
                            }
                        );
                    }
                    else
                    {
                        offer.amount = newAmount;
                        offer.status = OfferStatus.Pending;
                        AiRespondToOffer(offer, state, balance);
                        // CounterOffer: Dashboard 가 감지 → NegotiationScene 라우팅
                    }
                    break;
            }
        }

        private static void CompleteTransfer(TransferOffer offer, GameState state)
        {
            var player = state.GetPlayer(offer.playerId);
            var fromClub = state.GetClub(offer.fromClubId);
            var toClub = state.GetClub(offer.toClubId);

            // 정합성 방어 — 다른 이적으로 player.currentClubId 가 바뀐 경우 등
            if (
                player == null
                || fromClub == null
                || toClub == null
                || player.currentClubId != offer.fromClubId
            )
            {
                Debug.LogWarning(
                    $"[TransferSystem] CompleteTransfer 정합성 실패 (offer.id={offer.id}) — Rejected 로 종료"
                );
                offer.status = OfferStatus.Rejected;
                EventBus.Publish(
                    new OfferRespondedEvent { offerId = offer.id, newStatus = OfferStatus.Rejected }
                );
                return;
            }

            fromClub.seniorSquadIds.Remove(offer.playerId);
            if (!toClub.seniorSquadIds.Contains(offer.playerId))
                toClub.seniorSquadIds.Add(offer.playerId);

            player.contract = offer.proposed;
            player.currentClubId = toClub.id;

            int transferredAmount;
            if (offer.isLoan)
            {
                // 임대: parentClubId / loanEndDate 설정, loanFee 이동
                player.parentClubId = offer.fromClubId;
                player.loanEndDate = offer.loanEndDate;
                fromClub.finance.money += offer.loanFee;
                toClub.finance.money -= offer.loanFee;
                transferredAmount = offer.loanFee;
            }
            else
            {
                // 영구 이적: parentClubId 초기화 (임대 해지 후 영구 이적 케이스 포함)
                player.parentClubId = -1;
                player.loanEndDate = null;
                // 자금 이동 (V0.1: 자금 부족도 허용 — 적자 가능. V0.5+ 사전 검증)
                fromClub.finance.money += offer.amount;
                toClub.finance.money -= offer.amount;
                transferredAmount = offer.amount;
            }

            offer.status = OfferStatus.Completed;
            EventBus.Publish(
                new TransferCompletedEvent
                {
                    offerId = offer.id,
                    playerId = offer.playerId,
                    fromClubId = offer.fromClubId,
                    toClubId = offer.toClubId,
                    amount = transferredAmount,
                }
            );
        }

        // ── ProcessLoanReturns (algorithms.md V0.5-3.1 DailyProcessor 임대 복귀 처리) ──

        // DailyProcessor 가 매일 호출 — loanEndDate 도래 선수 자동 원 구단 복귀 + LoanReturnedEvent.
        public static void ProcessLoanReturns(GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            foreach (var player in state.allPlayers)
            {
                if (player.parentClubId == -1 || !player.loanEndDate.HasValue)
                    continue;
                if (state.currentDate.Date < player.loanEndDate.Value.Date)
                    continue;

                int fromClubId = player.currentClubId;
                int parentClubId = player.parentClubId;
                var fromClub = state.GetClub(fromClubId);
                var parentClub = state.GetClub(parentClubId);

                if (fromClub == null || parentClub == null)
                {
                    Debug.LogWarning(
                        $"[TransferSystem] 임대 복귀 실패 player.id={player.id} — club 없음"
                    );
                    continue;
                }

                fromClub.seniorSquadIds.Remove(player.id);
                if (!parentClub.seniorSquadIds.Contains(player.id))
                    parentClub.seniorSquadIds.Add(player.id);

                player.currentClubId = parentClubId;
                player.parentClubId = -1;
                player.loanEndDate = null;

                EventBus.Publish(
                    new LoanReturnedEvent
                    {
                        playerId = player.id,
                        fromClubId = fromClubId,
                        parentClubId = parentClubId,
                    }
                );
            }
        }

        // ── IsTransferWindowOpen ─────────────────────────────────────

        public static bool IsTransferWindowOpen(DateTime date, GameBalanceSO balance)
        {
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            int year = date.Year;
            var summerStart = new DateTime(
                year,
                balance.transferWindowSummerStartMonth,
                balance.transferWindowSummerStartDay
            );
            var summerEnd = new DateTime(
                year,
                balance.transferWindowSummerEndMonth,
                balance.transferWindowSummerEndDay
            );
            var winterStart = new DateTime(
                year,
                balance.transferWindowWinterStartMonth,
                balance.transferWindowWinterStartDay
            );
            var winterEnd = new DateTime(
                year,
                balance.transferWindowWinterEndMonth,
                balance.transferWindowWinterEndDay
            );

            var d = date.Date;
            return (d >= summerStart.Date && d <= summerEnd.Date)
                || (d >= winterStart.Date && d <= winterEnd.Date);
        }

        // ── RenewContract (algorithms.md V0.5-3.1 / design-decisions.md #48) ──

        // 상시 재계약. 시점 제약 X. 잔여 6개월 이내 가산점.
        // 수락 → contract 갱신 + MoraleSystem.OnContractRenewed + ContractRenewedEvent
        // 거절 → ContractRenewalRejectedEvent
        public static void RenewContract(
            int playerId,
            Contract newContract,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (newContract == null)
                throw new ArgumentNullException(nameof(newContract));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player =
                state.GetPlayer(playerId)
                ?? throw new ArgumentException($"player id={playerId} not found");

            int seed = state.randomSeed ^ playerId ^ unchecked((int)state.currentDate.Ticks);
            var rng = new Random(seed);

            int fairWage = EstimateInitialWage(player.currentAbility, balance);
            double wageRatio = fairWage > 0 ? (double)newContract.weeklyWage / fairWage : 1.0;

            double acceptChance = 0.4;
            acceptChance += (wageRatio - 1.0) * 0.6;

            int loyalty = player.hiddenAttrs != null ? player.hiddenAttrs.loyalty : 50;
            acceptChance += (loyalty - 50) / 100.0 * 0.3;

            int daysRemaining =
                player.contract != null
                    ? (int)(player.contract.endDate - state.currentDate).TotalDays
                    : 0;
            if (daysRemaining <= 180)
                acceptChance += 0.15;

            if (rng.NextDouble() < acceptChance)
            {
                player.contract = newContract;
                MoraleSystem.OnContractRenewed(state, playerId, balance);
                EventBus.Publish(new ContractRenewedEvent { playerId = playerId });
            }
            else
            {
                EventBus.Publish(new ContractRenewalRejectedEvent { playerId = playerId });
            }
        }

        // ── SubmitFreeAgentContract (algorithms.md V0.5-3.1 / design-decisions.md #48) ──

        // 보스만 룰: 잔여 6개월 이내 선수 → 이적료 없이 직접 계약 제안.
        // 판매 구단 응답 불필요 (amount=0) → 즉시 Accepted. ProcessOffers 가 창 열리면 CompleteTransfer.
        public static TransferOffer SubmitFreeAgentContract(
            int playerId,
            int toClubId,
            Contract proposed,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (proposed == null)
                throw new ArgumentNullException(nameof(proposed));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var player =
                state.GetPlayer(playerId)
                ?? throw new ArgumentException($"player id={playerId} not found");

            if (player.currentClubId == toClubId)
                throw new ArgumentException(
                    $"player id={playerId} is already in club id={toClubId}"
                );

            _ =
                state.GetClub(toClubId)
                ?? throw new ArgumentException($"toClub id={toClubId} not found");

            int daysRemaining =
                player.contract != null
                    ? (int)(player.contract.endDate - state.currentDate).TotalDays
                    : 0;
            if (daysRemaining > 180)
                throw new ArgumentException(
                    $"player id={playerId} has {daysRemaining} days remaining — FA contract requires ≤180 days"
                );

            var offer = new TransferOffer
            {
                id = state.nextOfferId++,
                playerId = playerId,
                fromClubId = player.currentClubId,
                toClubId = toClubId,
                amount = 0,
                proposed = proposed,
                status = OfferStatus.Accepted, // 판매 구단 응답 불필요
            };

            state.activeOffers.Add(offer);
            EventBus.Publish(new OfferSubmittedEvent { offerId = offer.id });
            return offer;
        }

        // ── SubmitLoanOffer (algorithms.md V0.5-3.1 / design-decisions.md #48) ──

        // 임대 오퍼. V0.5: AI 협상 생략 — 즉시 Accepted. ProcessOffers 가 창 열리면 CompleteTransfer.
        // loanFee ≥ 0 허용 (무료 임대 포함).
        public static TransferOffer SubmitLoanOffer(
            int playerId,
            int fromClubId,
            int toClubId,
            LoanTerm term,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (term == null)
                throw new ArgumentNullException(nameof(term));
            if (term.proposed == null)
                throw new ArgumentException("term.proposed must be set", nameof(term));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (fromClubId == toClubId)
                throw new ArgumentException("from / to club must differ", nameof(toClubId));

            var player =
                state.GetPlayer(playerId)
                ?? throw new ArgumentException($"player id={playerId} not found");
            _ =
                state.GetClub(fromClubId)
                ?? throw new ArgumentException($"fromClub id={fromClubId} not found");
            _ =
                state.GetClub(toClubId)
                ?? throw new ArgumentException($"toClub id={toClubId} not found");

            if (player.currentClubId != fromClubId)
                throw new ArgumentException(
                    $"player id={playerId} not in fromClub id={fromClubId}"
                );

            var offer = new TransferOffer
            {
                id = state.nextOfferId++,
                playerId = playerId,
                fromClubId = fromClubId,
                toClubId = toClubId,
                amount = term.loanFee,
                proposed = term.proposed,
                status = OfferStatus.Accepted, // V0.5: 임대는 즉시 합의
                isLoan = true,
                loanFee = term.loanFee,
                loanWageShare = term.loanWageShare,
                loanEndDate = term.loanEndDate,
                loanOption = term.option,
            };

            state.activeOffers.Add(offer);
            EventBus.Publish(new OfferSubmittedEvent { offerId = offer.id });
            return offer;
        }

        // ── SearchPlayers (Task 11.2) ────────────────────────────────

        public static List<Player> SearchPlayers(TransferSearchFilter filter, GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            DateTime today = state.currentDate;
            var balance = GameDatabase.GameBalance;

            return state
                .allPlayers.Where(p => p != null)
                .Where(p =>
                    !filter.position.HasValue || p.info.primaryPosition == filter.position.Value
                )
                .Where(p =>
                {
                    int age = GetAge(p, today);
                    return age >= filter.minAge && age <= filter.maxAge;
                })
                .Where(p => p.currentAbility >= filter.minCA && p.currentAbility <= filter.maxCA)
                .Where(p => !filter.excludeUserClub || p.currentClubId != state.userClubId)
                .Where(p => filter.onlyClubId == null || p.currentClubId == filter.onlyClubId.Value)
                .Where(p =>
                    filter.nationalityCode == null
                    || p.info.nationalityCode == filter.nationalityCode
                )
                .Where(p =>
                    filter.traitId == null
                    || (p.traitIds != null && p.traitIds.Contains(filter.traitId.Value))
                )
                .Where(p =>
                {
                    if (balance == null)
                        return true;
                    int mv = CalculateMarketValue(p, state, balance);
                    return mv >= filter.minMarketValue && mv <= filter.maxMarketValue;
                })
                .Where(p =>
                {
                    if (p.contract == null)
                        return filter.minContractMonths == 0;
                    int months = Math.Max(
                        0,
                        (p.contract.endDate.Year - today.Year) * 12
                            + (p.contract.endDate.Month - today.Month)
                    );
                    return months >= filter.minContractMonths && months <= filter.maxContractMonths;
                })
                .Where(p =>
                    string.IsNullOrEmpty(filter.nameContains)
                    || (p.info.firstName + " " + p.info.lastName).IndexOf(
                        filter.nameContains,
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                // Stage F (#472): 주급 범위. 계약 없는 선수(무소속) = 주급 0 으로 간주.
                .Where(p =>
                {
                    int wage = p.contract?.weeklyWage ?? 0;
                    return wage >= filter.minWage && wage <= filter.maxWage;
                })
                // Stage F (#472): 세부 stat 임계 — 모든 항목 AND (각 stat 이 최소값 이상).
                .Where(p =>
                {
                    if (filter.statThresholds == null || filter.statThresholds.Count == 0)
                        return true;
                    foreach (var kv in filter.statThresholds)
                        if (StatCatalog.Read(p.stats, kv.Key) < kv.Value)
                            return false;
                    return true;
                })
                .ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static int GetAge(Player p, DateTime currentDate)
        {
            int age = currentDate.Year - p.info.birthDate.Year;
            if (currentDate.DayOfYear < p.info.birthDate.DayOfYear)
                age--;
            return age;
        }

        private static int ContractRemainingYears(Contract c, DateTime currentDate)
        {
            if (c == null)
                return 0;
            int years = c.endDate.Year - currentDate.Year;
            if (c.endDate.DayOfYear < currentDate.DayOfYear)
                years--;
            return Math.Max(0, years);
        }

        private static double AgeCurve(int age, GameBalanceSO b)
        {
            if (b.marketValueAgeCurve == null || b.marketValueAgeCurve.Length < 4)
                return 1.0;
            if (age <= 21)
                return b.marketValueAgeCurve[0];
            else if (age <= 28)
                return b.marketValueAgeCurve[1];
            else if (age <= 33)
                return b.marketValueAgeCurve[2];
            else
                return b.marketValueAgeCurve[3];
        }

        private static double ContractCurve(int remainingYears, GameBalanceSO b)
        {
            if (b.marketValueContractCurve == null || b.marketValueContractCurve.Length == 0)
                return 1.0;
            int idx = Math.Clamp(remainingYears - 1, 0, b.marketValueContractCurve.Length - 1);
            return b.marketValueContractCurve[idx];
        }

        private static double PositionFactor(Line line, GameBalanceSO b)
        {
            if (b.marketValuePositionFactor == null || b.marketValuePositionFactor.Length != 4)
                return 1.0;
            return b.marketValuePositionFactor[(int)line];
        }

        private static int Round100k(int value)
        {
            return ((int)Math.Round(value / 100000.0)) * 100000;
        }

        // algorithms.md #1 6단계 EstimateInitialWage — PlayerGenerator 와 동일 공식.
        // RenewContract 에서 선수 측 공정 주급 추정에 사용.
        private static int EstimateInitialWage(int ca, GameBalanceSO b)
        {
            float raw = b.wageBaseAtMinCA + (ca - b.minCA) * b.wagePerCAPoint;
            int rounded = (int)(Math.Round(raw / 100.0) * 100);
            return Math.Max(b.wageFloor, rounded);
        }
    }

    public class TransferSearchFilter
    {
        public Position? position; // null = 전체
        public int minAge = 16;
        public int maxAge = 99;
        public int minCA = 0;
        public int maxCA = 200;
        public bool excludeUserClub = true;
        public int? onlyClubId = null; // null = 전체 (Squad 검색: userClubId)
        public string nationalityCode = null; // null = 전체
        public int? traitId = null; // null = 전체
        public int minMarketValue = 0;
        public int maxMarketValue = int.MaxValue;
        public int minContractMonths = 0;
        public int maxContractMonths = int.MaxValue;
        public string nameContains = null; // null = 전체

        // Stage F (#472): 세부 stat 임계 — fieldPath("technical.passing") → 최소값. 모든 항목 AND.
        // StatCatalog 로 값 조회 (실제 수치 기준). Not-Scouted 선수도 필터링되나 표시는 정성적 라벨.
        public Dictionary<string, int> statThresholds = new();

        // Stage F (#472): 주급 범위 (주당). minWage=0 / maxWage=int.MaxValue = 전체.
        public int minWage = 0;
        public int maxWage = int.MaxValue;
    }
}
