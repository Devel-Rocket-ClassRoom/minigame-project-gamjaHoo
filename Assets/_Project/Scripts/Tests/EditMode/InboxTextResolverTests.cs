// InboxTextResolverTests.cs
// Stage B.1 (#443) — 인박스 타이틀 state 해석 헬퍼 검증 (로컬라이제이션 불필요 부분).

using System;
using System.Collections.Generic;
using FMLite.UI;
using NUnit.Framework;
using D = FMLite.Domain;

namespace FMLite.Tests
{
    public class InboxTextResolverTests
    {
        private static D.GameState StateWithPlayer(
            int id,
            string first,
            string last,
            DateTime birth
        )
        {
            var s = new D.GameState
            {
                currentDate = new DateTime(2026, 8, 1),
                randomSeed = 1,
                nextPlayerId = 1,
            };
            s.AddPlayer(
                new D.Player
                {
                    id = id,
                    currentAbility = 70,
                    info = new D.PersonalInfo
                    {
                        firstName = first,
                        lastName = last,
                        birthDate = birth,
                    },
                }
            );
            return s;
        }

        [Test]
        public void ArgInt_ParsesOrMinusOne()
        {
            var a = new Dictionary<string, string> { { "id", "5" } };
            Assert.AreEqual(5, InboxTextResolver.ArgInt(a, "id"));
            Assert.AreEqual(-1, InboxTextResolver.ArgInt(a, "missing"));
            Assert.AreEqual(-1, InboxTextResolver.ArgInt(null, "id"));
        }

        [Test]
        public void ArgStr_ReturnsOrQuestion()
        {
            var a = new Dictionary<string, string> { { "days", "3" } };
            Assert.AreEqual("3", InboxTextResolver.ArgStr(a, "days"));
            Assert.AreEqual("?", InboxTextResolver.ArgStr(a, "missing"));
        }

        [Test]
        public void PlayerName_ResolvesFromState()
        {
            var s = StateWithPlayer(12, "Heungmin", "Son", new DateTime(1992, 7, 8));
            Assert.AreEqual("Heungmin Son", InboxTextResolver.PlayerName(s, 12));
        }

        [Test]
        public void PlayerName_MissingPlayer_Question()
        {
            var s = StateWithPlayer(12, "A", "B", new DateTime(2000, 1, 1));
            Assert.AreEqual("?", InboxTextResolver.PlayerName(s, 999));
        }

        [Test]
        public void ComputeAge_BeforeAndAfterBirthday()
        {
            // currentDate = 2026-08-01
            var s = StateWithPlayer(1, "A", "B", new DateTime(2000, 7, 8)); // 생일 지남 → 26
            Assert.AreEqual(26, InboxTextResolver.ComputeAge(s.GetPlayer(1), s));
            var s2 = StateWithPlayer(1, "A", "B", new DateTime(2000, 9, 20)); // 생일 안 지남 → 25
            Assert.AreEqual(25, InboxTextResolver.ComputeAge(s2.GetPlayer(1), s2));
        }

        [Test]
        public void PromiseTypeKey_Maps()
        {
            Assert.AreEqual(
                "promise_type_playtime",
                InboxTextResolver.PromiseTypeKey(D.PromiseType.PlaytimeAgreement)
            );
            Assert.AreEqual(
                "promise_type_transfer_in",
                InboxTextResolver.PromiseTypeKey(D.PromiseType.TransferIn)
            );
            Assert.AreEqual(
                "promise_type_renewal",
                InboxTextResolver.PromiseTypeKey(D.PromiseType.Renewal)
            );
            Assert.AreEqual(
                "promise_type_transfer_out",
                InboxTextResolver.PromiseTypeKey(D.PromiseType.TransferOut)
            );
        }

        [Test]
        public void ResolveTitle_NullItem_Empty()
        {
            Assert.AreEqual(string.Empty, InboxTextResolver.ResolveTitle(null, null));
        }
    }
}
