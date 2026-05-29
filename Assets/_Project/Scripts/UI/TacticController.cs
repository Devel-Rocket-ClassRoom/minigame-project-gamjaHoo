// TacticController.cs
// J.5 슬라이스 — TacticScene: Formation 선택 + Mentality 선택 + Save/Back.
// 11 슬롯 Role/Duty 편집 + LineupScene 은 후속 (v0.5-tasks.md J.5).
// MUIP 첫 도입 화면 (design-decisions.md #45 / #57).

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class TacticController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("Header")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("Formation (MUIP CustomDropdown)")]
        [SerializeField]
        private CustomDropdown formationDropdown;

        [Header("Mentality (MUIP HorizontalSelector)")]
        [SerializeField]
        private HorizontalSelector mentalitySelector;

        [Header("Buttons (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager saveButton;

        [SerializeField]
        private ButtonManager backButton;

        [Header("Slots (11 rows)")]
        [SerializeField]
        private Transform slotRowContainer;

        [SerializeField]
        private TacticSlotRowController slotRowPrefab;

        // Mentality enum 7단계 Localization 키 (LocalizationSO 에 추가 필요 — 미추가 시 키 그대로 표시).
        private static readonly string[] MentalityKeys =
        {
            "mentality_very_defensive",
            "mentality_defensive",
            "mentality_cautious",
            "mentality_balanced",
            "mentality_positive",
            "mentality_attacking",
            "mentality_very_attacking",
        };

        private const int SlotCount = 11;

        private Club _userClub;
        private FormationSO _currentFormation;
        private readonly List<int> _formationIds = new List<int>();
        private readonly List<TacticSlotRowController> _slotRows = new List<TacticSlotRowController>();

        private void Start()
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;
            _userClub = GameManager.Instance.UserClub;
            if (_userClub == null)
                return;
            if (_userClub.tactic == null)
                _userClub.tactic = new Tactic();
            EnsureSlots(SlotCount);

            if (titleText != null)
                titleText.text = Localization.Get("tactic_title");

            BuildFormationDropdown();
            BuildMentalitySelector();
            BuildSlotRows();
            WireButtons();

            // Formation 변경 시 slot row 재빌드 (새 포메이션의 포지션에 맞춰 Role 후보 갱신).
            if (formationDropdown != null)
                formationDropdown.dropdownEvent.AddListener(OnFormationChanged);

            // 다음 프레임에 ScrollRect / Mask viewport 차원을 재계산하도록 강제 — 동적 Instantiate 직후
            // 같은 프레임에 ScrollRect.OnRectTransformDimensionsChange 가 stale 한 채 굳어 첫 Play 시
            // 리스트가 안 보이는 케이스 대응 (윈도우 리사이즈 한 번이 같은 효과를 내는 이유).
            StartCoroutine(NudgeDropdownScrollRectAfterFrame());
        }

        private void OnDisable()
        {
            if (formationDropdown != null)
                formationDropdown.dropdownEvent.RemoveListener(OnFormationChanged);
        }

        private void EnsureSlots(int count)
        {
            if (_userClub.tactic.slots == null)
                _userClub.tactic.slots = new List<TacticSlot>();
            while (_userClub.tactic.slots.Count < count)
                _userClub.tactic.slots.Add(
                    new TacticSlot
                    {
                        slotIndex = _userClub.tactic.slots.Count,
                        roleId = 0,
                        duty = Duty.Support,
                        assignedPlayerId = -1,
                    }
                );
        }

        private void OnFormationChanged(int dropdownIndex)
        {
            BuildSlotRows();
        }

        private void BuildSlotRows()
        {
            if (slotRowContainer == null || slotRowPrefab == null)
            {
                Debug.LogWarning("[TacticController] slotRowContainer 또는 slotRowPrefab 미지정");
                return;
            }

            // 현재 dropdown 선택값으로부터 formation 결정 (변경 직후 reflect).
            int formationId =
                _formationIds.Count > 0
                && formationDropdown.selectedItemIndex >= 0
                && formationDropdown.selectedItemIndex < _formationIds.Count
                    ? _formationIds[formationDropdown.selectedItemIndex]
                    : _userClub.tactic.formationId;
            _currentFormation = GameDatabase.GetFormation(formationId);

            // 기존 row 정리 (재진입 안전).
            foreach (var row in _slotRows)
                if (row != null)
                    Destroy(row.gameObject);
            _slotRows.Clear();

            if (_currentFormation == null || _currentFormation.slotPositions == null)
            {
                Debug.LogWarning("[TacticController] formation 미발견 또는 slotPositions null");
                return;
            }

            int count = Mathf.Min(_currentFormation.slotPositions.Length, SlotCount);
            for (int i = 0; i < count; i++)
            {
                var row = Instantiate(slotRowPrefab, slotRowContainer);

                // 위쪽 row 일수록 sortingOrder 높게 → RoleDropdown 펼침이 아래 row 위에 그려짐 (#390).
                // row 안 (RoleDropdown 자식) 에 Override Sorting=true Canvas 가 있어야 작동.
                var slotCanvas = row.GetComponentInChildren<Canvas>(includeInactive: true);
                if (slotCanvas != null)
                    slotCanvas.sortingOrder = count - i;

                var slot = _userClub.tactic.slots[i];
                var pos = _currentFormation.slotPositions[i];

                // 현재 slot.roleId 가 새 포지션과 호환 안 되면 첫 호환 role 로 기본값.
                int initialRoleId = slot.roleId;
                var role = GameDatabase.GetPlayerRole(initialRoleId);
                if (
                    role == null
                    || role.compatiblePositions == null
                    || !role.compatiblePositions.Contains(pos)
                )
                {
                    var firstCompatible = GameDatabase
                        .AllPlayerRoles.Where(r =>
                            r.compatiblePositions != null && r.compatiblePositions.Contains(pos)
                        )
                        .OrderBy(r => r.id)
                        .FirstOrDefault();
                    initialRoleId = firstCompatible != null ? firstCompatible.id : 0;
                }

                row.Init(i, pos, initialRoleId, slot.duty);
                _slotRows.Add(row);
            }

            // FacilityController 패턴 — 동적 Instantiate 후 layout dirty 강제 갱신.
            Canvas.ForceUpdateCanvases();
            if (slotRowContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private System.Collections.IEnumerator NudgeDropdownScrollRectAfterFrame()
        {
            yield return null; // 한 프레임 대기 — 모든 Awake/OnEnable/Start 체인 settle
            if (formationDropdown != null)
            {
                // 핵 옵션: GameObject 비활성→재활성 으로 모든 컴포넌트의 OnDisable/OnEnable 재발화.
                // CustomDropdown.OnEnable 이 closeOn, listRect.sizeDelta, listCG 를 settled 한 Canvas
                // 컨텍스트에서 다시 계산 → 윈도우 리사이즈와 동일 효과.
                var go = formationDropdown.gameObject;
                go.SetActive(false);
                go.SetActive(true);

                Canvas.ForceUpdateCanvases();
                if (formationDropdown.listRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(formationDropdown.listRect);
            }
        }

        private void BuildFormationDropdown()
        {
            if (formationDropdown == null)
            {
                Debug.LogWarning("[TacticController] formationDropdown 미지정");
                return;
            }

            formationDropdown.dropdownItems.Clear();
            _formationIds.Clear();

            int selectedIndex = 0;
            int i = 0;
            foreach (var f in GameDatabase.AllFormations.OrderBy(f => f.id))
            {
                formationDropdown.CreateNewItemFast(f.displayName, null);
                _formationIds.Add(f.id);
                if (f.id == _userClub.tactic.formationId)
                    selectedIndex = i;
                i++;
            }

            formationDropdown.selectedItemIndex = _formationIds.Count > 0 ? selectedIndex : 0;
            formationDropdown.SetupDropdown();

            // SetupDropdown 이 itemParent 에 동적 Instantiate 한 직후 같은 프레임에 layout dirty 가
            // 안 풀려, 첫 Play 시 리스트가 펼쳐지지 않는 케이스 대응 (FacilityController 동일 패턴).
            Canvas.ForceUpdateCanvases();
            if (formationDropdown.listRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(formationDropdown.listRect);
        }

        private void BuildMentalitySelector()
        {
            if (mentalitySelector == null)
            {
                Debug.LogWarning("[TacticController] mentalitySelector 미지정");
                return;
            }

            mentalitySelector.itemList.Clear();
            for (int m = 0; m < MentalityKeys.Length; m++)
                mentalitySelector.CreateNewItem(Localization.Get(MentalityKeys[m]));

            mentalitySelector.defaultIndex = Mathf.Clamp(
                (int)_userClub.tactic.mentality,
                0,
                MentalityKeys.Length - 1
            );
            mentalitySelector.SetupSelector();
        }

        private void WireButtons()
        {
            if (saveButton != null)
            {
                saveButton.buttonText = Localization.Get("tactic_save");
                saveButton.UpdateUI();
                saveButton.clickEvent.AddListener(OnSaveClicked);
            }
            if (backButton != null)
            {
                backButton.buttonText = Localization.Get("tactic_back");
                backButton.UpdateUI();
                backButton.clickEvent.AddListener(OnBackClicked);
            }
        }

        // Save — 현재 MUIP 선택값들을 club.tactic 에 반영 후 Dashboard 복귀 (deferred apply).
        public void OnSaveClicked()
        {
            if (_userClub?.tactic != null)
            {
                if (
                    formationDropdown != null
                    && formationDropdown.selectedItemIndex >= 0
                    && formationDropdown.selectedItemIndex < _formationIds.Count
                )
                {
                    _userClub.tactic.formationId = _formationIds[
                        formationDropdown.selectedItemIndex
                    ];
                }
                if (mentalitySelector != null)
                {
                    int idx = Mathf.Clamp(mentalitySelector.index, 0, MentalityKeys.Length - 1);
                    _userClub.tactic.mentality = (Mentality)idx;
                }

                // 11 슬롯 row → tactic.slots[i].roleId / .duty 반영.
                EnsureSlots(_slotRows.Count);
                foreach (var row in _slotRows)
                {
                    if (row == null)
                        continue;
                    int si = row.SlotIndex;
                    if (si < 0 || si >= _userClub.tactic.slots.Count)
                        continue;
                    var slot = _userClub.tactic.slots[si];
                    int newRoleId = row.SelectedRoleId;
                    if (newRoleId > 0)
                        slot.roleId = newRoleId;
                    slot.duty = row.SelectedDuty;
                }
            }
            SceneManager.LoadScene(DashboardScene);
        }

        // Back — 변경 폐기 후 Dashboard 복귀.
        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);
    }
}
