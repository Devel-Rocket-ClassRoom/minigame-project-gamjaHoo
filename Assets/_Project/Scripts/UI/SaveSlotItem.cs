// 로드 게임 슬롯 목록 아이템 프리팹 컨트롤러.
// Unity AI Assistant 가 프리팹 구성 + 인스펙터 연결.

using System;
using FMLite.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class SaveSlotItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text clubNameText;

        [SerializeField]
        private TMP_Text gameDateText;

        [SerializeField]
        private TMP_Text savedAtText;

        [SerializeField]
        private Button loadButton;

        private Action<string> onLoad;

        public void Setup(SaveSlotMeta meta, Action<string> loadCallback)
        {
            if (clubNameText != null)
                clubNameText.text = string.IsNullOrEmpty(meta.userClubName) ? "미선택" : meta.userClubName;
            if (gameDateText != null)
                gameDateText.text = meta.currentDate.ToString("yyyy-MM-dd");
            if (savedAtText != null)
                savedAtText.text = meta.savedAt.ToString("MM/dd HH:mm");

            onLoad = loadCallback;
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => onLoad(meta.slotName));
        }
    }
}
