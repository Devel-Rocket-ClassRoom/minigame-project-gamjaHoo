// GameDatabaseTests.cs
// DoD 검증: v0.1-tasks.md Task 5.2 — Register/Get 동작 + null 안전.
// Resources.LoadAll 자체는 UnityEngine 의존이라 in-memory ScriptableObject.CreateInstance 로 검증.

using NUnit.Framework;
using UnityEngine;
using FMLite.Domain;

namespace FMLite.Tests
{
    public class GameDatabaseTests
    {
        [SetUp]
        public void Setup() => GameDatabase.Clear();

        [TearDown]
        public void TearDown() => GameDatabase.Clear();

        [Test]
        public void Register_Trait_RetrievableById()
        {
            var t = ScriptableObject.CreateInstance<TraitSO>();
            t.id = 42;
            t.displayName = "Test Trait";

            GameDatabase.Register(t);

            Assert.AreSame(t, GameDatabase.GetTrait(42));
        }

        [Test]
        public void Register_Position_RetrievableById()
        {
            var p = ScriptableObject.CreateInstance<PositionSO>();
            p.id = 7;
            p.position = Position.ST;

            GameDatabase.Register(p);

            Assert.AreSame(p, GameDatabase.GetPosition(7));
        }

        [Test]
        public void Register_NamePool_KeyedByCountryId()
        {
            var n = ScriptableObject.CreateInstance<NamePoolSO>();
            n.countryId = 5;

            GameDatabase.Register(n);

            Assert.AreSame(n, GameDatabase.GetNamePool(5));
        }

        [Test]
        public void Register_FacilityLevel_LookupByTypeAndLevel()
        {
            var youthLv3 = ScriptableObject.CreateInstance<FacilityLevelSO>();
            youthLv3.facilityType = FacilityType.Youth;
            youthLv3.level = 3;
            var scoutLv3 = ScriptableObject.CreateInstance<FacilityLevelSO>();
            scoutLv3.facilityType = FacilityType.Scout;
            scoutLv3.level = 3;

            GameDatabase.Register(youthLv3);
            GameDatabase.Register(scoutLv3);

            Assert.AreSame(youthLv3, GameDatabase.GetFacilityLevel(FacilityType.Youth, 3));
            Assert.AreSame(scoutLv3, GameDatabase.GetFacilityLevel(FacilityType.Scout, 3));
            Assert.IsNull(GameDatabase.GetFacilityLevel(FacilityType.Training, 3));
        }

        [Test]
        public void Register_GameBalance_AccessibleViaSingletonProperty()
        {
            var b = ScriptableObject.CreateInstance<GameBalanceSO>();
            b.isDebugMode = false;

            GameDatabase.Register(b);

            Assert.AreSame(b, GameDatabase.GameBalance);
            Assert.IsFalse(GameDatabase.GameBalance.isDebugMode);
        }

        [Test]
        public void Get_NonexistentId_ReturnsNull()
        {
            Assert.IsNull(GameDatabase.GetTrait(999));
            Assert.IsNull(GameDatabase.GetPosition(999));
            Assert.IsNull(GameDatabase.GetCountry(999));
            Assert.IsNull(GameDatabase.GetNamePool(999));
            Assert.IsNull(GameDatabase.GetLeagueConfig(999));
            Assert.IsNull(GameDatabase.GetFacilityLevel(FacilityType.Youth, 99));
            Assert.IsNull(GameDatabase.GameBalance);
        }

        [Test]
        public void Clear_EmptiesAllRegistries()
        {
            var t = ScriptableObject.CreateInstance<TraitSO>();
            t.id = 1;
            GameDatabase.Register(t);
            Assert.IsNotNull(GameDatabase.GetTrait(1));

            GameDatabase.Clear();

            Assert.IsNull(GameDatabase.GetTrait(1));
        }

        [Test]
        public void AllTraits_EnumeratesRegisteredEntries()
        {
            var t1 = ScriptableObject.CreateInstance<TraitSO>();
            t1.id = 1;
            var t2 = ScriptableObject.CreateInstance<TraitSO>();
            t2.id = 2;
            GameDatabase.Register(t1);
            GameDatabase.Register(t2);

            int count = 0;
            foreach (var _ in GameDatabase.AllTraits) count++;
            Assert.AreEqual(2, count);
        }
    }
}
