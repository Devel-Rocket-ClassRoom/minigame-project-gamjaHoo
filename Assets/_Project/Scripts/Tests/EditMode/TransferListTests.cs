// TransferListTests.cs
// V0.5 K.4 — Transfer List DoD: transferListed → 시장가 ×0.7 + AI 영입 가시화.
// algorithms.md K.4 / design-decisions.md #48.

using System;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class TransferListTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 7, 1);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.marketValueBase = 5_000_000;
            _balance.marketValueCaExponent = 4.0f;
            _balance.marketValuePaCoeff = 0f;
            _balance.marketValueAgeCurve = new float[] { 0.8f, 1.0f, 0.9f, 0.7f };
            _balance.marketValueContractCurve = new float[] { 0.5f, 0.8f, 1.0f, 1.05f };
            _balance.marketValuePositionFactor = new float[] { 0.75f, 0.85f, 1.0f, 1.2f };
            _balance.marketValueInjuryFactor = 0.5f;
            _balance.aiValueNoiseSigma = 0f; // 노이즈 제거 — 결정성 보장
            _balance.aiAcceptThreshold = 1.30f;
            _balance.aiCounterOfferThreshold = 1.10f;
            _balance.aiMockingThreshold = 0.85f;
            _balance.aiCounterOfferFactor = 1.30f;
            _balance.maxNegotiationRounds = 3;
            _balance.aiMockingMoralePenalty = 3;
            _balance.playtimeAgreementBonus = 0.2f;
            _balance.wageBaseAtMinCA = 500;
            _balance.wagePerCAPoint = 350f;
            _balance.wageFloor = 500;
            _balance.minCA = 30;
            _balance.transferListedDiscount = 0.7f;
            _balance.transferWindowSummerStartMonth = 6;
            _balance.transferWindowSummerStartDay = 1;
            _balance.transferWindowSummerEndMonth = 8;
            _balance.transferWindowSummerEndDay = 31;
            _balance.transferWindowWinterStartMonth = 1;
            _balance.transferWindowWinterStartDay = 1;
            _balance.transferWindowWinterEndMonth = 1;
            _balance.transferWindowWinterEndDay = 31;
            // CpuTransferAi
            _balance.aiWeaknessRatioThreshold = 0.95f;
            _balance.aiCoreInjuryWeeksThreshold = 4;
            _balance.aiContractFaThresholdDays = 180;
            _balance.aiSavingsThreshold = 10_000;
            _balance.aiBudgetRatio = 0.4f;
            _balance.aiOfferAmountRandomMin = 1.0f;
            _balance.aiOfferAmountRandomMax = 1.0f; // 고정 배율 — 결정성
            _balance.caRepBase = 30;
            _balance.caRepCoeff = 0.5f;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── T1. transferListed → 시장가 ×0.7 ────────────────────────

        [Test]
        public void T1_TransferListed_MarketValue_IsDiscounted()
        {
            var state = BuildBaseState();
            var player = BuildPlayer(1, 1, ca: 100);
            state.AddPlayer(player);

            int mvNormal = TransferSystem.CalculateMarketValue(player, state, _balance);

            player.state.transferListed = true;
            int mvListed = TransferSystem.CalculateMarketValue(player, state, _balance);

            Assert.Less(mvListed, mvNormal, "T1: transferListed → 시장가 감소");
            Assert.AreEqual(
                (int)(Math.Round(mvNormal * 0.7 / 100_000.0) * 100_000),
                mvListed,
                "T1: 시장가 ×0.7"
            );
        }

        // ── T2. 미등록 → 정상 시장가 ─────────────────────────────────

        [Test]
        public void T2_NotTransferListed_MarketValue_Normal()
        {
            var state = BuildBaseState();
            var player = BuildPlayer(1, 1, ca: 100);
            player.state.transferListed = false;
            state.AddPlayer(player);

            int mv1 = TransferSystem.CalculateMarketValue(player, state, _balance);
            int mv2 = TransferSystem.CalculateMarketValue(player, state, _balance);

            Assert.AreEqual(mv1, mv2, "T2: 같은 선수 → 같은 시장가");
        }

        // ── T3. CpuTransferAi → transferListed 선수 스카우팅 없이도 영입 ─

        [Test]
        public void T3_CpuAi_TargetsTransferListed_WithoutScouting()
        {
            // c1: 약한 GK 라인을 가진 AI 구단 (약점 → WeakLine 트리거)
            // c2: 상대 구단 (transferListed 선수 소속)
            var state = new GameState
            {
                currentDate = _today,
                randomSeed = 1,
                userClubId = 99, // AI 구단들만 (userClubId 와 다른 id)
                nextPlayerId = 100,
                nextIntakeId = 1,
                nextOfferId = 1,
            };

            var aiClub = new Club
            {
                id = 1,
                name = "AiClub",
                reputation = 60,
                leagueId = 1,
                finance = new Finance { money = 100_000_000 },
                facilities = new Facilities(), // CpuTransferAi.Run null 체크 통과
            };
            var sellingClub = new Club
            {
                id = 2,
                name = "SellingClub",
                reputation = 60,
                leagueId = 1,
                finance = new Finance { money = 10_000_000 },
            };
            state.AddClub(aiClub);
            state.AddClub(sellingClub);

            // AI 구단 선수 (GK 약점 — 낮은 CA)
            var weakGk = BuildPlayer(10, aiClub.id, ca: 40, pos: Position.GK);
            state.AddPlayer(weakGk);
            aiClub.seniorSquadIds.Add(weakGk.id);

            // 상대 구단 transferListed 선수 (GK, 높은 CA — 영입 목표)
            var target = BuildPlayer(20, sellingClub.id, ca: 80, pos: Position.GK);
            target.state.transferListed = true;
            state.AddPlayer(target);
            sellingClub.seniorSquadIds.Add(target.id);

            // scoutingKnowledge 에 target 없음 (미등록)
            aiClub.scoutingKnowledge = new System.Collections.Generic.Dictionary<
                int,
                ScoutReport
            >();

            CpuTransferAi.Run(state, _balance);

            bool offerMade = state.activeOffers.Any(o =>
                o.playerId == target.id && o.toClubId == aiClub.id
            );
            Assert.IsTrue(offerMade, "T3: AI가 transferListed 선수 (스카우팅 없이) 영입 오퍼");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private GameState BuildBaseState()
        {
            var state = new GameState
            {
                currentDate = _today,
                randomSeed = 42,
                userClubId = -1,
                nextPlayerId = 100,
                nextIntakeId = 1,
                nextOfferId = 1,
            };
            var club = new Club
            {
                id = 1,
                name = "Club",
                reputation = 60,
                leagueId = 1,
                finance = new Finance { money = 50_000_000 },
            };
            state.AddClub(club);
            return state;
        }

        private Player BuildPlayer(int id, int clubId, int ca = 100, Position pos = Position.CM)
        {
            return new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = ca + 10,
                currentClubId = clubId,
                info = new PersonalInfo
                {
                    firstName = "T",
                    lastName = "P",
                    birthDate = _today.AddYears(-25),
                    primaryPosition = pos,
                },
                state = new PlayerState
                {
                    morale = 50,
                    happiness = 50,
                    fatigue = 0,
                    form = 50,
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    transferListed = false,
                },
                contract = new Contract
                {
                    weeklyWage = 25_000,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddYears(2),
                },
                hiddenAttrs = new HiddenAttributes { loyalty = 50, ambition = 50 },
            };
        }
    }
}
