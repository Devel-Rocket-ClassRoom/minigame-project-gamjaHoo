// MentoringSystemTests.cs
// L.4 — MentoringSystem 단위 테스트 (T1~T4).

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class MentoringSystemTests
    {
        private static readonly DateTime BaseDate = new DateTime(2025, 8, 1);
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // mentoringRateModifier 기본값 5 사용
        }

        // ── T1: 6회 틱 후 professionalism +30 ──────────────────────────

        [Test]
        public void T1_SixTicks_ProfessionalismConverges30()
        {
            var (state, club, mentor, mentee) = BuildScenario(
                mentorProf: 80, menteeProf: 20,
                mentorAmbition: 50, menteeAmbition: 50,
                mentorLoyalty: 50, menteeLoyalty: 50
            );

            for (int i = 0; i < 6; i++)
                MentoringSystem.RunMentoring(state, _balance);

            // delta/tick = min(|80-20|, 5) = 5 → 6 ticks = +30
            Assert.AreEqual(50, mentee.hiddenAttrs.professionalism, "T1: mentee professionalism +30 (20→50)");
        }

        // ── T2: 그룹 없으면 변화 없음 ────────────────────────────────

        [Test]
        public void T2_NoGroup_NoChange()
        {
            var state = new GameState { currentDate = BaseDate };
            var club = MakeClub(1);
            state.AddClub(club);
            // mentoringGroups 비어있음

            var player = MakePlayer(1, prof: 40, ambition: 40, loyalty: 40);
            state.AddPlayer(player);
            club.seniorSquadIds.Add(player.id);

            MentoringSystem.RunMentoring(state, _balance);

            Assert.AreEqual(40, player.hiddenAttrs.professionalism, "T2: 그룹 없으면 변화 없음");
        }

        // ── T3: mentor == mentee 값이면 변화 없음 ────────────────────

        [Test]
        public void T3_EqualAttrs_NoChange()
        {
            var (state, _, mentor, mentee) = BuildScenario(
                mentorProf: 60, menteeProf: 60,
                mentorAmbition: 60, menteeAmbition: 60,
                mentorLoyalty: 60, menteeLoyalty: 60
            );

            MentoringSystem.RunMentoring(state, _balance);

            Assert.AreEqual(60, mentee.hiddenAttrs.professionalism, "T3: 동일하면 변화 없음");
            Assert.AreEqual(60, mentee.hiddenAttrs.ambition);
            Assert.AreEqual(60, mentee.hiddenAttrs.loyalty);
        }

        // ── T4: AddGroup / RemoveGroup API ───────────────────────────

        [Test]
        public void T4_AddGroup_ThenRemoveGroup()
        {
            var state = new GameState { currentDate = BaseDate };
            var club = MakeClub(1);
            state.AddClub(club);

            var mentor = MakePlayer(1, prof: 80, ambition: 50, loyalty: 50);
            var mentee = MakePlayer(2, prof: 20, ambition: 50, loyalty: 50);
            state.AddPlayer(mentor);
            state.AddPlayer(mentee);
            club.seniorSquadIds.Add(mentor.id);
            club.seniorSquadIds.Add(mentee.id);

            // AddGroup
            var group = MentoringSystem.AddGroup(club, mentor.id, new List<int> { mentee.id }, state);
            Assert.AreEqual(1, club.season.mentoringGroups.Count, "T4: 그룹 추가됨");
            Assert.AreEqual(mentor.id, group.mentorPlayerId);

            // 같은 멘토로 중복 추가 → 예외
            Assert.Throws<InvalidOperationException>(() =>
                MentoringSystem.AddGroup(club, mentor.id, new List<int> { mentee.id }, state)
            , "T4: 멘토 중복 → 예외");

            // RemoveGroup
            MentoringSystem.RemoveGroup(club, group.id);
            Assert.AreEqual(0, club.season.mentoringGroups.Count, "T4: 그룹 제거됨");

            // 없는 그룹 제거 → 예외
            Assert.Throws<InvalidOperationException>(() =>
                MentoringSystem.RemoveGroup(club, group.id)
            , "T4: 없는 그룹 제거 → 예외");
        }

        // ── T5: 음수 방향 수렴 (mentor < mentee) ────────────────────

        [Test]
        public void T5_MentorLower_MenteeDecreases()
        {
            var (state, _, _, mentee) = BuildScenario(
                mentorProf: 20, menteeProf: 80,
                mentorAmbition: 50, menteeAmbition: 50,
                mentorLoyalty: 50, menteeLoyalty: 50
            );

            MentoringSystem.RunMentoring(state, _balance);

            Assert.AreEqual(75, mentee.hiddenAttrs.professionalism, "T5: mentor < mentee → -5");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private (GameState state, Club club, Player mentor, Player mentee) BuildScenario(
            int mentorProf, int menteeProf,
            int mentorAmbition, int menteeAmbition,
            int mentorLoyalty, int menteeLoyalty
        )
        {
            var state = new GameState { currentDate = BaseDate };
            var club = MakeClub(1);
            state.AddClub(club);

            var mentor = MakePlayer(1, prof: mentorProf, ambition: mentorAmbition, loyalty: mentorLoyalty);
            var mentee = MakePlayer(2, prof: menteeProf, ambition: menteeAmbition, loyalty: menteeLoyalty);
            state.AddPlayer(mentor);
            state.AddPlayer(mentee);
            club.seniorSquadIds.Add(mentor.id);
            club.seniorSquadIds.Add(mentee.id);

            MentoringSystem.AddGroup(club, mentor.id, new List<int> { mentee.id }, state);

            return (state, club, mentor, mentee);
        }

        private static Player MakePlayer(int id, int prof, int ambition, int loyalty)
        {
            return new Player
            {
                id = id,
                info = new PersonalInfo
                {
                    birthDate = BaseDate.AddYears(-25),
                    primaryPosition = Position.CM,
                },
                hiddenAttrs = new HiddenAttributes
                {
                    professionalism = prof,
                    ambition = ambition,
                    loyalty = loyalty,
                },
                state = new PlayerState(),
                stats = new Stats
                {
                    technical = new TechnicalStats(),
                    mental = new MentalStats(),
                    physical = new PhysicalStats(),
                    gk = new GoalkeepingStats(),
                },
                contract = new Contract
                {
                    endDate = BaseDate.AddYears(2),
                },
            };
        }

        private static Club MakeClub(int id)
        {
            return new Club
            {
                id = id,
                name = $"Club{id}",
                facilities = new Facilities(),
                finance = new Finance(),
                season = new SeasonState(),
                seniorSquadIds = new List<int>(),
                youthSquadIds = new List<int>(),
                intakeHistory = new List<YouthIntake>(),
            };
        }
    }
}
