// ScoutingSystemTests.cs
// V0.5 E.2 — ScoutingSystem 명단 관리 검증.
// 완료 조건: 시설 Lv5 → ~3000명 / Lv1 → ~50명 (단일 리그 모델, 후보 수 제한).

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class ScoutingSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 5, 25); // 월요일

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // GameDatabase 의 FacilityLevelSO 의존 — 테스트는 직접 결과 검증보다 동작 검증.
        }

        // ── T1. 자기 구단 자동 등록 (scoutLevel=100, 정확 estimate) ──

        [Test]
        public void T1_OwnSquad_RegisteredWithMaxLevel()
        {
            var state = NewState();
            var club = NewClub(1, scoutLevel: 1);
            var p = NewPlayer(101, ca: 145, pa: 180);
            state.AddPlayer(p);
            club.seniorSquadIds.Add(101);
            state.AddClub(club);

            ScoutingSystem.UpdateKnowledge(state, _balance);

            Assert.IsTrue(club.scoutingKnowledge.ContainsKey(101), "T1: 자기 구단 선수 명단 등록");
            var report = club.scoutingKnowledge[101];
            Assert.AreEqual(100, report.scoutLevel, "T1: scoutLevel = 100");
            Assert.AreEqual(145, report.caEstimate.estimate, "T1: 정확 CA");
            Assert.AreEqual(0, report.caEstimate.margin, "T1: margin = 0");
            Assert.AreEqual(180, report.paEstimate.estimate, "T1: 정확 PA");
        }

        // ── T2. 유스도 자동 등록 ──────────────────────────────────────

        [Test]
        public void T2_YouthSquad_AlsoRegistered()
        {
            var state = NewState();
            var club = NewClub(1, scoutLevel: 1);
            var youth = NewPlayer(202, ca: 60, pa: 150);
            state.AddPlayer(youth);
            club.youthSquadIds.Add(202);
            state.AddClub(club);

            ScoutingSystem.UpdateKnowledge(state, _balance);

            Assert.IsTrue(club.scoutingKnowledge.ContainsKey(202), "T2: 유스도 자동 등록");
            Assert.AreEqual(100, club.scoutingKnowledge[202].scoutLevel);
        }

        // ── T3. 매주 호출 시 누적 (scoutLevel ↑) ──────────────────────

        [Test]
        public void T3_WeeklyAccumulation_LevelIncreases()
        {
            var state = NewState();
            var clubA = NewClub(1, scoutLevel: 5);
            var clubB = NewClub(2, scoutLevel: 1);
            // B 의 선수가 A 에게는 외부 선수
            var pid = 301;
            var p = NewPlayer(pid, ca: 100, pa: 150);
            state.AddPlayer(p);
            clubB.seniorSquadIds.Add(pid);
            state.AddClub(clubA);
            state.AddClub(clubB);

            // 직접 외부 보고서 시드 (FacilityLevelSO 의존 회피)
            clubA.scoutingKnowledge[pid] = new ScoutReport
            {
                playerId = pid,
                scoutLevel = 30,
                lastUpdated = _today.AddDays(-7),
                caEstimate = new CaPaEstimate { estimate = 110, margin = 21 },
                paEstimate = new CaPaEstimate { estimate = 160, margin = 21 },
            };

            ScoutingSystem.UpdateKnowledge(state, _balance);

            var report = clubA.scoutingKnowledge[pid];
            Assert.AreEqual(35, report.scoutLevel, "T3: scoutLevel 30 → 35 (+gain 5)");
            Assert.Less(report.caEstimate.margin, 21, "T3: margin 감소");
            Assert.AreEqual(_today, report.lastUpdated, "T3: lastUpdated 갱신");
        }

        // ── T4. scoutLevel 100 도달 후 누적 X ─────────────────────────

        [Test]
        public void T4_MaxScoutLevel_NoFurtherAccumulation()
        {
            var state = NewState();
            var clubA = NewClub(1, scoutLevel: 5);
            var clubB = NewClub(2, scoutLevel: 1);
            var pid = 401;
            var p = NewPlayer(pid, ca: 100, pa: 150);
            state.AddPlayer(p);
            clubB.seniorSquadIds.Add(pid);
            state.AddClub(clubA);
            state.AddClub(clubB);

            clubA.scoutingKnowledge[pid] = new ScoutReport
            {
                playerId = pid,
                scoutLevel = 100, // 이미 max
                lastUpdated = _today.AddDays(-7),
                caEstimate = new CaPaEstimate { estimate = 100, margin = 0 },
                paEstimate = new CaPaEstimate { estimate = 150, margin = 0 },
            };

            ScoutingSystem.UpdateKnowledge(state, _balance);

            Assert.AreEqual(100, clubA.scoutingKnowledge[pid].scoutLevel, "T4: scoutLevel 변화 X");
        }

        // ── T5. 명단 추가 시 lastUpdated 갱신 ────────────────────────

        [Test]
        public void T5_OwnSquad_LastUpdatedToToday()
        {
            var state = NewState();
            var club = NewClub(1);
            var p = NewPlayer(501, ca: 100, pa: 150);
            state.AddPlayer(p);
            club.seniorSquadIds.Add(501);
            state.AddClub(club);

            ScoutingSystem.UpdateKnowledge(state, _balance);

            Assert.AreEqual(_today, club.scoutingKnowledge[501].lastUpdated);
        }

        // ── T6. 결정성 — 같은 시드 → 같은 명단 ────────────────────────

        [Test]
        public void T6_Determinism_SameSeedSameKnowledge()
        {
            var state1 = NewStateWithMultipleClubs(seed: 42);
            var state2 = NewStateWithMultipleClubs(seed: 42);

            ScoutingSystem.UpdateKnowledge(state1, _balance);
            ScoutingSystem.UpdateKnowledge(state2, _balance);

            var c1 = state1.GetClub(1);
            var c2 = state2.GetClub(1);
            Assert.AreEqual(
                c1.scoutingKnowledge.Count,
                c2.scoutingKnowledge.Count,
                "T6: 같은 시드 명단 크기 동일"
            );
        }

        // ── T7. 빈 state — no-op ──────────────────────────────────────

        [Test]
        public void T7_EmptyState_DoesNotThrow()
        {
            var state = NewState();
            Assert.DoesNotThrow(() => ScoutingSystem.UpdateKnowledge(state, _balance));
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState NewState() => new GameState { currentDate = _today, randomSeed = 42 };

        private GameState NewStateWithMultipleClubs(int seed)
        {
            var state = new GameState { currentDate = _today, randomSeed = seed };
            for (int cid = 1; cid <= 3; cid++)
            {
                var club = NewClub(cid);
                for (int i = 0; i < 5; i++)
                {
                    int pid = cid * 100 + i;
                    var p = NewPlayer(pid, ca: 100, pa: 150);
                    state.AddPlayer(p);
                    club.seniorSquadIds.Add(pid);
                }
                state.AddClub(club);
            }
            return state;
        }

        private static Player NewPlayer(int id, int ca, int pa) =>
            new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = pa,
                stats = new Stats(),
                info = new PersonalInfo { birthDate = new DateTime(2000, 1, 1) },
                state = new PlayerState { injury = new InjuryInfo { injuryTypeId = -1 } },
            };

        private static Club NewClub(int id, int scoutLevel = 1) =>
            new Club
            {
                id = id,
                leagueId = 1,
                facilities = new Facilities { scoutLevel = scoutLevel },
            };
    }
}
