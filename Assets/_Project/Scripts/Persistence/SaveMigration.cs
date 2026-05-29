// SaveMigration.cs
// 세이브 파일 버전 마이그레이션 인프라. design-decisions.md #52 / algorithms.md V0.5-8.
//
// V0.1 → V0.5 마이그레이션은 미지원 (Q8 결정). 인프라만 구축해 V0.5 → V1.0 후속 대비.
// 현재 버전: V0.5 = saveVersion 2.
// V0.1 세이브 (saveVersion 0 또는 1) 로드 시도 → NotSupportedException.

using System;
using System.Collections.Generic;
using FMLite.Domain;

namespace FMLite.Persistence
{
    public interface IMigrator
    {
        GameState Apply(GameState state);
    }

    public static class SaveMigration
    {
        public const int CurrentVersion = 2; // V0.5

        private static readonly Dictionary<int, IMigrator> Migrators = new Dictionary<
            int,
            IMigrator
        >
        {
            { 2, new MigratorV1_0() },
        };

        // -------------------------------------------------------------------
        // 핵심 API

        /// <summary>
        /// state.saveVersion 이 targetVersion 보다 낮으면 단계별 마이그레이션 적용.
        /// V0.1 세이브 (saveVersion 0 또는 1) → NotSupportedException.
        /// 해당 버전 마이그레이터 없음 → InvalidOperationException.
        /// </summary>
        public static GameState Migrate(GameState state, int targetVersion)
        {
            if (state.saveVersion >= targetVersion)
                return state;

            // saveVersion=0 은 V0.1 구형 세이브 (JSON 에 필드 없음 → 0으로 역직렬화).
            // 내부 처리상 version 1 과 동일하게 취급.
            if (state.saveVersion == 0)
                state.saveVersion = 1;

            while (state.saveVersion < targetVersion)
            {
                int nextVersion = state.saveVersion + 1;
                if (!Migrators.TryGetValue(nextVersion, out var migrator))
                    throw new InvalidOperationException(
                        $"No migrator registered for save version {nextVersion}. Cannot upgrade save file."
                    );

                state = migrator.Apply(state);
                state.saveVersion = nextVersion;
            }

            return state;
        }

        // -------------------------------------------------------------------
        // 테스트 / 확장 지원 (GameDatabase.Register 패턴과 동일)

        /// <summary>테스트나 플러그인에서 커스텀 마이그레이터 등록.</summary>
        public static void RegisterMigrator(int version, IMigrator migrator) =>
            Migrators[version] = migrator;

        /// <summary>등록한 테스트용 마이그레이터 제거.</summary>
        public static void RemoveMigrator(int version) => Migrators.Remove(version);
    }

    // -----------------------------------------------------------------------
    // V0.1 → V0.5 마이그레이터 (Q8: 미지원, 예외만 던짐)

    public class MigratorV1_0 : IMigrator
    {
        public GameState Apply(GameState state)
        {
            throw new NotSupportedException(
                "V0.1 save files are not compatible with V0.5. Please start a new game."
            );
        }
    }
}
