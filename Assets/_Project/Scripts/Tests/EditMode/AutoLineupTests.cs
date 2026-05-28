// AutoLineupTests.cs
// J.5 — AutoLineup 단위 테스트 (T1~T5).

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class AutoLineupTests
    {
        // ── T1: AssignBest — 포지션 호환 + 부상 제외 ──────────────────────────────

        [Test]
        public void T1_AssignBest_AssignsCompatibleHealthyPlayers()
        {
            GameDatabase.Clear();

            var formSO = ScriptableObject.CreateInstance<FormationSO>();
            formSO.id = 1;
            formSO.slotPositions = new[]
            {
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST,
            };
            GameDatabase.Register(formSO);

            var state = new GameState
            {
                randomSeed = 1,
                currentDate = new System.DateTime(2025, 8, 1),
            };

            var club = new Club { id = 1, name = "Test" };
            club.tactic = new Tactic { formationId = 1, slots = new List<TacticSlot>() };
            AutoLineup.EnsureSetPieceTakers(club.tactic, 4);
            for (int i = 0; i < 11; i++)
                club.tactic.slots.Add(
                    new TacticSlot
                    {
                        slotIndex = i,
                        roleId = 0,
                        duty = Duty.Support,
                        assignedPlayerId = -1,
                    }
                );

            state.AddClub(club);

            // 포지션 맞는 건강한 선수 11명 + GK 포지션 부상 선수 1명
            int id = 1;
            Position[] positions =
            {
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST,
            };
            foreach (var pos in positions)
            {
                var p = NewPlayer(id++, pos, ca: 100);
                state.AddPlayer(p);
                club.seniorSquadIds.Add(p.id);
            }
            // 부상 GK — 배정 안 돼야 함
            var injuredGK = NewPlayer(id++, Position.GK, ca: 200);
            injuredGK.state.injury = new InjuryInfo { injuryTypeId = 1 };
            state.AddPlayer(injuredGK);
            club.seniorSquadIds.Add(injuredGK.id);

            AutoLineup.AssignBest(club, state);

            Assert.AreEqual(11, club.tactic.slots.Count(s => s.assignedPlayerId >= 0));
            // 부상 GK는 배정되면 안 됨
            Assert.IsFalse(club.tactic.slots.Any(s => s.assignedPlayerId == injuredGK.id));
        }

        // ── T2: AssignBest — 중복 배정 없음 ──────────────────────────────────────

        [Test]
        public void T2_AssignBest_NoPlayerAssignedTwice()
        {
            GameDatabase.Clear();

            var formSO = ScriptableObject.CreateInstance<FormationSO>();
            formSO.id = 1;
            formSO.slotPositions = new[]
            {
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST,
            };
            GameDatabase.Register(formSO);

            var state = new GameState
            {
                randomSeed = 2,
                currentDate = new System.DateTime(2025, 8, 1),
            };
            var club = new Club { id = 1, name = "Test" };
            club.tactic = new Tactic { formationId = 1, slots = new List<TacticSlot>() };
            AutoLineup.EnsureSetPieceTakers(club.tactic, 4);
            for (int i = 0; i < 11; i++)
                club.tactic.slots.Add(
                    new TacticSlot
                    {
                        slotIndex = i,
                        roleId = 0,
                        duty = Duty.Support,
                        assignedPlayerId = -1,
                    }
                );
            state.AddClub(club);

            int id = 1;
            Position[] positions =
            {
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST,
            };
            foreach (var pos in positions)
            {
                var p = NewPlayer(id++, pos, ca: 100);
                state.AddPlayer(p);
                club.seniorSquadIds.Add(p.id);
            }

            AutoLineup.AssignBest(club, state);

            var assigned = club
                .tactic.slots.Where(s => s.assignedPlayerId >= 0)
                .Select(s => s.assignedPlayerId)
                .ToList();
            Assert.AreEqual(assigned.Count, assigned.Distinct().Count(), "중복 배정 발생");
        }

        // ── T3: IsAvailable — 부상/정지 판별 ─────────────────────────────────────

        [Test]
        public void T3_IsAvailable_InjuredOrSuspendedReturnsFalse()
        {
            var healthy = NewPlayer(1, Position.ST, ca: 100);
            Assert.IsTrue(AutoLineup.IsAvailable(healthy));

            var injured = NewPlayer(2, Position.ST, ca: 100);
            injured.state.injury = new InjuryInfo { injuryTypeId = 5 };
            Assert.IsFalse(AutoLineup.IsAvailable(injured));

            var suspended = NewPlayer(3, Position.ST, ca: 100);
            suspended.state.suspendedMatches = 1;
            Assert.IsFalse(AutoLineup.IsAvailable(suspended));
        }

        // ── T4: IsPositionCompatible — 1차/2차 포지션 ────────────────────────────

        [Test]
        public void T4_IsPositionCompatible_PrimaryAndSecondaryMatch()
        {
            var p = NewPlayer(1, Position.CM, ca: 100);
            p.info.secondaryPositions = new List<Position> { Position.DM };

            Assert.IsTrue(AutoLineup.IsPositionCompatible(p, Position.CM));
            Assert.IsTrue(AutoLineup.IsPositionCompatible(p, Position.DM));
            Assert.IsFalse(AutoLineup.IsPositionCompatible(p, Position.GK));
        }

        // ── T5: AdjustedCA — form/morale 가중치 ──────────────────────────────────

        [Test]
        public void T5_AdjustedCA_IncludesFormAndMorale()
        {
            var p = NewPlayer(1, Position.ST, ca: 80);
            p.state.form = 10;
            p.state.morale = 10;

            float expected = 80 + 10 * 0.2f + 10 * 0.1f;
            Assert.AreEqual(expected, AutoLineup.AdjustedCA(p), 0.001f);
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────────

        private static Player NewPlayer(int id, Position pos, int ca, int clubId = 1) =>
            new Player
            {
                id = id,
                currentClubId = clubId,
                currentAbility = ca,
                potentialAbility = ca + 10,
                info = new PersonalInfo
                {
                    primaryPosition = pos,
                    firstName = "F",
                    lastName = "L",
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    suspendedMatches = 0,
                    form = 5,
                    morale = 5,
                },
            };
    }
}
