// GameManagerTests.cs
// DoD 검증: v0.1-tasks.md Task 2.3.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FMLite.Core;

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
    }
}
