// TacticSlotRowController.cs
// J.5 — TacticScene 의 11 슬롯 row. 각 row: Position 라벨 + Role 드롭다운 (포지션 호환 필터) + Duty 셀렉터.
// FacilityRowController 패턴 (TacticController 가 11개 인스턴스화 + Init).

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
    public class TacticSlotRowController : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField]
        private TMP_Text positionLabel;

        [Header("Role (MUIP CustomDropdown)")]
        [SerializeField]
        private CustomDropdown roleDropdown;

        [Header("Duty (MUIP HorizontalSelector)")]
        [SerializeField]
        private HorizontalSelector dutySelector;

        // Duty enum 3 단계 Localization 키 (LocalizationSO 에 추가 필요).
        private static readonly string[] DutyKeys =
        {
            "duty_attack",
            "duty_support",
            "duty_defend",
        };

        private int _slotIndex;
        private Position _position;
        private readonly List<int> _roleIds = new List<int>();

        public int SlotIndex => _slotIndex;

        public int SelectedRoleId =>
            roleDropdown != null
            && roleDropdown.selectedItemIndex >= 0
            && roleDropdown.selectedItemIndex < _roleIds.Count
                ? _roleIds[roleDropdown.selectedItemIndex]
                : -1;

        public Duty SelectedDuty =>
            dutySelector != null
                ? (Duty)Mathf.Clamp(dutySelector.index, 0, DutyKeys.Length - 1)
                : Duty.Support;

        public void Init(int slotIndex, Position position, int currentRoleId, Duty currentDuty)
        {
            _slotIndex = slotIndex;
            _position = position;

            if (positionLabel != null)
                positionLabel.text = position.ToString();

            BuildRoleDropdown(currentRoleId);
            BuildDutySelector(currentDuty);

            // TacticController 와 동일 stale-layout 문제 대응 (윈도우 리사이즈 한 번이 같은 효과 내는 케이스).
            StartCoroutine(NudgeRoleDropdownAfterFrame());
        }

        private void BuildRoleDropdown(int currentRoleId)
        {
            if (roleDropdown == null)
            {
                Debug.LogWarning(
                    $"[TacticSlotRowController] slot {_slotIndex}: roleDropdown 미지정"
                );
                return;
            }

            roleDropdown.dropdownItems.Clear();
            _roleIds.Clear();

            int selectedIndex = 0;
            int i = 0;
            foreach (
                var role in GameDatabase
                    .AllPlayerRoles.Where(r =>
                        r.compatiblePositions != null && r.compatiblePositions.Contains(_position)
                    )
                    .OrderBy(r => r.id)
            )
            {
                roleDropdown.CreateNewItemFast(role.displayName, null);
                _roleIds.Add(role.id);
                if (role.id == currentRoleId)
                    selectedIndex = i;
                i++;
            }

            roleDropdown.selectedItemIndex = _roleIds.Count > 0 ? selectedIndex : 0;
            roleDropdown.SetupDropdown();
        }

        private void BuildDutySelector(Duty currentDuty)
        {
            if (dutySelector == null)
            {
                Debug.LogWarning(
                    $"[TacticSlotRowController] slot {_slotIndex}: dutySelector 미지정"
                );
                return;
            }

            dutySelector.itemList.Clear();
            for (int i = 0; i < DutyKeys.Length; i++)
                dutySelector.CreateNewItem(Localization.Get(DutyKeys[i]));

            dutySelector.defaultIndex = Mathf.Clamp((int)currentDuty, 0, DutyKeys.Length - 1);
            dutySelector.SetupSelector();
        }

        // TacticController 의 dropdown 와 동일 stale-layout 문제 → GameObject 토글로 OnEnable 재발화.
        private IEnumerator NudgeRoleDropdownAfterFrame()
        {
            yield return null;
            if (roleDropdown != null)
            {
                var go = roleDropdown.gameObject;
                go.SetActive(false);
                go.SetActive(true);
                Canvas.ForceUpdateCanvases();
                if (roleDropdown.listRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(roleDropdown.listRect);
            }
        }
    }
}
