// LineupSlotRowController.cs
// J.5 — LineupScene 의 슬롯 row. Position/Role 라벨(읽기 전용) + 선수 드롭다운.
// TacticSlotRowController 패턴 (LineupController 가 11개 인스턴스화 + Init).

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class LineupSlotRowController : MonoBehaviour
    {
        [Header("Labels (읽기 전용)")]
        [SerializeField]
        private TMP_Text positionLabel;

        [SerializeField]
        private TMP_Text roleLabel;

        [Header("Player (MUIP CustomDropdown)")]
        [SerializeField]
        private CustomDropdown playerDropdown;

        private int _slotIndex;
        private readonly List<int> _playerIds = new List<int>(); // index → playerId (-1 = 미배정)

        public int SlotIndex => _slotIndex;

        public int SelectedPlayerId =>
            playerDropdown != null
            && playerDropdown.selectedItemIndex >= 0
            && playerDropdown.selectedItemIndex < _playerIds.Count
                ? _playerIds[playerDropdown.selectedItemIndex]
                : -1;

        // slotIndex: 0-10
        // pos: 슬롯 포지션 (포지션 라벨 표시용)
        // roleId: 현재 배정 Role (이름 표시용)
        // currentPlayerId: 현재 배정 선수 (-1 = 미배정)
        // squad: 이 포지션과 호환되는 선수 목록 (부상·정지 여부 포함, 이름에 "(부상)"/"(정지)" 표기)
        public void Init(
            int slotIndex,
            Position pos,
            int roleId,
            int currentPlayerId,
            List<(Player player, string displayName)> squad
        )
        {
            _slotIndex = slotIndex;

            if (positionLabel != null)
                positionLabel.text = pos.ToString();

            if (roleLabel != null)
            {
                var role = GameDatabase.GetPlayerRole(roleId);
                roleLabel.text = role != null ? role.displayName : "-";
            }

            BuildPlayerDropdown(currentPlayerId, squad);
            StartCoroutine(NudgeDropdownAfterFrame());
        }

        private void BuildPlayerDropdown(
            int currentPlayerId,
            List<(Player player, string displayName)> squad
        )
        {
            if (playerDropdown == null)
            {
                Debug.LogWarning($"[LineupSlotRowController] slot {_slotIndex}: playerDropdown 미지정");
                return;
            }

            playerDropdown.dropdownItems.Clear();
            _playerIds.Clear();

            // 첫 항목: 미배정
            playerDropdown.CreateNewItemFast(Localization.Get("lineup_unassigned"), null);
            _playerIds.Add(-1);

            int selectedIndex = 0;
            int i = 1;
            foreach (var (player, displayName) in squad)
            {
                playerDropdown.CreateNewItemFast(displayName, null);
                _playerIds.Add(player.id);
                if (player.id == currentPlayerId)
                    selectedIndex = i;
                i++;
            }

            playerDropdown.selectedItemIndex = selectedIndex;
            playerDropdown.SetupDropdown();

            Canvas.ForceUpdateCanvases();
            if (playerDropdown.listRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(playerDropdown.listRect);
        }

        private IEnumerator NudgeDropdownAfterFrame()
        {
            yield return null;
            if (playerDropdown != null)
            {
                // listRect에 Canvas override → ScrollView Mask 위에 렌더링
                if (playerDropdown.listRect != null
                    && playerDropdown.listRect.GetComponent<Canvas>() == null)
                {
                    var c = playerDropdown.listRect.gameObject.AddComponent<Canvas>();
                    c.overrideSorting = true;
                    c.sortingOrder = 10;
                    playerDropdown.listRect.gameObject.AddComponent<GraphicRaycaster>();
                }

                var go = playerDropdown.gameObject;
                go.SetActive(false);
                go.SetActive(true);
                Canvas.ForceUpdateCanvases();
                if (playerDropdown.listRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(playerDropdown.listRect);
            }
        }
    }
}
