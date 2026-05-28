// LineupController.cs
// J.5 — LineupScene: 11명 수동 배정 + 자동 라인업 + Set Pieces 4종 담당자.
// TacticController 패턴 (MUIP CustomDropdown × N + ButtonManager × 2).
// design-decisions.md #45 / #57.

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
    public class LineupController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        // setPieceTakers 인덱스 상수 (MatchSimulator.FindSetPieceTaker 와 동기화)
        public const int SetPiecePenalty = 0;
        public const int SetPieceFreeKick = 1;
        public const int SetPieceCorner = 2;
        public const int SetPieceThrowIn = 3;
        private const int SetPieceCount = 4;

        [Header("Header")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("Slot Rows (11개)")]
        [SerializeField]
        private Transform slotRowContainer;

        [SerializeField]
        private LineupSlotRowController slotRowPrefab;

        [Header("자동 라인업 버튼")]
        [SerializeField]
        private ButtonManager autoLineupButton;

        [Header("Set Pieces — 인덱스 0=PK / 1=FK / 2=CK / 3=ThrowIn")]
        [SerializeField]
        private TMP_Text penaltyLabel;

        [SerializeField]
        private TMP_Text freeKickLabel;

        [SerializeField]
        private TMP_Text cornerLabel;

        [SerializeField]
        private TMP_Text throwInLabel;

        [SerializeField]
        private CustomDropdown penaltyDropdown;

        [SerializeField]
        private CustomDropdown freeKickDropdown;

        [SerializeField]
        private CustomDropdown cornerDropdown;

        [SerializeField]
        private CustomDropdown throwInDropdown;

        [Header("Buttons")]
        [SerializeField]
        private ButtonManager saveButton;

        [SerializeField]
        private ButtonManager backButton;

        private Club _userClub;
        private FormationSO _currentFormation;
        private readonly List<LineupSlotRowController> _slotRows =
            new List<LineupSlotRowController>();

        // setPiece 드롭다운별 playerIds (index → playerId, 0 = -1 미배정)
        private readonly List<int>[] _setPiecePlayerIds = new List<int>[SetPieceCount];

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
            AutoLineup.EnsureSetPieceTakers(_userClub.tactic, SetPieceCount);

            if (titleText != null)
                titleText.text = Localization.Get("lineup_title");

            _currentFormation = GameDatabase.GetFormation(_userClub.tactic.formationId);

            BuildSlotRows();
            BuildSetPieceDropdowns(state);
            WireButtons();

            StartCoroutine(NudgeAfterFrame());
        }

        // ── Slot Rows ─────────────────────────────────────────────────────────────

        private void BuildSlotRows()
        {
            if (slotRowContainer == null || slotRowPrefab == null)
            {
                Debug.LogWarning("[LineupController] slotRowContainer 또는 slotRowPrefab 미지정");
                return;
            }

            foreach (var row in _slotRows)
                if (row != null)
                    Destroy(row.gameObject);
            _slotRows.Clear();

            if (_currentFormation?.slotPositions == null)
            {
                Debug.LogWarning("[LineupController] formation 미발견 또는 slotPositions null");
                return;
            }

            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            // 스쿼드 전체 (부상·정지 표기 포함)
            var squadWithNames = BuildSquadList(state, _userClub);

            int count = System.Math.Min(_currentFormation.slotPositions.Length, 11);
            for (int i = 0; i < count; i++)
            {
                var pos = _currentFormation.slotPositions[i];
                var slot = GetOrCreateSlot(i);

                // 이 포지션과 호환되는 선수 목록
                var compatible = squadWithNames
                    .Where(x => AutoLineup.IsPositionCompatible(x.player, pos))
                    .OrderByDescending(x => AutoLineup.AdjustedCA(x.player))
                    .ToList();

                var row = Instantiate(slotRowPrefab, slotRowContainer);
                row.Init(i, pos, slot.roleId, slot.assignedPlayerId, compatible);
                _slotRows.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            if (slotRowContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        // ── Set Pieces ────────────────────────────────────────────────────────────

        private static readonly string[] SetPieceLabelKeys = new string[SetPieceCount]
        {
            "lineup_penalty_taker",
            "lineup_free_kick_taker",
            "lineup_corner_taker",
            "lineup_throw_in_taker",
        };

        private CustomDropdown[] SetPieceDropdowns =>
            new[] { penaltyDropdown, freeKickDropdown, cornerDropdown, throwInDropdown };

        private TMP_Text[] SetPieceLabels =>
            new[] { penaltyLabel, freeKickLabel, cornerLabel, throwInLabel };

        private void BuildSetPieceDropdowns(GameState state)
        {
            var squad = state.allPlayers
                .Where(p => p.currentClubId == _userClub.id)
                .OrderByDescending(AutoLineup.AdjustedCA)
                .ToList();

            var drops = SetPieceDropdowns;
            var labelTexts = SetPieceLabels;
            for (int t = 0; t < SetPieceCount; t++)
            {
                if (labelTexts[t] != null)
                    labelTexts[t].text = Localization.Get(SetPieceLabelKeys[t]);

                _setPiecePlayerIds[t] = new List<int>();
                var dd = drops[t];
                if (dd == null)
                    continue;

                dd.dropdownItems.Clear();
                _setPiecePlayerIds[t].Clear();

                // 첫 항목: 자동 (stat 최상위 폴백)
                dd.CreateNewItemFast(Localization.Get("lineup_auto_taker"), null);
                _setPiecePlayerIds[t].Add(-1);

                int selectedIndex = 0;
                int i = 1;
                int currentTaker = _userClub.tactic.setPieceTakers[t];
                foreach (var p in squad)
                {
                    dd.CreateNewItemFast(PlayerDisplayName(p, state), null);
                    _setPiecePlayerIds[t].Add(p.id);
                    if (p.id == currentTaker)
                        selectedIndex = i;
                    i++;
                }

                dd.selectedItemIndex = selectedIndex;
                dd.SetupDropdown();

                Canvas.ForceUpdateCanvases();
                if (dd.listRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(dd.listRect);
            }
        }

        // ── Buttons ───────────────────────────────────────────────────────────────

        private void WireButtons()
        {
            if (autoLineupButton != null)
            {
                autoLineupButton.buttonText = Localization.Get("lineup_auto_assign");
                autoLineupButton.UpdateUI();
                autoLineupButton.clickEvent.AddListener(OnAutoLineupClicked);
            }
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

        public void OnAutoLineupClicked()
        {
            var state = GameManager.Instance?.State;
            if (state == null || _userClub == null)
                return;
            AutoLineup.AssignBest(_userClub, state);
            BuildSlotRows(); // UI 갱신
        }

        public void OnSaveClicked()
        {
            if (_userClub?.tactic == null)
            {
                SceneManager.LoadScene(DashboardScene);
                return;
            }

            // 슬롯 저장 — 중복 선수 배정 방지 (낮은 슬롯 인덱스 우선)
            var assignedIds = new HashSet<int>();
            foreach (var row in _slotRows)
            {
                if (row == null)
                    continue;
                var slot = GetOrCreateSlot(row.SlotIndex);
                int pid = row.SelectedPlayerId;
                if (pid >= 0 && !assignedIds.Add(pid))
                    pid = -1; // 중복 → 미배정
                slot.assignedPlayerId = pid;
            }

            // Set Pieces 저장
            AutoLineup.EnsureSetPieceTakers(_userClub.tactic, SetPieceCount);
            var drops = SetPieceDropdowns;
            for (int t = 0; t < SetPieceCount; t++)
            {
                var dd = drops[t];
                var ids = _setPiecePlayerIds[t];
                if (dd == null || ids == null || ids.Count == 0)
                    continue;
                int idx = dd.selectedItemIndex;
                _userClub.tactic.setPieceTakers[t] =
                    idx >= 0 && idx < ids.Count ? ids[idx] : -1;
            }

            SceneManager.LoadScene(DashboardScene);
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        // ── 헬퍼 ──────────────────────────────────────────────────────────────────

        private TacticSlot GetOrCreateSlot(int index)
        {
            if (_userClub.tactic.slots == null)
                _userClub.tactic.slots = new List<TacticSlot>();
            while (_userClub.tactic.slots.Count <= index)
                _userClub.tactic.slots.Add(
                    new TacticSlot
                    {
                        slotIndex = _userClub.tactic.slots.Count,
                        roleId = 0,
                        duty = Duty.Support,
                        assignedPlayerId = -1,
                    }
                );
            return _userClub.tactic.slots[index];
        }

        // 스쿼드 선수 목록 + 표시 이름 빌드 (부상/정지 표기).
        private static List<(Player player, string displayName)> BuildSquadList(
            GameState state,
            Club club
        )
        {
            return club
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .Where(p => p != null)
                .Select(p => (player: p, displayName: PlayerDisplayName(p, state)))
                .OrderByDescending(x => AutoLineup.AdjustedCA(x.player))
                .ToList();
        }

        private static string PlayerDisplayName(Player p, GameState state)
        {
            var name =
                p.info != null
                    ? $"{p.info.lastName} {p.info.firstName}"
                    : $"#{p.id}";

            if (!AutoLineup.IsAvailable(p))
            {
                if (p.state.injury?.injuryTypeId != -1)
                    name += $" ({Localization.Get("lineup_injured")})";
                else if (p.state.suspendedMatches > 0)
                    name += $" ({Localization.Get("lineup_suspended")})";
            }
            return name;
        }

        // Set Piece 드롭다운 listRect에 Canvas override → 다른 UI 위에 렌더링 + MUIP 초기화.
        private System.Collections.IEnumerator NudgeAfterFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var dd in SetPieceDropdowns)
            {
                if (dd == null)
                    continue;
                if (
                    dd.listRect != null
                    && dd.listRect.GetComponent<Canvas>() == null
                )
                {
                    var c = dd.listRect.gameObject.AddComponent<Canvas>();
                    c.overrideSorting = true;
                    c.sortingOrder = 10;
                    dd.listRect.gameObject.AddComponent<GraphicRaycaster>();
                }
                var go = dd.gameObject;
                go.SetActive(false);
                go.SetActive(true);
            }
            Canvas.ForceUpdateCanvases();
        }
    }
}
