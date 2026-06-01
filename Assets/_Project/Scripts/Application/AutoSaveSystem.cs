// AutoSaveSystem.cs
// Stage X.7 (V1.0) — 자동 저장 트리거 (design-decisions #59 / v1.0-plan §3.20.3, Q10).
// OptionsManager.AutoSave ON 시: 매월 1일(DailyProcessor) / 시즌 종료 5/15(SeasonEndProcessor) / 새 시즌 6/1(NewSeasonProcessor) 자동 저장.
// 슬롯명: autosave_<클럽>_<YYYY-MM> (월간) / autosave_<클럽>_<연도>_<event> (시즌).
// 최근 3개만 보관, 나머지 자동 삭제.
//
// Application → Persistence (SaveSystem) 참조 — 순환 없음 (Persistence 는 Core/Domain 만 참조).
// 슬롯명 생성 / 삭제 대상 선정은 순수 static 헬퍼 (테스트 대상). 실제 I/O 는 SaveSystem.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMLite.Domain;
using FMLite.Persistence;

namespace FMLite.Application
{
    public static class AutoSaveSystem
    {
        public const string Prefix = "autosave_";
        public const int KeepRecent = 3;

        /// <summary>매월 1일 호출 (DailyProcessor). AutoSave OFF 면 no-op.</summary>
        public static void MonthlyAutoSave(GameState state)
        {
            if (state == null || !OptionsManager.AutoSave)
                return;
            SaveAndPrune(state, MonthlySlotName(ClubName(state), state.currentDate));
        }

        /// <summary>시즌 이벤트 (종료 5/15 / 새 시즌 6/1) 호출. AutoSave OFF 면 no-op.</summary>
        public static void EventAutoSave(GameState state, string eventTag)
        {
            if (state == null || !OptionsManager.AutoSave)
                return;
            SaveAndPrune(state, EventSlotName(ClubName(state), state.currentDate.Year, eventTag));
        }

        private static void SaveAndPrune(GameState state, string slotName)
        {
            SaveSystem.Save(state, slotName);
            foreach (var meta in SelectToDelete(ListAutosaves(), KeepRecent))
                SaveSystem.DeleteSlot(meta.slotName);
        }

        private static List<SaveSlotMeta> ListAutosaves() =>
            SaveSystem.ListSlots().Where(m => m.slotName.StartsWith(Prefix)).ToList();

        // ── 순수 헬퍼 (테스트 대상) ──────────────────────────────────

        /// <summary>최근 keep 개를 제외한 (오래된) 자동 저장 슬롯 = 삭제 대상.</summary>
        public static List<SaveSlotMeta> SelectToDelete(List<SaveSlotMeta> autosaves, int keep)
        {
            if (autosaves == null)
                return new List<SaveSlotMeta>();
            return autosaves.OrderByDescending(m => m.savedAt).Skip(Math.Max(0, keep)).ToList();
        }

        public static string MonthlySlotName(string clubName, DateTime date) =>
            $"{Prefix}{Sanitize(clubName)}_{date:yyyy-MM}";

        public static string EventSlotName(string clubName, int seasonYear, string eventTag) =>
            $"{Prefix}{Sanitize(clubName)}_{seasonYear}_{eventTag}";

        public static string Sanitize(string name)
        {
            name ??= "user";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        private static string ClubName(GameState state)
        {
            var club = state?.GetClub(state.userClubId);
            return string.IsNullOrEmpty(club?.name) ? "user" : club.name;
        }
    }
}
