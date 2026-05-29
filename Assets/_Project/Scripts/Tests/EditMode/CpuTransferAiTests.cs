// CpuTransferAiTests.cs
// V0.5 F.1+F.2 — CpuTransferAi 트리거 + 후보 추첨 + 오퍼 제출 검증.

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class CpuTransferAiTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 5, 25); // 월요일

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // 기본값: aiWeaknessRatioThreshold=0.95 / aiBudgetRatio=0.4 / aiSavingsThreshold=10000
            // caRepBase=50 / caRepCoeff=1.5 (V0.1 기본)
        }

        // ── T1. 빈 state — no-op ──────────────────────────────────────

        [Test]
        public void T1_EmptyState_DoesNotThrow()
        {
            var state = NewState();
            Assert.DoesNotThrow(() => CpuTransferAi.Run(state, _balance));
            Assert.AreEqual(0, state.activeOffers.Count);
        }

        // ── T2. 자기 클럽 제외 ───────────────────────────────────────

        [Test]
        public void T2_UserClub_NotProcessed()
        {
            var state = NewState();
            state.userClubId = 1;
            var userClub = NewClub(1);
            // 약점 라인 강제 — userClub 의 GK 라인이 0 평균
            state.AddClub(userClub);

            // 다른 후보 클럽 있어도 user club 자체는 처리 X
            CpuTransferAi.Run(state, _balance);
            Assert.AreEqual(0, state.activeOffers.Count);
        }

        // ── T3. 약점 라인 트리거 — 명단 후보 존재 시 오퍼 ───────────

        [Test]
        public void T3_WeakLine_OffersForStrongerCandidate()
        {
            var state = NewState();
            state.userClubId = -1;

            // 클럽 A — DF 라인만 약점 (CB 4명 CA 50, 나머지 라인 CA 200 = expected 와 동일)
            var clubA = NewClub(1, reputation: 100);
            clubA.finance = new Finance { money = 100_000_000 };
            for (int i = 1; i <= 11; i++)
            {
                int ca = (i >= 2 && i <= 5) ? 50 : 200; // CB 만 약함
                var p = NewPlayer(i, ca: ca, pos: PositionFor(i), clubId: 1);
                state.AddPlayer(p);
                clubA.seniorSquadIds.Add(i);
            }

            // 외부 강자 B — 클럽 A 의 명단에 들어가 있음 (CB, CA 150)
            var clubB = NewClub(2, reputation: 100);
            for (int i = 100; i < 105; i++)
            {
                var p = NewPlayer(i, ca: 150, pos: Position.CB, clubId: 2);
                state.AddPlayer(p);
                clubB.seniorSquadIds.Add(i);
            }

            // clubA 의 scoutingKnowledge 에 clubB 의 선수 등록
            foreach (var pid in clubB.seniorSquadIds)
            {
                clubA.scoutingKnowledge[pid] = new ScoutReport
                {
                    playerId = pid,
                    scoutLevel = 100,
                };
            }

            state.AddClub(clubA);
            state.AddClub(clubB);

            CpuTransferAi.Run(state, _balance);

            // clubA 가 강자 영입 시도 — 오퍼 ≥ 1
            Assert.Greater(state.activeOffers.Count, 0, "T3: 약점 라인 → 오퍼 발생");
            var offer = state.activeOffers[0];
            Assert.AreEqual(1, offer.toClubId, "T3: clubA 가 영입");
            Assert.AreEqual(2, offer.fromClubId, "T3: clubB 에서 옴");
        }

        // ── T4. 후보 없음 — 오퍼 X ─────────────────────────────────────

        [Test]
        public void T4_NoCandidates_NoOffer()
        {
            var state = NewState();
            state.userClubId = -1;

            var clubA = NewClub(1, reputation: 100);
            clubA.finance = new Finance { money = 100_000_000 };
            // 클럽 A 만 — 명단 비어 있음
            for (int i = 1; i <= 11; i++)
            {
                var p = NewPlayer(i, ca: 50, pos: PositionFor(i), clubId: 1);
                state.AddPlayer(p);
                clubA.seniorSquadIds.Add(i);
            }
            state.AddClub(clubA);

            CpuTransferAi.Run(state, _balance);
            Assert.AreEqual(0, state.activeOffers.Count, "T4: 명단 ∅ → 오퍼 X");
        }

        // ── T5. 결정성 — 같은 시드 → 같은 오퍼 ────────────────────────

        [Test]
        public void T5_Determinism_SameSeedSameOffers()
        {
            var s1 = BuildWeakLineScenario(seed: 42);
            var s2 = BuildWeakLineScenario(seed: 42);

            CpuTransferAi.Run(s1, _balance);
            CpuTransferAi.Run(s2, _balance);

            Assert.AreEqual(s1.activeOffers.Count, s2.activeOffers.Count, "T5: 같은 시드 오퍼 개수 동일");
            if (s1.activeOffers.Count > 0)
                Assert.AreEqual(
                    s1.activeOffers[0].playerId,
                    s2.activeOffers[0].playerId,
                    "T5: 같은 후보 선수"
                );
        }

        // ── T6. 자금 부족 — 오퍼 X ────────────────────────────────────

        [Test]
        public void T6_InsufficientBudget_NoOffer()
        {
            var state = BuildWeakLineScenario(seed: 42);
            // 자금 0
            foreach (var club in state.allClubs)
            {
                if (club.id != state.userClubId)
                    club.finance.money = 0;
            }

            CpuTransferAi.Run(state, _balance);
            Assert.AreEqual(0, state.activeOffers.Count, "T6: 자금 0 → 오퍼 X");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState BuildWeakLineScenario(int seed)
        {
            var state = NewState();
            state.randomSeed = seed;
            state.userClubId = -1;

            var clubA = NewClub(1, reputation: 100);
            clubA.finance = new Finance { money = 100_000_000 };
            for (int i = 1; i <= 11; i++)
            {
                int ca = (i >= 2 && i <= 5) ? 50 : 200;
                var p = NewPlayer(i, ca: ca, pos: PositionFor(i), clubId: 1);
                state.AddPlayer(p);
                clubA.seniorSquadIds.Add(i);
            }

            var clubB = NewClub(2, reputation: 100);
            for (int i = 100; i < 105; i++)
            {
                var p = NewPlayer(i, ca: 150, pos: Position.CB, clubId: 2);
                state.AddPlayer(p);
                clubB.seniorSquadIds.Add(i);
            }
            foreach (var pid in clubB.seniorSquadIds)
                clubA.scoutingKnowledge[pid] = new ScoutReport { playerId = pid, scoutLevel = 100 };

            state.AddClub(clubA);
            state.AddClub(clubB);
            return state;
        }

        private GameState NewState() =>
            new GameState
            {
                currentDate = _today,
                randomSeed = 42,
                activeOffers = new List<TransferOffer>(),
            };

        private static Player NewPlayer(int id, int ca, Position pos, int clubId)
        {
            return new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = ca,
                currentClubId = clubId,
                info = new PersonalInfo
                {
                    firstName = $"P{id}",
                    lastName = "Test",
                    primaryPosition = pos,
                    birthDate = new DateTime(2000, 1, 1),
                },
                stats = new Stats(),
                contract = new Contract
                {
                    weeklyWage = 1000,
                    startDate = new DateTime(2024, 1, 1),
                    endDate = new DateTime(2030, 1, 1),
                },
                state = new PlayerState { injury = new InjuryInfo { injuryTypeId = -1 } },
            };
        }

        private static Club NewClub(int id, int reputation = 100) =>
            new Club
            {
                id = id,
                name = $"Club{id}",
                leagueId = 1,
                reputation = reputation,
                facilities = new Facilities { scoutLevel = 5 },
                finance = new Finance { money = 100_000_000 },
            };

        // 11 선수 — GK 1 + CB 4 + CM 4 + ST 2
        private static Position PositionFor(int idx)
        {
            if (idx == 1)
                return Position.GK;
            if (idx <= 5)
                return Position.CB;
            if (idx <= 9)
                return Position.CM;
            return Position.ST;
        }
    }
}
