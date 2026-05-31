// GrowthHistoryTests.cs
// Task A.3 DoD:
//   T1  3개월 GrowthSystem.Tick → growthHistory.Count == 3
//   T2  GetStatChange — 3개월 변화량 정확 (known delta 검증)
//   T3  StatSnapshot 직렬화 라운드트립
//   T4  growthHistory 데이터 부족 시 GetStatChange == 0
//   T5  Stats.Clone() 독립성 — 원본 변경이 스냅샷에 영향 없음

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class GrowthHistoryTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _start = new DateTime(2026, 6, 1);

        [SetUp]
        public void Setup() =>
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();

        // ── T1. 3개월 Tick → growthHistory.Count == 3 ────────────────

        [Test]
        public void T1_ThreeMonthTicks_HistoryCountEqualsThree()
        {
            var state = BuildState(_start);
            var player = state.GetPlayer(1);

            TickMonths(state, 3);

            Assert.AreEqual(3, player.growthHistory.Count, "T1: 3개월 후 Count=3");
            Assert.AreEqual(2026, player.growthHistory[0].year, "T1: 1st year");
            Assert.AreEqual(6, player.growthHistory[0].month, "T1: 1st month=6");
            Assert.AreEqual(8, player.growthHistory[2].month, "T1: 3rd month=8");
        }

        // ── T2. GetStatChange — 3개월 변화량 정확 ────────────────────

        [Test]
        public void T2_GetStatChange_ReturnsCorrectDelta()
        {
            var state = BuildState(_start);
            var player = state.GetPlayer(1);

            // 1개월 Tick → 스냅샷[0] = passing:50
            TickMonths(state, 1);

            // 수동으로 passing 변경 (+5)
            player.stats.technical.passing = 55;

            // 2개월 Tick → 스냅샷[1] = passing:55
            TickMonths(state, 1, monthOffset: 1);

            // 다시 passing 변경 (+3)
            player.stats.technical.passing = 58;

            // 3개월 Tick → 스냅샷[2] = passing:58
            TickMonths(state, 1, monthOffset: 2);

            // GetStatChange(3): current(58) - snapshot[Count-3=0](50) = +8
            // 단, Tick 이후 추가 변경이 없으므로 current = player.stats.technical.passing = 58
            int change = GrowthSystem.GetStatChange(player, "technical.passing", 3);
            Assert.AreEqual(8, change, "T2: 3개월 변화량 +8");
        }

        // ── T3. StatSnapshot 직렬화 라운드트립 ───────────────────────

        [Test]
        public void T3_StatSnapshot_SerializesAndDeserializes()
        {
            var state = BuildState(_start);
            TickMonths(state, 1);

            var player = state.GetPlayer(1);
            var json = JsonConvert.SerializeObject(player);
            var loaded = JsonConvert.DeserializeObject<Player>(json);

            Assert.IsNotNull(loaded.growthHistory, "T3: history not null");
            Assert.AreEqual(1, loaded.growthHistory.Count, "T3: 1개");
            var snap = loaded.growthHistory[0];
            Assert.AreEqual(2026, snap.year, "T3: year");
            Assert.AreEqual(6, snap.month, "T3: month");
            Assert.IsNotNull(snap.stats, "T3: stats not null");
            Assert.AreEqual(50, snap.stats.technical.passing, "T3: passing 50 보존");
        }

        // ── T4. 데이터 부족 시 GetStatChange == 0 ─────────────────────

        [Test]
        public void T4_GetStatChange_InsufficientHistory_ReturnsZero()
        {
            var state = BuildState(_start);
            var player = state.GetPlayer(1);

            TickMonths(state, 2); // 2개월만 — 3개월 미달

            int change = GrowthSystem.GetStatChange(player, "technical.passing", 3);
            Assert.AreEqual(0, change, "T4: 히스토리 부족 → 0");
        }

        // ── T5. Stats.Clone() 독립성 ──────────────────────────────────

        [Test]
        public void T5_StatsClone_IsIndependent()
        {
            var original = new Stats();
            original.technical.ApplyToAll(_ => 50);
            original.mental.ApplyToAll(_ => 50);
            original.physical.ApplyToAll(_ => 50);
            original.gk.ApplyToAll(_ => 50);

            var clone = original.Clone();
            original.technical.passing = 99; // 원본 변경

            Assert.AreEqual(50, clone.technical.passing, "T5: 클론은 영향 없음");
            Assert.AreEqual(99, original.technical.passing, "T5: 원본은 변경됨");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState BuildState(DateTime date)
        {
            var state = new GameState { currentDate = date, randomSeed = 42 };
            var club = new Club
            {
                id = 1,
                facilities = new Facilities { trainingLevel = 5, gymLevel = 5 },
                seniorSquadIds = new List<int> { 1 },
            };
            var stats = new Stats();
            stats.technical.ApplyToAll(_ => 50);
            stats.mental.ApplyToAll(_ => 50);
            stats.physical.ApplyToAll(_ => 50);
            stats.gk.ApplyToAll(_ => 50);
            var player = new Player
            {
                id = 1,
                info = new PersonalInfo { birthDate = new DateTime(2003, 1, 1) },
                stats = stats,
                currentAbility = 80,
                potentialAbility = 150,
                state = new PlayerState { injury = new InjuryInfo { injuryTypeId = -1 } },
            };
            state.AddPlayer(player);
            state.AddClub(club);
            return state;
        }

        // monthOffset: 0=첫 달(6월), 1=두 번째(7월) ...
        private void TickMonths(GameState state, int count, int monthOffset = 0)
        {
            for (int i = 0; i < count; i++)
            {
                state.currentDate = _start.AddMonths(monthOffset + i);
                GrowthSystem.Tick(state, _balance);
            }
        }
    }
}
