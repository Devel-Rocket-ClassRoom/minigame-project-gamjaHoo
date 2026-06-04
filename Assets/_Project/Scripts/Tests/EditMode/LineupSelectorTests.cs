// LineupSelectorTests.cs
// Stage H.6 (#483) — 포메이션 정합 선발 11 (LineupSelector). 포지션 적합 > CA, 부상/정지 제외,
// 무포메이션 top-CA 폴백, HasValidLineup, 결정성, 노이즈.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class LineupSelectorTests
    {
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
        }

        [TearDown]
        public void Teardown() => GameDatabase.Clear();

        // 4-4-2: GK,CB,CB,LB,RB,CM,CM,LM,RM,ST,ST (id=1)
        private static FormationSO Register442()
        {
            var f = ScriptableObject.CreateInstance<FormationSO>();
            f.id = 1;
            f.displayName = "4-4-2";
            f.slotPositions = new[]
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
            GameDatabase.Register(f);
            return f;
        }

        private static Player Mk(int id, Position pos, int ca)
        {
            return new Player
            {
                id = id,
                currentAbility = ca,
                info = new PersonalInfo
                {
                    primaryPosition = pos,
                    secondaryPositions = new List<Position>(),
                    firstName = "F",
                    lastName = "L" + id,
                },
                state = new PlayerState { injury = new InjuryInfo { injuryTypeId = -1 } },
            };
        }

        private static (GameState, Club) BuildClub(int formationId, IEnumerable<Player> players)
        {
            var state = new GameState { randomSeed = 1 };
            var club = new Club
            {
                id = 1,
                name = "C",
                tactic = new Tactic { formationId = formationId, slots = new List<TacticSlot>() },
            };
            state.AddClub(club);
            foreach (var p in players)
            {
                state.AddPlayer(p);
                club.seniorSquadIds.Add(p.id);
            }
            return (state, club);
        }

        private static int LineCount(List<int> xi, GameState s, Line line) =>
            xi.Count(id => StartingSquadGacha.LineOf(s.GetPlayer(id).info.primaryPosition) == line);

        // ── T1. 포지션 적합 > CA — 고 CA 공격수 多여도 GK 1 + 라인 정합 ──
        [Test]
        public void T1_FormationFit_BeatsRawCA()
        {
            Register442();
            var squad = new List<Player>();
            int id = 1;
            squad.Add(Mk(id++, Position.GK, 60));
            for (int i = 0; i < 4; i++)
                squad.Add(Mk(id++, Position.CB, 60));
            squad.Add(Mk(id++, Position.LB, 60));
            squad.Add(Mk(id++, Position.RB, 60));
            for (int i = 0; i < 4; i++)
                squad.Add(Mk(id++, Position.CM, 60));
            squad.Add(Mk(id++, Position.LM, 60));
            squad.Add(Mk(id++, Position.RM, 60));
            for (int i = 0; i < 6; i++)
                squad.Add(Mk(id++, Position.ST, 90)); // 공격수 CA 훨씬 높음
            var (state, club) = BuildClub(1, squad);

            var xi = LineupSelector.SelectStartingEleven(club, state, 42, _balance);

            Assert.AreEqual(11, xi.Count, "T1: 11명");
            Assert.AreEqual(1, LineCount(xi, state, Line.GK), "T1: GK 정확히 1 (고CA 공격수 多여도)");
            Assert.AreEqual(2, LineCount(xi, state, Line.AT), "T1: AT 2 (ST 슬롯 2개만, 6 ST 중 2)");
            Assert.AreEqual(4, LineCount(xi, state, Line.DF), "T1: DF 4");
            Assert.AreEqual(4, LineCount(xi, state, Line.MF), "T1: MF 4");
        }

        // ── T2. 부상/정지 제외 ──
        [Test]
        public void T2_ExcludesInjuredSuspended()
        {
            Register442();
            var squad = new List<Player>();
            int id = 1;
            squad.Add(Mk(id++, Position.GK, 60));
            for (int i = 0; i < 5; i++)
                squad.Add(Mk(id++, Position.CB, 60));
            for (int i = 0; i < 5; i++)
                squad.Add(Mk(id++, Position.CM, 60));
            for (int i = 0; i < 3; i++)
                squad.Add(Mk(id++, Position.ST, 60));
            var (state, club) = BuildClub(1, squad);
            // 첫 CB 부상, 첫 CM 정지
            var injured = squad.First(p => p.info.primaryPosition == Position.CB);
            injured.state.injury.injuryTypeId = 1;
            var susp = squad.First(p => p.info.primaryPosition == Position.CM);
            susp.state.suspendedMatches = 1;

            var xi = LineupSelector.SelectStartingEleven(club, state, 7, _balance);
            Assert.IsFalse(xi.Contains(injured.id), "T2: 부상 제외");
            Assert.IsFalse(xi.Contains(susp.id), "T2: 정지 제외");
            Assert.AreEqual(11, xi.Count, "T2: 11명 (가용 14명 중)");
        }

        // ── T3. 포메이션 미등록 → top-CA 폴백 (포지션 편향 없음) ──
        [Test]
        public void T3_NoFormation_TopCaFallback()
        {
            // 포메이션 미등록. 다양한 포지션 + CA. 상위 11 CA 가 선발돼야.
            var squad = new List<Player>();
            for (int i = 0; i < 16; i++)
                squad.Add(
                    Mk(i + 1, i % 4 == 0 ? Position.CB : Position.CM, 50 + i)
                ); // CA 50..65
            var (state, club) = BuildClub(1, squad); // formationId=1 이지만 미등록 → GetFormation null

            var xi = LineupSelector.SelectStartingEleven(club, state, 1, _balance);
            Assert.AreEqual(11, xi.Count);
            int xiMin = xi.Min(id => state.GetPlayer(id).currentAbility);
            int benchMax = squad
                .Where(p => !xi.Contains(p.id))
                .Select(p => p.currentAbility)
                .DefaultIfEmpty(0)
                .Max();
            Assert.GreaterOrEqual(xiMin, benchMax, "T3: 무포메이션 → top-CA (XI 최저 ≥ 벤치 최고)");
        }

        // ── T4. HasValidLineup ──
        [Test]
        public void T4_HasValidLineup()
        {
            Register442();
            var squad = new List<Player>();
            for (int i = 0; i < 11; i++)
                squad.Add(Mk(i + 1, Position.CM, 60));
            var (state, club) = BuildClub(1, squad);
            Assert.IsFalse(LineupSelector.HasValidLineup(club, state), "T4: 미배정 → false");

            for (int i = 0; i < 11; i++)
                club.tactic.slots.Add(new TacticSlot { slotIndex = i, assignedPlayerId = i + 1 });
            Assert.IsTrue(LineupSelector.HasValidLineup(club, state), "T4: 11 배정 → true");

            squad[0].state.injury.injuryTypeId = 1; // 배정 선수 1명 부상
            Assert.IsFalse(LineupSelector.HasValidLineup(club, state), "T4: 배정 선수 부상 → false");
        }

        // ── T5. 결정성 — 같은 시드 동일 XI ──
        [Test]
        public void T5_Determinism()
        {
            Register442();
            var squad1 = new List<Player>();
            var squad2 = new List<Player>();
            int id = 1;
            void Add(Position p, int ca)
            {
                squad1.Add(Mk(id, p, ca));
                squad2.Add(Mk(id, p, ca));
                id++;
            }
            Add(Position.GK, 60);
            for (int i = 0; i < 4; i++)
                Add(Position.CB, 55 + i);
            for (int i = 0; i < 5; i++)
                Add(Position.CM, 55 + i);
            for (int i = 0; i < 4; i++)
                Add(Position.ST, 55 + i);
            var (s1, c1) = BuildClub(1, squad1);
            var (s2, c2) = BuildClub(1, squad2);

            var xi1 = LineupSelector.SelectStartingEleven(c1, s1, 99, _balance);
            var xi2 = LineupSelector.SelectStartingEleven(c2, s2, 99, _balance);
            CollectionAssert.AreEqual(xi1, xi2, "T5: 같은 시드 동일 XI");
        }
    }
}
