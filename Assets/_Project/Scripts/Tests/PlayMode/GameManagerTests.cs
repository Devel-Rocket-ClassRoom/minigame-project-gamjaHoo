// GameManagerTests.cs
// DoD 검증: v0.1-tasks.md Task 2.3.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Tests
{
    public class GameManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameManager.Instance != null)
            {
                Object.DestroyImmediate(GameManager.Instance.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Instance_IsSetAfterAwake()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null;

            Assert.AreEqual(gm, GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator Duplicate_IsDestroyedAndOriginalRemains()
        {
            var go1 = new GameObject("GameManager_1");
            var gm1 = go1.AddComponent<GameManager>();
            yield return null;

            var go2 = new GameObject("GameManager_2");
            go2.AddComponent<GameManager>();
            yield return null;

            Assert.AreEqual(gm1, GameManager.Instance);
            Assert.IsTrue(go2 == null, "Duplicate GameObject should have been destroyed");
        }

        [UnityTest]
        public IEnumerator OnDestroy_ClearsInstance()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            yield return null;
            Assert.IsNotNull(GameManager.Instance);

            Object.DestroyImmediate(go);

            Assert.IsNull(GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator State_IsNullByDefault()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null;

            Assert.IsNull(gm.State);
            Assert.IsNull(gm.UserClub, "UserClub must be null when State is null");
        }

        [UnityTest]
        public IEnumerator SetState_AssignsState_AndUserClubResolves()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null;

            var state = new GameState { userClubId = 7 };
            state.AddClub(new Club { id = 7, name = "Test FC" });
            state.AddClub(new Club { id = 8, name = "Other FC" });

            gm.SetState(state);

            Assert.AreSame(state, gm.State);
            Assert.IsNotNull(gm.UserClub);
            Assert.AreEqual(7, gm.UserClub.id);
            Assert.AreEqual("Test FC", gm.UserClub.name);
        }

        [UnityTest]
        public IEnumerator UserClub_NullWhenUserClubIdNotPresent()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null;

            var state = new GameState { userClubId = 999 };
            state.AddClub(new Club { id = 1 });
            gm.SetState(state);

            Assert.IsNull(gm.UserClub, "UserClub must be null when userClubId is not in allClubs");
        }
    }
}
