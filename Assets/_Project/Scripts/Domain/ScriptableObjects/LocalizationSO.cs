// LocalizationSO.cs
// 키 기반 다국어 문자열 데이터. design-decisions.md #52.
// CreateAssetMenu 로 에디터에서 SO 생성 → LocalizationImporter 로 JSON 채우기.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    [Serializable]
    public class LocalizationEntry
    {
        public string key;

        [TextArea(1, 3)]
        public string korean;

        [TextArea(1, 3)]
        public string english;
    }

    [CreateAssetMenu(menuName = "FM-Lite/LocalizationSO", fileName = "LocalizationSO")]
    public class LocalizationSO : ScriptableObject
    {
        public List<LocalizationEntry> entries = new List<LocalizationEntry>();

        private Dictionary<string, LocalizationEntry> _index;

        public void BuildIndex()
        {
            _index = new Dictionary<string, LocalizationEntry>(entries.Count);
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.key))
                    _index[e.key] = e;
        }

        public bool TryGetEntry(string key, out LocalizationEntry entry)
        {
            if (_index == null)
                BuildIndex();
            return _index.TryGetValue(key, out entry);
        }
    }
}
