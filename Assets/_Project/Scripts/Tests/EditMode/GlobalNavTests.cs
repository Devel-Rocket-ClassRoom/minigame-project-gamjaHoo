// GlobalNavTests.cs
// Stage W Sub-B (#419) — GlobalNav 순수 로직 EditMode 테스트.
// MonoBehaviour 인스턴스화 없이 static 헬퍼만 검증 (씬/prefab 의존 0).

using System;
using FMLite.UI;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class GlobalNavTests
    {
        // ── W.4 SideBar 강조 — 씬명 → 메뉴 인덱스 ─────────────────────

        [Test]
        public void GetMenuIndex_Dashboard_Returns0()
        {
            Assert.AreEqual(
                0,
                GlobalNavController.GetMenuIndex(GlobalNavController.DashboardScene)
            );
        }

        [Test]
        public void GetMenuIndex_Mentoring_ReturnsLast()
        {
            int last = GlobalNavController.SideBarScenes.Length - 1;
            Assert.AreEqual(
                last,
                GlobalNavController.GetMenuIndex(GlobalNavController.MentoringScene)
            );
        }

        [Test]
        public void GetMenuIndex_Squad_Returns1()
        {
            Assert.AreEqual(1, GlobalNavController.GetMenuIndex(GlobalNavController.SquadScene));
        }

        [TestCase("MainMenuScene")]
        [TestCase("OptionsScene")]
        [TestCase("PlayerProfileScene")]
        [TestCase("GachaScene")]
        [TestCase("")]
        [TestCase("NonExistentScene")]
        public void GetMenuIndex_NonSideBarScene_ReturnsMinus1(string scene)
        {
            Assert.AreEqual(-1, GlobalNavController.GetMenuIndex(scene));
        }

        [Test]
        public void SideBarScenes_Has9_InExpectedOrder()
        {
            // H.4: 구 TacticScene + LineupScene 버튼 폐기 → TacticLineupScene 통합 (10→9).
            var expected = new[]
            {
                "DashboardScene",
                "SquadScene",
                "TacticLineupScene",
                "TransferScene",
                "ScheduleScene",
                "StandingsScene",
                "FacilityScene",
                "YouthScene",
                "MentoringScene",
            };
            CollectionAssert.AreEqual(expected, GlobalNavController.SideBarScenes);
        }

        [Test]
        public void GetMenuIndex_TacticLineup_Returns2_And_DeprecatedScenesMinus1()
        {
            Assert.AreEqual(
                2,
                GlobalNavController.GetMenuIndex(GlobalNavController.TacticLineupScene)
            );
            // 구 분리 씬은 사이드바에서 폐기 → -1
            Assert.AreEqual(-1, GlobalNavController.GetMenuIndex("TacticScene"));
            Assert.AreEqual(-1, GlobalNavController.GetMenuIndex("LineupScene"));
        }

        [Test]
        public void SideBarScenes_NoDuplicates()
        {
            var set = new System.Collections.Generic.HashSet<string>(
                GlobalNavController.SideBarScenes
            );
            Assert.AreEqual(GlobalNavController.SideBarScenes.Length, set.Count);
        }

        // ── W.5 GlobalSavePanel — 자동 슬롯명 ────────────────────────

        [Test]
        public void GenerateAutoSlotName_Format()
        {
            var now = new DateTime(2026, 8, 15, 14, 30, 0);
            string slot = GlobalSavePanel.GenerateAutoSlotName("Arsenal", now);
            Assert.AreEqual("slot_Arsenal_260815_1430", slot);
        }

        [Test]
        public void GenerateAutoSlotName_SpacesReplaced()
        {
            var now = new DateTime(2026, 1, 5, 9, 5, 0);
            string slot = GlobalSavePanel.GenerateAutoSlotName("Man City", now);
            Assert.AreEqual("slot_Man_City_260105_0905", slot);
        }

        [Test]
        public void GenerateAutoSlotName_NullClub_FallsBackToUser()
        {
            var now = new DateTime(2026, 6, 1, 0, 0, 0);
            string slot = GlobalSavePanel.GenerateAutoSlotName(null, now);
            Assert.AreEqual("slot_user_260601_0000", slot);
        }

        [Test]
        public void SanitizeSlotName_ReplacesInvalidFileChars()
        {
            // '/' 와 ':' 는 파일명 불가 문자 → '_' 치환. 공백도 '_'.
            string sanitized = GlobalSavePanel.SanitizeSlotName("FC/Bar 1:2");
            StringAssert.DoesNotContain("/", sanitized);
            StringAssert.DoesNotContain(":", sanitized);
            StringAssert.DoesNotContain(" ", sanitized);
        }
    }
}
