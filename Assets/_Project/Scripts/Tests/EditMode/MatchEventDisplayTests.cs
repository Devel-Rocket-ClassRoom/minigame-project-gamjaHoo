// MatchEventDisplayTests.cs
// V1.0 Stage J (#497) — 매치 텍스트 이벤트 표시/SFX 분류 검증.
//   T1  ShouldShowText — 핵심 이벤트는 true
//   T2  ShouldShowText — 통계 전용(사소한) 이벤트는 false
//   T3  SfxFor — 골/카드/부상/교체/킥오프/풀타임 매핑
//   T4  SfxFor — 대응 SFX 없는 이벤트는 null

using FMLite.Application;
using FMLite.Domain;
using FMLite.UI;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class MatchEventDisplayTests
    {
        // ── T1. 핵심 이벤트 표시 ──────────────────────────────────────

        [Test]
        public void T1_ShouldShowText_CoreEvents_True()
        {
            Assert.IsTrue(MatchEventDisplay.ShouldShowText(MatchEventType.Goal), "Goal");
            Assert.IsTrue(MatchEventDisplay.ShouldShowText(MatchEventType.ShotSaved), "Save");
            Assert.IsTrue(MatchEventDisplay.ShouldShowText(MatchEventType.YellowCard), "Yellow");
            Assert.IsTrue(
                MatchEventDisplay.ShouldShowText(MatchEventType.Substitution),
                "Substitution"
            );
            Assert.IsTrue(MatchEventDisplay.ShouldShowText(MatchEventType.Corner), "Corner");
            Assert.IsTrue(
                MatchEventDisplay.ShouldShowText(MatchEventType.Dribble),
                "Dribble(키패스)"
            );
            Assert.IsTrue(MatchEventDisplay.ShouldShowText(MatchEventType.Offside), "Offside");
            Assert.IsTrue(MatchEventDisplay.ShouldShowText(MatchEventType.Injury), "Injury");
        }

        // ── T2. 사소한(통계 전용) 이벤트 숨김 ─────────────────────────

        [Test]
        public void T2_ShouldShowText_MinorEvents_False()
        {
            Assert.IsFalse(MatchEventDisplay.ShouldShowText(MatchEventType.PassCompleted), "Pass");
            Assert.IsFalse(
                MatchEventDisplay.ShouldShowText(MatchEventType.ShotOnTarget),
                "ShotOnTarget(통계)"
            );
            Assert.IsFalse(MatchEventDisplay.ShouldShowText(MatchEventType.ShotBlocked), "Blocked");
            Assert.IsFalse(MatchEventDisplay.ShouldShowText(MatchEventType.Tackle), "Tackle");
            Assert.IsFalse(
                MatchEventDisplay.ShouldShowText(MatchEventType.Interception),
                "Interception"
            );
            Assert.IsFalse(MatchEventDisplay.ShouldShowText(MatchEventType.Clearance), "Clearance");
            Assert.IsFalse(
                MatchEventDisplay.ShouldShowText(MatchEventType.ThrowIn),
                "ThrowIn(flavor)"
            );
        }

        // ── T3. SFX 매핑 ──────────────────────────────────────────────

        [Test]
        public void T3_SfxFor_MappedEvents()
        {
            Assert.AreEqual(SfxId.Goal, MatchEventDisplay.SfxFor(MatchEventType.Goal));
            Assert.AreEqual(SfxId.Goal, MatchEventDisplay.SfxFor(MatchEventType.PenaltyGoal));
            Assert.AreEqual(SfxId.CardYellow, MatchEventDisplay.SfxFor(MatchEventType.YellowCard));
            Assert.AreEqual(
                SfxId.CardYellow,
                MatchEventDisplay.SfxFor(MatchEventType.SecondYellow)
            );
            Assert.AreEqual(SfxId.CardRed, MatchEventDisplay.SfxFor(MatchEventType.RedCard));
            Assert.AreEqual(SfxId.Injury, MatchEventDisplay.SfxFor(MatchEventType.Injury));
            Assert.AreEqual(
                SfxId.Substitution,
                MatchEventDisplay.SfxFor(MatchEventType.Substitution)
            );
            Assert.AreEqual(SfxId.MatchKickoff, MatchEventDisplay.SfxFor(MatchEventType.KickOff));
            Assert.AreEqual(SfxId.MatchFulltime, MatchEventDisplay.SfxFor(MatchEventType.FullTime));
        }

        // ── T4. SFX 없는 이벤트 ───────────────────────────────────────

        [Test]
        public void T4_SfxFor_Unmapped_Null()
        {
            Assert.IsNull(MatchEventDisplay.SfxFor(MatchEventType.Corner));
            Assert.IsNull(MatchEventDisplay.SfxFor(MatchEventType.Foul));
            Assert.IsNull(MatchEventDisplay.SfxFor(MatchEventType.ShotSaved));
            Assert.IsNull(MatchEventDisplay.SfxFor(MatchEventType.PassCompleted));
        }
    }
}
