// LocalizationImporter.cs
// LocalizationSO 에 JSON 파일로 일괄 import. design-decisions.md #52.
//
// JSON 형식:
//   [{ "key": "reroll_button", "korean": "리롤", "english": "Reroll" }, ...]
//
// 사용법: 에디터에서 LocalizationSO 선택 후
//   FM-Lite/Localization/Import from JSON → 파일 선택.

using System.Collections.Generic;
using System.IO;
using FMLite.Domain;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FMLite.Editor
{
    public static class LocalizationImporter
    {
        [MenuItem("FM-Lite/Localization/Import from JSON")]
        public static void ImportFromJson()
        {
            var so = Selection.activeObject as LocalizationSO;
            if (so == null)
            {
                EditorUtility.DisplayDialog(
                    "LocalizationImporter",
                    "Project 창에서 LocalizationSO asset 을 선택한 후 실행하세요.",
                    "OK"
                );
                return;
            }

            var path = EditorUtility.OpenFilePanel(
                "JSON 파일 선택",
                UnityEngine.Application.dataPath,
                "json"
            );
            if (string.IsNullOrEmpty(path))
                return;

            var json = File.ReadAllText(path);
            var imported = JsonConvert.DeserializeObject<List<LocalizationEntry>>(json);
            if (imported == null || imported.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "LocalizationImporter",
                    "항목이 없거나 파싱 실패.",
                    "OK"
                );
                return;
            }

            Undo.RecordObject(so, "LocalizationSO Import");
            so.entries.Clear();
            so.entries.AddRange(imported);
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "LocalizationImporter",
                $"{imported.Count}개 항목 임포트 완료.",
                "OK"
            );
        }
    }
}
