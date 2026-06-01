// GlobalSavePanel.cs
// Stage W.5 (V1.0) — 어디서든 TopBar [저장] → 모달. 11+ 씬의 자체 savePanel 일원화.
// DashboardController.savePanel 로직 추출 (design-decisions.md #58 / v1.0-plan §3.19.5).
// 슬롯명 사용자 입력 (TMP_InputField) 은 Stage N.1 — V1.0 W.5 는 자동 슬롯명 + 기존 슬롯 덮어쓰기.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class GlobalSavePanel : MonoBehaviour
    {
        [SerializeField]
        private GameObject root; // 모달 루트 (비활성 시작)

        [SerializeField]
        private Transform slotListParent;

        [SerializeField]
        private GameObject slotItemPrefab;

        [SerializeField]
        private TMP_Text noSlotsText;

        [SerializeField]
        private Button newSlotButton;

        [SerializeField]
        private Button closeButton;

        private void Start()
        {
            if (newSlotButton != null)
            {
                newSlotButton.onClick.RemoveAllListeners();
                newSlotButton.onClick.AddListener(OnNewSlotClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
            Hide();
        }

        public void Show()
        {
            if (root != null)
                root.SetActive(true);
            Populate();
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        private void OnNewSlotClicked()
        {
            var state = GameManager.Instance?.State;
            var clubName = state?.GetClub(state.userClubId)?.name ?? "user";
            SaveToSlot(GenerateAutoSlotName(clubName, DateTime.Now));
        }

        private void Populate()
        {
            if (slotListParent == null || slotItemPrefab == null)
                return;

            foreach (Transform child in slotListParent)
                Destroy(child.gameObject);

            var slots = SaveSystem.ListSlots();
            if (noSlotsText != null)
                noSlotsText.gameObject.SetActive(slots.Count == 0);

            foreach (var meta in slots)
            {
                var item = Instantiate(slotItemPrefab, slotListParent);
                item.GetComponent<SaveSlotItem>()?.Setup(meta, SaveToSlot);
            }
        }

        private void SaveToSlot(string slotName)
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;
            SaveSystem.Save(state, slotName);
            GameLog.Log(LogCategory.System, $"슬롯 저장: {slotName}");
            Hide();
        }

        /// <summary>자동 슬롯명: slot_&lt;클럽&gt;_&lt;yyMMdd_HHmm&gt;. 파일명 불가 문자 치환. (테스트 가능)</summary>
        public static string GenerateAutoSlotName(string clubName, DateTime now)
        {
            string safe = SanitizeSlotName(clubName ?? "user");
            return $"slot_{safe}_{now:yyMMdd_HHmm}";
        }

        public static string SanitizeSlotName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
