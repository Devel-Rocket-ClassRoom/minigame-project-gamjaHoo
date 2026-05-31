// SaveMigrationTests.cs
// V0.5-8 Test Scenarios + V1.0 A.4 (saveVersion=3 / V0.5 차단).

using System;
using FMLite.Domain;
using FMLite.Persistence;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class SaveMigrationTests
    {
        // T1: V1.0 신규 세이브 — saveVersion=3 이면 마이그레이션 없이 그대로 반환.
        [Test]
        public void Migrate_V10State_ReturnsSameState()
        {
            var state = new GameState { saveVersion = SaveMigration.CurrentVersion };

            var result = SaveMigration.Migrate(state, SaveMigration.CurrentVersion);

            Assert.AreSame(state, result, "이미 최신 버전이면 같은 객체 반환");
            Assert.AreEqual(SaveMigration.CurrentVersion, result.saveVersion);
        }

        // T2-a: V0.1 세이브 (saveVersion=1) → NotSupportedException.
        [Test]
        public void Migrate_V01SaveVersion1_ThrowsNotSupported()
        {
            var state = new GameState { saveVersion = 1 };

            Assert.Throws<NotSupportedException>(() =>
                SaveMigration.Migrate(state, SaveMigration.CurrentVersion)
            );
        }

        // T2-b: V0.1 구형 세이브 (saveVersion=0) → NotSupportedException.
        [Test]
        public void Migrate_V01SaveVersion0_ThrowsNotSupported()
        {
            var state = new GameState { saveVersion = 0 };

            Assert.Throws<NotSupportedException>(() =>
                SaveMigration.Migrate(state, SaveMigration.CurrentVersion)
            );
        }

        // T2-c: V0.5 세이브 (saveVersion=2) → NotSupportedException (V1.0 A.4 / Q-MIG).
        [Test]
        public void Migrate_V05Save_ThrowsNotSupported()
        {
            var state = new GameState { saveVersion = 2 };

            var ex = Assert.Throws<NotSupportedException>(() =>
                SaveMigration.Migrate(state, SaveMigration.CurrentVersion)
            );
            StringAssert.Contains("V0.5", ex.Message, "T2-c: 에러 메시지에 V0.5 포함");
        }

        // T3: 인프라 라운드트립 — 가상 V3→V4 마이그레이터 등록 후 saveVersion 갱신.
        [Test]
        public void Migrate_CustomMigrator_UpdatesSaveVersion()
        {
            const int targetVersion = 4; // 현재 버전(3)보다 높은 임의 버전
            SaveMigration.RegisterMigrator(targetVersion, new PassThroughMigrator());

            try
            {
                var state = new GameState { saveVersion = SaveMigration.CurrentVersion }; // 3

                var result = SaveMigration.Migrate(state, targetVersion);

                Assert.AreEqual(
                    targetVersion,
                    result.saveVersion,
                    "마이그레이션 후 saveVersion 갱신"
                );
            }
            finally
            {
                SaveMigration.RemoveMigrator(targetVersion);
            }
        }

        // T3-b: 마이그레이터 없는 버전 → InvalidOperationException.
        [Test]
        public void Migrate_MissingMigratorVersion_ThrowsInvalidOperation()
        {
            const int missingTarget = 99;
            var state = new GameState { saveVersion = 98 };

            Assert.Throws<InvalidOperationException>(() =>
                SaveMigration.Migrate(state, missingTarget)
            );
        }

        // 테스트용 통과 마이그레이터
        private class PassThroughMigrator : IMigrator
        {
            public GameState Apply(GameState state) => state;
        }
    }
}
