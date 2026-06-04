// SynergyMatchupTests.cs
// G.3 시너지 검출 (TacticImpact.ComputeSynergies) + G.4 포메이션 상성 (FormationMatchupSO) + 매치 통합 (#478).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class SynergyMatchupTests
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

        // ── 헬퍼 ───────────────────────────────────────────────────────────
        private static Player MkPlayer(int id, Position pos, int height = 180, string nat = "KOR")
        {
            return new Player
            {
                id = id,
                currentAbility = 100,
                info = new PersonalInfo
                {
                    primaryPosition = pos,
                    nationalityCode = nat,
                    firstName = "F",
                    lastName = "L",
                },
                stats = new Stats(),
                physical = new PhysicalAttributes
                {
                    height = height,
                    weight = 78,
                    preferredFoot = Foot.Right,
                    weakFootAbility = 3,
                },
                state = new PlayerState { injury = new InjuryInfo { injuryTypeId = -1 } },
                hiddenAttrs = new HiddenAttributes { injuryProneness = 50 },
            };
        }

        private static (GameState state, Tactic tactic) BuildLineup(params Player[] players)
        {
            var state = new GameState
            {
                randomSeed = 1,
                currentDate = new DateTime(2025, 8, 15),
            };
            var tactic = new Tactic { formationId = 1 };
            int slot = 0;
            foreach (var p in players)
            {
                state.AddPlayer(p);
                tactic.slots.Add(
                    new TacticSlot
                    {
                        slotIndex = slot++,
                        assignedPlayerId = p.id,
                        roleId = -1,
                    }
                );
            }
            return (state, tactic);
        }

        private static SynergySO MkSynergy(int id, float bonus, params SynergyCondition[] conds)
        {
            var s = ScriptableObject.CreateInstance<SynergySO>();
            s.id = id;
            s.strengthBonus = bonus;
            s.conditions = conds.ToList();
            return s;
        }

        private static SynergyCondition Cond(
            string stat,
            int minCount = 1,
            bool sameNat = false,
            params Position[] pos
        ) =>
            new SynergyCondition
            {
                positions = pos.ToList(),
                statRequirement = stat,
                minCount = minCount,
                requireSameNationality = sameNat,
            };

        // ── T1. stat 조건 검출 / 미충족 ─────────────────────────────────────
        [Test]
        public void T1_Synergy_StatCondition()
        {
            var tall = MkPlayer(1, Position.ST, height: 192);
            var (state, tactic) = BuildLineup(tall);
            GameDatabase.Register(MkSynergy(1, 1.10f, Cond("height>=188", 1, false, Position.ST)));
            Assert.AreEqual(1, TacticImpact.ComputeSynergies(tactic, state).Count, "T1: 키 큰 ST 활성");

            var (state2, tactic2) = BuildLineup(MkPlayer(1, Position.ST, height: 178));
            GameDatabase.Clear();
            GameDatabase.Register(MkSynergy(1, 1.10f, Cond("height>=188", 1, false, Position.ST)));
            Assert.AreEqual(
                0,
                TacticImpact.ComputeSynergies(tactic2, state2).Count,
                "T1: 작은 ST 미활성"
            );
        }

        // ── T2. 멀티 stat AND + minCount=2 ──────────────────────────────────
        [Test]
        public void T2_Synergy_MultiStatAndCount()
        {
            var cm1 = MkPlayer(1, Position.CM);
            cm1.stats.technical.passing = 85;
            cm1.stats.mental.vision = 80;
            var cm2 = MkPlayer(2, Position.AM);
            cm2.stats.technical.passing = 82;
            cm2.stats.mental.vision = 78;
            var (state, tactic) = BuildLineup(cm1, cm2);
            GameDatabase.Register(
                MkSynergy(
                    1,
                    1.05f,
                    Cond(
                        "technical.passing>=80 & mental.vision>=75",
                        2,
                        false,
                        Position.CM,
                        Position.AM
                    )
                )
            );
            Assert.AreEqual(1, TacticImpact.ComputeSynergies(tactic, state).Count, "T2: 2명 충족 활성");

            // 1명만 충족 → 미활성
            cm2.stats.mental.vision = 50;
            Assert.AreEqual(
                0,
                TacticImpact.ComputeSynergies(tactic, state).Count,
                "T2: minCount=2 미달 → 미활성"
            );
        }

        // ── T3. 자국인 라인 (requireSameNationality) ────────────────────────
        [Test]
        public void T3_Synergy_SameNationality()
        {
            var gk = MkPlayer(1, Position.GK, nat: "KOR");
            var cb = MkPlayer(2, Position.CB, nat: "KOR");
            var dm = MkPlayer(3, Position.DM, nat: "KOR");
            var st = MkPlayer(4, Position.ST, nat: "KOR");
            var (state, tactic) = BuildLineup(gk, cb, dm, st);
            GameDatabase.Register(
                MkSynergy(
                    1,
                    1.03f,
                    Cond(
                        "",
                        4,
                        true,
                        Position.GK,
                        Position.CB,
                        Position.DM,
                        Position.ST
                    )
                )
            );
            Assert.AreEqual(1, TacticImpact.ComputeSynergies(tactic, state).Count, "T3: 4인 자국 활성");

            st.info.nationalityCode = "ENG"; // 3인만 동일
            Assert.AreEqual(
                0,
                TacticImpact.ComputeSynergies(tactic, state).Count,
                "T3: 3인 → minCount=4 미달"
            );
        }

        // ── T4. tactic/라인업 미배정 → 빈 목록 (회귀 가드) ──────────────────
        [Test]
        public void T4_Synergy_NoTacticOrLineup_Empty()
        {
            var (state, _) = BuildLineup(MkPlayer(1, Position.ST, height: 192));
            GameDatabase.Register(MkSynergy(1, 1.10f, Cond("height>=188", 1, false, Position.ST)));
            Assert.IsEmpty(TacticImpact.ComputeSynergies(null, state), "T4: tactic null → 빈");
            var empty = new Tactic { formationId = 1 }; // 슬롯 미배정
            Assert.IsEmpty(TacticImpact.ComputeSynergies(empty, state), "T4: 미배정 → 빈");
        }

        // ── T5. FormationMatchupSO.Get ──────────────────────────────────────
        [Test]
        public void T5_FormationMatchup_Get()
        {
            var so = ScriptableObject.CreateInstance<FormationMatchupSO>();
            so.matchups = new List<MatchupEntry>
            {
                new MatchupEntry
                {
                    homeFormationId = 2,
                    awayFormationId = 6,
                    homeBonus = 1.07f,
                }, // 4-3-3 vs 5-3-2
            };
            Assert.AreEqual(1.07f, so.Get(2, 6), 1e-5f, "T5: 정의된 매치업 bonus");
            Assert.AreEqual(1.0f, so.Get(6, 2), 1e-5f, "T5: 미정의 → 1.0");
            Assert.AreEqual(1.0f, so.Get(1, 1), 1e-5f, "T5: 동일 포메이션 → 1.0");
        }

        // ── T6. 매치 통합 — 활성 시너지가 홈 득점/xG ↑ (페어드) ────────────────
        // teamMod 은 xG(찬스 품질)에만 적용 → 단조. 신호 명확화 위해 1.5 사용 (실제 카탈로그는 1.05).
        [Test]
        public void T6_Integration_SynergyBoostsHome_Paired()
        {
            int withGoals = 0,
                withoutGoals = 0;
            double withXg = 0,
                withoutXg = 0;
            const int N = 150;
            var seedGen = new System.Random(99);
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();

                GameDatabase.Clear();
                GameDatabase.Register(
                    MkSynergy(1, 1.5f, Cond("", 4, true, Position.GK, Position.CB, Position.DM, Position.ST))
                );
                var (s1, m1) = BuildMatchState(seed);
                var r1 = MatchSimulator.Simulate(m1, s1, _balance);
                withGoals += r1.homeScore;
                withXg += HomeXg(r1);

                GameDatabase.Clear(); // 시너지 미등록
                var (s2, m2) = BuildMatchState(seed);
                var r2 = MatchSimulator.Simulate(m2, s2, _balance);
                withoutGoals += r2.homeScore;
                withoutXg += HomeXg(r2);
            }
            Debug.Log(
                $"[T6] 시너지: 골={withGoals} xG={withXg:F1} / 무시너지: 골={withoutGoals} xG={withoutXg:F1}"
            );
            Assert.Greater(withXg, withoutXg, "T6: 활성 시너지 → 홈 찬스 품질(xG) 우세");
            Assert.Greater(withGoals, withoutGoals, "T6: 활성 시너지 → 홈 득점 우세");
        }

        // ── T7. 매치 통합 — 포메이션 상성 우세 팀 득점/xG ↑ (페어드) ────────────
        [Test]
        public void T7_Integration_FormationMatchupAdvantage_Paired()
        {
            int favorGoals = 0,
                neutralGoals = 0;
            double favorXg = 0,
                neutralXg = 0;
            const int N = 150;
            var seedGen = new System.Random(77);
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();

                GameDatabase.Clear();
                var mu = ScriptableObject.CreateInstance<FormationMatchupSO>();
                mu.matchups = new List<MatchupEntry>
                {
                    new MatchupEntry
                    {
                        homeFormationId = 1,
                        awayFormationId = 2,
                        homeBonus = 1.5f,
                    },
                };
                GameDatabase.Register(mu);
                var (s1, m1) = BuildMatchState(seed); // home formation 1, away 2
                var r1 = MatchSimulator.Simulate(m1, s1, _balance);
                favorGoals += r1.homeScore;
                favorXg += HomeXg(r1);

                GameDatabase.Clear(); // 매치업 미등록 → 1.0
                var (s2, m2) = BuildMatchState(seed);
                var r2 = MatchSimulator.Simulate(m2, s2, _balance);
                neutralGoals += r2.homeScore;
                neutralXg += HomeXg(r2);
            }
            Debug.Log(
                $"[T7] 상성우세: 골={favorGoals} xG={favorXg:F1} / 중립: 골={neutralGoals} xG={neutralXg:F1}"
            );
            Assert.Greater(favorXg, neutralXg, "T7: 포메이션 상성 우세 → 홈 찬스 품질(xG) 우세");
            Assert.Greater(favorGoals, neutralGoals, "T7: 포메이션 상성 우세 → 홈 득점 우세");
        }

        private static double HomeXg(MatchResult r) =>
            r.playerStats.Where(p => r.homeStarting11.Contains(p.playerId)).Sum(p => p.xg);

        // 2클럽 풀 라인업 매치 상태 (home formation=1, away formation=2). home 전원 KOR (자국 시너지 충족).
        private (GameState, Match) BuildMatchState(int seed)
        {
            var state = new GameState
            {
                randomSeed = seed,
                currentDate = new DateTime(2025, 8, 15),
            };
            var home = NewClub(1, formationId: 1);
            var away = NewClub(2, formationId: 2);
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddLineup(state, home, nextId, "KOR", uniform: true); // 전원 자국 → 시너지 충족
            nextId = AddLineup(state, away, nextId, "X", uniform: false); // 국적 분산 → 미충족
            var match = new Match
            {
                id = seed,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };
            return (state, match);
        }

        private static Club NewClub(int id, int formationId) =>
            new Club
            {
                id = id,
                name = "C" + id,
                reputation = 60,
                facilities = new Facilities { medicalLevel = 1, gymLevel = 1 },
                tactic = new Tactic { formationId = formationId },
            };

        // 11 슬롯 (4-4-2) 풀 라인업 + 동일 stat. tactic.slots 배정.
        // uniform=true → 전원 같은 국적(nat) / false → 선수마다 다른 국적(nat+index, 시너지 미충족).
        private static int AddLineup(
            GameState state,
            Club club,
            int nextId,
            string nat,
            bool uniform
        )
        {
            var slots = new[]
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
            for (int i = 0; i < slots.Length; i++)
            {
                var p = MkPlayer(nextId, slots[i], nat: uniform ? nat : nat + i);
                p.stats.technical.ApplyToAll(_ => 55);
                p.stats.mental.ApplyToAll(_ => 55);
                p.stats.physical.ApplyToAll(_ => 55);
                p.stats.gk.ApplyToAll(_ => 55);
                state.AddPlayer(p);
                club.seniorSquadIds.Add(nextId);
                club.tactic.slots.Add(
                    new TacticSlot
                    {
                        slotIndex = i,
                        assignedPlayerId = nextId,
                        roleId = -1,
                    }
                );
                nextId++;
            }
            return nextId;
        }
    }
}
