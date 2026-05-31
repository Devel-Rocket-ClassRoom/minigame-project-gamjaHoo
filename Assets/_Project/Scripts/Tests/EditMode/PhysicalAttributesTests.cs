// PhysicalAttributesTests.cs
// Task A.2 DoD:
//   T1  GK / CB 100명 생성 → 평균 키 > 183 cm
//   T2  LW 100명 생성 → 평균 키 < 180 cm
//   T3  혼합 포지션 100명 → preferredFoot=Right 비율 60~80%
//   T4  직렬화 라운드트립 — PhysicalAttributes 필드 보존
//   T5  height / weight Clamp 경계 (극단 seed)

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class PhysicalAttributesTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _date = new DateTime(2026, 6, 1);

        [SetUp]
        public void Setup() => _balance = ScriptableObject.CreateInstance<GameBalanceSO>();

        // ── T1. GK/CB 평균 키 > 183 ──────────────────────────────────

        [Test]
        public void T1_GkCb_AverageHeight_Above183()
        {
            var rng = new System.Random(42);
            var heights = new List<int>();

            for (int i = 0; i < 100; i++)
            {
                var p = PlayerGenerator.Generate(
                    rng,
                    50,
                    i % 2 == 0 ? Position.GK : Position.CB,
                    25,
                    "ENG",
                    1,
                    -1,
                    PlayerOrigin.InitialRoster,
                    _date,
                    _balance
                );
                heights.Add(p.physical.height);
            }

            double avg = heights.Average();
            Assert.Greater(avg, 183.0, $"T1: GK/CB 평균 키 {avg:F1} > 183");
        }

        // ── T2. LW 평균 키 < 180 ─────────────────────────────────────

        [Test]
        public void T2_Lw_AverageHeight_Below180()
        {
            var rng = new System.Random(42);
            var heights = new List<int>();

            for (int i = 0; i < 100; i++)
            {
                var p = PlayerGenerator.Generate(
                    rng,
                    50,
                    Position.LW,
                    22,
                    "ENG",
                    1,
                    -1,
                    PlayerOrigin.InitialRoster,
                    _date,
                    _balance
                );
                heights.Add(p.physical.height);
            }

            double avg = heights.Average();
            Assert.Less(avg, 180.0, $"T2: LW 평균 키 {avg:F1} < 180");
        }

        // ── T3. preferredFoot=Right 비율 60~80% ──────────────────────

        [Test]
        public void T3_PreferredFoot_RightRatio_60to80Percent()
        {
            var rng = new System.Random(42);
            int rightCount = 0;

            for (int i = 0; i < 100; i++)
            {
                var pos = (Position)(i % 14); // 모든 포지션 순환
                var p = PlayerGenerator.Generate(
                    rng,
                    50,
                    pos,
                    25,
                    "ENG",
                    1,
                    -1,
                    PlayerOrigin.InitialRoster,
                    _date,
                    _balance
                );
                if (p.physical.preferredFoot == Foot.Right)
                    rightCount++;
            }

            Assert.GreaterOrEqual(rightCount, 60, $"T3: Right 비율 {rightCount}% >= 60%");
            Assert.LessOrEqual(rightCount, 80, $"T3: Right 비율 {rightCount}% <= 80%");
        }

        // ── T4. 직렬화 라운드트립 ─────────────────────────────────────

        [Test]
        public void T4_Physical_SerializesAndDeserializes()
        {
            var rng = new System.Random(7);
            var player = PlayerGenerator.Generate(
                rng,
                70,
                Position.ST,
                24,
                "ENG",
                1,
                -1,
                PlayerOrigin.InitialRoster,
                _date,
                _balance
            );

            var json = JsonConvert.SerializeObject(player);
            var loaded = JsonConvert.DeserializeObject<Player>(json);

            Assert.IsNotNull(loaded.physical, "T4: physical not null");
            Assert.AreEqual(player.physical.height, loaded.physical.height, "T4: height");
            Assert.AreEqual(player.physical.weight, loaded.physical.weight, "T4: weight");
            Assert.AreEqual(
                player.physical.preferredFoot,
                loaded.physical.preferredFoot,
                "T4: foot"
            );
            Assert.AreEqual(
                player.physical.weakFootAbility,
                loaded.physical.weakFootAbility,
                "T4: weakFoot"
            );
        }

        // ── T5. height/weight Clamp 경계 ─────────────────────────────

        [Test]
        public void T5_HeightAndWeight_WithinClampBounds()
        {
            var rng = new System.Random(42);

            for (int i = 0; i < 500; i++)
            {
                var pos = (Position)(i % 14);
                var p = PlayerGenerator.Generate(
                    rng,
                    50,
                    pos,
                    20,
                    "ENG",
                    1,
                    -1,
                    PlayerOrigin.InitialRoster,
                    _date,
                    _balance
                );

                Assert.GreaterOrEqual(p.physical.height, 165, $"T5[{i}]: height >= 165");
                Assert.LessOrEqual(p.physical.height, 205, $"T5[{i}]: height <= 205");
                Assert.GreaterOrEqual(p.physical.weight, 60, $"T5[{i}]: weight >= 60");
                Assert.LessOrEqual(p.physical.weight, 100, $"T5[{i}]: weight <= 100");
                Assert.GreaterOrEqual(p.physical.weakFootAbility, 1, $"T5[{i}]: weakFoot >= 1");
                Assert.LessOrEqual(p.physical.weakFootAbility, 5, $"T5[{i}]: weakFoot <= 5");
            }
        }
    }
}
