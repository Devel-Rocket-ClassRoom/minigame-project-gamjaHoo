// SaveMigrationTests.cs
// DoD 검증: v1.0-tasks.md Stage A / Task A.1 — SaveMigration 인프라.
// algorithms.md V1.0-8 Test Scenarios T1~T3.

using System;
using FMLite.Domain;
using FMLite.Persistence;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class SaveMigrationTests
    {
        // T1: V1.0 신규 세이브 로드 — saveVersion=2 이면 마이그레이션 없이 그대로 반환.
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

        // T2-b: V0.1 구형 세이브 (saveVersion=0, JSON 에 필드 없던 경우) → NotSupportedException.
        [Test]
        public void Migrate_V01SaveVersion0_ThrowsNotSupported()
        {
            var state = new GameState { saveVersion = 0 };

            Assert.Throws<NotSupportedException>(() =>
                SaveMigration.Migrate(state, SaveMigration.CurrentVersion)
            );
        }

        // T3: 인프라 라운드트립 — 가상 V2→V3 마이그레이터 등록 후 정상 마이그레이션 + saveVersion 갱신.
        [Test]
        public void Migrate_CustomMigrator_UpdatesSaveVersion()
        {
            const int targetVersion = 3;
            SaveMigration.RegisterMigrator(targetVersion, new PassThroughMigrator());

            try
            {
                var state = new GameState { saveVersion = SaveMigration.CurrentVersion };

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
            var state = new GameState { saveVersion = 1 };

            // version 1 → 2 는 MigratorV1_0 (NotSupported). version 3 은 등록 안 됨.
            // saveVersion=2 → target=3 인 경우 마이그레이터 없어서 InvalidOperationException.
            const int missingTarget = 99;
            var state2 = new GameState { saveVersion = 98 };
            Assert.Throws<InvalidOperationException>(() =>
                SaveMigration.Migrate(state2, missingTarget)
            );
        }

        // 테스트용 통과 마이그레이터 (아무 변환 없이 그대로 반환)
        private class PassThroughMigrator : IMigrator
        {
            public GameState Apply(GameState state) => state;
        }
    }
}
