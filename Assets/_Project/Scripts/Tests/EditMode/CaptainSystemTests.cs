// CaptainSystemTests.cs
// J.6 — CaptainSystem 단위 테스트 (T1~T3).

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class CaptainSystemTests
    {
        private static readonly DateTime BaseDate = new DateTime(2025, 8, 1);

        // ── T1: AssignAuto — 리더십+나이+계약 점수 상위 → 캡틴 ──────────────────────

        [Test]
        public void T1_AssignAuto_PicksHighestScoredPlayerAsCaptain()
        {
            // A: leadership=15, age=30, yearsLeft=2 → 15 + 30*0.5 + 2*2.0 = 34
            // B: leadership=10, age=25, yearsLeft=5 → 10 + 25*0.5 + 5*2.0 = 32.5
            // A 가 더 높음 → A 가 캡틴
            var balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            var state = new GameState { currentDate = BaseDate };
            var club = MakeClub(1);
            state.AddClub(club);

            var playerA = MakePlayer(
                1,
                clubId: 1,
                leadership: 15,
                birthDate: BaseDate.AddYears(-30),
                contractEnd: BaseDate.AddYears(2)
            );
            var playerB = MakePlayer(
                2,
                clubId: 1,
                leadership: 10,
                birthDate: BaseDate.AddYears(-25),
                contractEnd: BaseDate.AddYears(5)
            );
            state.AddPlayer(playerA);
            state.AddPlayer(playerB);
            club.seniorSquadIds.Add(playerA.id);
            club.seniorSquadIds.Add(playerB.id);

            CaptainSystem.AssignAuto(club, state, balance);

            Assert.AreEqual(playerA.id, club.season.captainPlayerId);
            Assert.AreEqual(playerB.id, club.season.viceCaptainPlayerId);
        }

        // ── T2: Assign 수동 변경 ─────────────────────────────────────────────────────

        [Test]
        public void T2_Assign_ManualCaptainChange()
        {
            var club = MakeClub(1);
            club.season.captainPlayerId = 1;
            club.season.viceCaptainPlayerId = 2;

            // captain → 3 (기존 captain=1 → 3)
            CaptainSystem.Assign(club, 3, isVice: false);
            Assert.AreEqual(3, club.season.captainPlayerId);
            Assert.AreEqual(2, club.season.viceCaptainPlayerId); // vice 그대로

            // vice → 3 → captain 이랑 겹치면 captain = -1
            CaptainSystem.Assign(club, 3, isVice: false);
            CaptainSystem.Assign(club, 3, isVice: true);
            Assert.AreEqual(-1, club.season.captainPlayerId);
            Assert.AreEqual(3, club.season.viceCaptainPlayerId);
        }

        // ── T3: 라커룸 분위기 — 캡틴 리더십 가산점 검증 ─────────────────────────────

        [Test]
        public void T3_MoraleBonus_CaptainLeadershipAddsToMood()
        {
            var balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // dressingRoomCaptainLeadershipBonus 는 [0.2f] 기본값이 직렬화에 의존하므로
            // 필드를 직접 반영 (리플렉션 없이 실제 값 0.2f 사용)
            var state = new GameState { currentDate = BaseDate };
            var club = MakeClub(1);
            state.AddClub(club);

            var captain = MakePlayer(
                1,
                clubId: 1,
                leadership: 10,
                birthDate: BaseDate.AddYears(-25),
                contractEnd: BaseDate.AddYears(3)
            );
            captain.state.happiness = 60;
            state.AddPlayer(captain);
            club.seniorSquadIds.Add(captain.id);
            club.season.captainPlayerId = captain.id;

            MoraleSystem.UpdateDressingRoomMood(club, state, balance);

            // dressingRoomMood = happiness(60) + leadership(10) * bonus(0.2f) = 62
            int expected = 60 + (int)Math.Round(10 * balance.dressingRoomCaptainLeadershipBonus);
            Assert.AreEqual(expected, club.season.dressingRoomMood);
        }

        // ── T4: Score — 나이 cap 35세 적용 ─────────────────────────────────────────

        [Test]
        public void T4_Score_AgeCappedAt35()
        {
            var balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            var p40 = MakePlayer(
                1,
                clubId: 1,
                leadership: 0,
                birthDate: BaseDate.AddYears(-40),
                contractEnd: BaseDate.AddYears(1)
            );
            var p35 = MakePlayer(
                2,
                clubId: 1,
                leadership: 0,
                birthDate: BaseDate.AddYears(-35),
                contractEnd: BaseDate.AddYears(1)
            );

            // age cap = 35 이므로 두 선수 점수 동일해야 함
            float score40 = CaptainSystem.Score(p40, BaseDate, balance);
            float score35 = CaptainSystem.Score(p35, BaseDate, balance);
            Assert.AreEqual(
                score35,
                score40,
                0.1f,
                "40살 선수 점수가 35살 선수 점수와 달라야 하지 않음 (age cap 35)"
            );
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────────────

        private static Club MakeClub(int id) =>
            new Club
            {
                id = id,
                name = $"Club{id}",
                season = new SeasonState(),
                seniorSquadIds = new List<int>(),
            };

        private static Player MakePlayer(
            int id,
            int clubId,
            int leadership,
            DateTime birthDate,
            DateTime contractEnd
        ) =>
            new Player
            {
                id = id,
                currentClubId = clubId,
                currentAbility = 80,
                info = new PersonalInfo
                {
                    firstName = "F",
                    lastName = $"P{id}",
                    primaryPosition = Position.CM,
                    birthDate = birthDate,
                },
                stats = new Stats { mental = new MentalStats { leadership = leadership } },
                contract = new Contract
                {
                    weeklyWage = 1000,
                    startDate = birthDate.AddYears(18),
                    endDate = contractEnd,
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    suspendedMatches = 0,
                    happiness = 50,
                    morale = 50,
                },
            };
    }
}
