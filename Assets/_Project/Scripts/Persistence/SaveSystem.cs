// SaveSystem.cs
// GameState 직렬화 기반 세이브/로드. design-decisions.md #21 (Newtonsoft.Json).
//
// 슬롯당 폴더 1개. 각 폴더 안:
//   state.json   — GameState 전체 (로드 후 BuildIndexes 호출됨)
//   meta.json    — SaveSlotMeta (슬롯 목록 화면 빠른 조회용)
//
// 부분 쓰기 방지: state.json 은 .tmp 파일에 먼저 쓰고 File.Replace 로
// 원자적 교체. 도중 크래시 시 기존 state.json 무손상.

using System;
using System.Collections.Generic;
using System.IO;
using FMLite.Core;
using FMLite.Domain;
using Newtonsoft.Json;
using UnityEngine;

namespace FMLite.Persistence
{
    public static class SaveSystem
    {
        private const string SaveFolder = "saves";
        private const string StateFile = "state.json";
        private const string MetaFile = "meta.json";

        public static string SavesPath => Path.Combine(Application.persistentDataPath, SaveFolder);

        public static string GetSlotPath(string slotName) => Path.Combine(SavesPath, slotName);

        public static void Save(GameState state, string slotName)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("slotName must be non-empty", nameof(slotName));

            // 직렬화 직전 현재 버전 스탬프 (design-decisions.md #52).
            state.saveVersion = SaveMigration.CurrentVersion;

            var slotPath = GetSlotPath(slotName);
            Directory.CreateDirectory(slotPath);

            var stateJson = JsonConvert.SerializeObject(state, Formatting.Indented);
            AtomicWrite(Path.Combine(slotPath, StateFile), stateJson);

            var meta = new SaveSlotMeta
            {
                slotName = slotName,
                savedAt = DateTime.Now,
                currentDate = state.currentDate,
                userClubId = state.userClubId,
                userClubName = state.GetClub(state.userClubId)?.name ?? string.Empty,
            };
            var metaJson = JsonConvert.SerializeObject(meta, Formatting.Indented);
            AtomicWrite(Path.Combine(slotPath, MetaFile), metaJson);

            EventBus.Publish(new GameSavedEvent { slotName = slotName });
        }

        public static GameState Load(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("slotName must be non-empty", nameof(slotName));

            var statePath = Path.Combine(GetSlotPath(slotName), StateFile);
            if (!File.Exists(statePath))
                return null;

            var json = File.ReadAllText(statePath);
            var state = JsonConvert.DeserializeObject<GameState>(json);

            if (state != null)
            {
                // saveVersion 이 현재 버전보다 낮으면 마이그레이션 시도.
                // V0.1 세이브 → NotSupportedException (Q8).
                if (state.saveVersion < SaveMigration.CurrentVersion)
                    state = SaveMigration.Migrate(state, SaveMigration.CurrentVersion);

                state.BuildIndexes();
                EventBus.Publish(new GameLoadedEvent());
            }

            return state;
        }

        public static SaveSlotMeta LoadSlotMeta(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("slotName must be non-empty", nameof(slotName));

            var metaPath = Path.Combine(GetSlotPath(slotName), MetaFile);
            if (!File.Exists(metaPath))
                return null;

            var json = File.ReadAllText(metaPath);
            return JsonConvert.DeserializeObject<SaveSlotMeta>(json);
        }

        public static List<SaveSlotMeta> ListSlots()
        {
            var result = new List<SaveSlotMeta>();
            if (!Directory.Exists(SavesPath))
                return result;

            foreach (var dir in Directory.GetDirectories(SavesPath))
            {
                var slotName = Path.GetFileName(dir);
                var meta = LoadSlotMeta(slotName);
                if (meta != null)
                    result.Add(meta);
            }
            return result;
        }

        public static bool DeleteSlot(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("slotName must be non-empty", nameof(slotName));

            var slotPath = GetSlotPath(slotName);
            if (!Directory.Exists(slotPath))
                return false;

            Directory.Delete(slotPath, recursive: true);
            return true;
        }

        private static void AtomicWrite(string finalPath, string content)
        {
            var tmpPath = finalPath + ".tmp";
            File.WriteAllText(tmpPath, content);
            if (File.Exists(finalPath))
            {
                File.Replace(tmpPath, finalPath, null);
            }
            else
            {
                File.Move(tmpPath, finalPath);
            }
        }
    }
}
