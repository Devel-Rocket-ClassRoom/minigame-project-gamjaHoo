// TacticLineupController.cs
// Stage H.1 — TacticLineupScene 통합 (전술 / 라인업 / 세트피스 3탭). TacticScene + LineupScene 폐기 대체.
// 기존 씬 문제(드롭다운 클리핑 등) 회피 재구성 — 포메이션/Mentality = HorizontalSelector, Role/Duty = 슬롯 팝업(H.3).
// 전술 탭: 포메이션 + Mentality + 자동 라인업(LineupSelector). 라인업 탭(피치+드래그)=H.2 / 세트피스=후속.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class TacticLineupController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("Tabs (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager tacticTab;

        [SerializeField]
        private ButtonManager lineupTab;

        [SerializeField]
        private ButtonManager setPiecesTab;

        [Header("Tab Panels")]
        [SerializeField]
        private GameObject tacticPanel;

        [SerializeField]
        private GameObject lineupPanel;

        [SerializeField]
        private GameObject setPiecesPanel;

        [Header("Tactic Tab (HorizontalSelector — 드롭다운 클리핑 회피)")]
        [SerializeField]
        private HorizontalSelector formationSelector;

        [SerializeField]
        private HorizontalSelector mentalitySelector;

        [SerializeField]
        private ButtonManager autoLineupButton;

        [SerializeField]
        private TMP_Text statusText;

        [Header("Nav")]
        [SerializeField]
        private ButtonManager saveButton;

        [SerializeField]
        private ButtonManager backButton;

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

        private Club _userClub;
        private readonly List<int> _formationIds = new List<int>();

        private void Start()
        {
            WireTab(tacticTab, "전술", 0);
            WireTab(lineupTab, "라인업", 1);
            WireTab(setPiecesTab, "세트피스", 2);
            WireButton(backButton, "뒤로", OnBackClicked);
            WireButton(saveButton, "저장", OnSaveClicked);
            WireButton(autoLineupButton, "자동 라인업", OnAutoLineupClicked);

            _userClub = GameManager.Instance?.UserClub;
            if (_userClub != null && _userClub.tactic == null)
                _userClub.tactic = new Tactic();
            // 리스트는 GameDatabase / 정적 데이터만 있으면 채울 수 있음 — 선택 인덱스만 클럽에 의존.
            BuildFormationSelector();
            BuildMentalitySelector();
            ShowTab(0);
        }

        // ── 탭 전환 ───────────────────────────────────────────────────────
        private void WireTab(ButtonManager tab, string label, int index)
        {
            if (tab == null)
                return;
            tab.buttonText = label;
            tab.UpdateUI();
            tab.clickEvent.AddListener(() => ShowTab(index));
        }

        private void WireButton(ButtonManager b, string label, UnityEngine.Events.UnityAction cb)
        {
            if (b == null)
                return;
            b.buttonText = label;
            b.UpdateUI();
            b.clickEvent.AddListener(cb);
        }

        public void ShowTab(int index)
        {
            // 라인업 탭 진입 전 전술(포메이션) 커밋 + 슬롯 보장 → 패널 활성 시 LineupTabController.OnEnable 이 Refresh.
            if (index == 1)
                PrepareLineupTab();
            if (tacticPanel != null)
                tacticPanel.SetActive(index == 0);
            if (lineupPanel != null)
                lineupPanel.SetActive(index == 1);
            if (setPiecesPanel != null)
                setPiecesPanel.SetActive(index == 2);
            SetTabCurrent(tacticTab, index == 0);
            SetTabCurrent(lineupTab, index == 1);
            SetTabCurrent(setPiecesTab, index == 2);
        }

        // 현재 탭 표시 + MUIP 하이라이트 잔상 제거.
        // MUIP ButtonManager 는 interactable==false 일 때 OnPointerExit FadeOut 이 막혀
        // Highlighted CanvasGroup 가 alpha 1(흰색)로 고착됨. 매 전환마다 CG 를 직접 리셋해 해결.
        private static void SetTabCurrent(ButtonManager tab, bool isCurrent)
        {
            if (tab == null)
                return;
            tab.StopAllCoroutines(); // 진행 중인 FadeIn/FadeOut 중단
            var normalTr = tab.transform.Find("Normal");
            var highlightedTr = tab.transform.Find("Highlighted");
            if (normalTr != null)
                normalTr.GetComponent<CanvasGroup>().alpha = isCurrent ? 0f : 1f;
            if (highlightedTr != null)
                highlightedTr.GetComponent<CanvasGroup>().alpha = isCurrent ? 1f : 0f;
            var btn = tab.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
                btn.interactable = !isCurrent;
        }

        // ── 전술 탭 (포메이션 / Mentality) ─────────────────────────────────
        private void BuildFormationSelector()
        {
            if (formationSelector == null)
                return;
            formationSelector.itemList.Clear();
            _formationIds.Clear();
            int selected = 0,
                i = 0;
            foreach (var f in GameDatabase.AllFormations.OrderBy(f => f.id))
            {
                formationSelector.CreateNewItem(f.displayName);
                _formationIds.Add(f.id);
                if (_userClub?.tactic != null && f.id == _userClub.tactic.formationId)
                    selected = i;
                i++;
            }
            formationSelector.defaultIndex = _formationIds.Count > 0 ? selected : 0;
            formationSelector.SetupSelector();
        }

        private void BuildMentalitySelector()
        {
            if (mentalitySelector == null)
                return;
            mentalitySelector.itemList.Clear();
            for (int m = 0; m < MentalityKeys.Length; m++)
                mentalitySelector.CreateNewItem(Localization.Get(MentalityKeys[m]));
            int mentalityIndex = _userClub?.tactic != null ? (int)_userClub.tactic.mentality : 3; // 3 = 균형
            mentalitySelector.defaultIndex = Mathf.Clamp(mentalityIndex, 0, MentalityKeys.Length - 1);
            mentalitySelector.SetupSelector();
        }

        private void ApplyTacticFromUI()
        {
            if (_userClub?.tactic == null)
                return;
            if (
                formationSelector != null
                && formationSelector.index >= 0
                && formationSelector.index < _formationIds.Count
            )
            {
                int newFormationId = _formationIds[formationSelector.index];
                if (newFormationId != _userClub.tactic.formationId)
                {
                    _userClub.tactic.formationId = newFormationId;
                    // 포메이션 변경 → 슬롯 포지션 재정렬 (배정/role 은 라인업 탭에서 재설정)
                    RebuildSlotsForFormation(newFormationId);
                }
            }
            if (mentalitySelector != null)
                _userClub.tactic.mentality = (Mentality)
                    Mathf.Clamp(mentalitySelector.index, 0, MentalityKeys.Length - 1);
        }

        // 포메이션 변경 시 11 슬롯을 새 포메이션 포지션 기본 role/duty 로 재구성 (배정은 자동 라인업/드래그로).
        private void RebuildSlotsForFormation(int formationId)
        {
            var formation = GameDatabase.GetFormation(formationId);
            if (formation?.slotPositions == null)
                return;
            var slots = new List<TacticSlot>(11);
            for (int i = 0; i < formation.slotPositions.Length && i < 11; i++)
            {
                var pos = formation.slotPositions[i];
                var role = GameDatabase
                    .AllPlayerRoles.Where(r => r.compatiblePositions?.Contains(pos) == true)
                    .OrderBy(r => r.id)
                    .FirstOrDefault();
                slots.Add(
                    new TacticSlot
                    {
                        slotIndex = i,
                        roleId = role?.id ?? 0,
                        duty = role?.defaultDuty ?? Duty.Support,
                        assignedPlayerId = -1,
                    }
                );
            }
            _userClub.tactic.slots = slots;
        }

        // 라인업 탭 준비 — 포메이션/Mentality 커밋 + 슬롯 미생성/불일치 시 재구성.
        private void PrepareLineupTab()
        {
            ApplyTacticFromUI();
            if (_userClub?.tactic == null)
                return;
            var formation = GameDatabase.GetFormation(_userClub.tactic.formationId);
            int need = formation?.slotPositions?.Length ?? 0;
            var slots = _userClub.tactic.slots;
            if (need > 0 && (slots == null || slots.Count != need))
                RebuildSlotsForFormation(_userClub.tactic.formationId);
        }

        private void OnAutoLineupClicked()
        {
            ApplyTacticFromUI();
            if (_userClub == null)
                return;
            var state = GameManager.Instance.State;
            int seed = (state?.randomSeed ?? 0) ^ _userClub.id ^ 0x5EED;
            var xi = LineupSelector.SelectStartingEleven(
                _userClub,
                state,
                seed,
                GameDatabase.GameBalance
            );
            // 자동 결과를 슬롯에 배정 (순서대로).
            if (_userClub.tactic.slots != null)
                for (int i = 0; i < _userClub.tactic.slots.Count && i < xi.Count; i++)
                    _userClub.tactic.slots[i].assignedPlayerId = xi[i];
            SetStatus($"자동 라인업 배정 완료 ({xi.Count}명)");
        }

        private void SetStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }

        private void OnSaveClicked()
        {
            ApplyTacticFromUI();
            SetStatus("저장됨");
            SceneManager.LoadScene(DashboardScene);
        }

        private void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        public void ShowTactic() => ShowTab(0);

        public void ShowLineup() => ShowTab(1);

        public void ShowSetPieces() => ShowTab(2);
    }
}
