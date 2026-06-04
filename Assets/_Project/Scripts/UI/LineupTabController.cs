// LineupTabController.cs
// Stage H.2 — TacticLineupScene 라인업 탭. 포메이션 피치 도식 + 드래그&드롭 선수 배정.
// 좌: 피치 슬롯 점(FormationPitchLayout 좌표) / 우: Squad 목록(전체, 배정자 흐리게).
// 배정 결과는 Tactic.slots[i].assignedPlayerId 에 기록. Q5 = Swap 정책.
// 패널 활성(탭 전환) 시 OnEnable → Refresh. TacticLineupController 가 슬롯 보장 후 활성화.

using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class LineupTabController : MonoBehaviour
    {
        [Header("Pitch")]
        [SerializeField]
        private RectTransform pitchContainer;

        [SerializeField]
        private PitchSlotView slotPrefab;

        [Header("Squad list")]
        [SerializeField]
        private RectTransform squadContent;

        [SerializeField]
        private SquadDragItem squadItemPrefab;

        [SerializeField]
        private BenchDropZone benchDropZone;

        [Header("Drag ghost")]
        [SerializeField]
        private RectTransform dragGhost;

        [SerializeField]
        private TMP_Text dragGhostText;

        [Header("Role/Duty popup (H.3)")]
        [SerializeField]
        private RoleDutyPopup rolePopup;

        // 포지션 적합 색.
        private static readonly Color FitPrimary = new Color32(46, 204, 113, 255); // 초록
        private static readonly Color FitSecondary = new Color32(241, 196, 15, 255); // 노랑
        private static readonly Color FitOut = new Color32(231, 76, 60, 255); // 빨강
        private static readonly Color EmptySlot = new Color32(58, 58, 78, 255); // 빈 슬롯 회색

        private readonly List<PitchSlotView> _slotViews = new List<PitchSlotView>();
        private readonly List<SquadDragItem> _squadItems = new List<SquadDragItem>();

        private Club _club;
        private GameState _state;

        private void OnEnable()
        {
            if (benchDropZone != null)
                benchDropZone.Init(this);
            if (dragGhost != null)
                dragGhost.gameObject.SetActive(false);
            Refresh();
        }

        public void Refresh()
        {
            _club = GameManager.Instance?.UserClub;
            _state = GameManager.Instance?.State;
            if (_club?.tactic == null || _state == null)
                return;

            var formation = GameDatabase.GetFormation(_club.tactic.formationId);
            var coords = FormationPitchLayout.Compute(formation);
            var slotPos = formation?.slotPositions;
            var slots = _club.tactic.slots;

            BuildPitch(coords, slotPos, slots);
            BuildSquadList(slots);
        }

        // ── 피치 슬롯 ─────────────────────────────────────────────────────
        private void BuildPitch(Vector2[] coords, Position[] slotPos, List<TacticSlot> slots)
        {
            EnsurePool(_slotViews, slotPrefab, pitchContainer, coords.Length, i => { });
            for (int i = 0; i < _slotViews.Count; i++)
            {
                var view = _slotViews[i];
                bool active = i < coords.Length;
                view.gameObject.SetActive(active);
                if (!active)
                    continue;

                view.Init(this, i);
                var rt = (RectTransform)view.transform;
                rt.anchorMin = rt.anchorMax = coords[i];
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                var pos = slotPos != null && i < slotPos.Length ? slotPos[i] : Position.GK;
                int pid = slots != null && i < slots.Count ? slots[i].assignedPlayerId : -1;
                var player = pid >= 0 ? _state.GetPlayer(pid) : null;
                string label = player?.info?.lastName ?? string.Empty;
                int ca = player?.currentAbility ?? 0;
                // 포지션 + Duty 약어 (Role/Duty 변경 시 슬롯 갱신 가시화, H.3)
                string posLabel =
                    slots != null && i < slots.Count
                        ? $"{pos} · {DutyShort(slots[i].duty)}"
                        : pos.ToString();
                view.SetContent(
                    posLabel,
                    player != null ? pid : -1,
                    label,
                    ca,
                    player != null ? FitColor(player, pos) : EmptySlot
                );
            }
        }

        // 슬롯 클릭 → Role/Duty 팝업 (H.3). 빈 슬롯도 역할/임무 설정 가능.
        public void OnSlotClicked(int slotIndex)
        {
            var slots = _club?.tactic?.slots;
            if (rolePopup == null || slots == null || slotIndex < 0 || slotIndex >= slots.Count)
                return;
            var formation = GameDatabase.GetFormation(_club.tactic.formationId);
            var pos =
                formation?.slotPositions != null && slotIndex < formation.slotPositions.Length
                    ? formation.slotPositions[slotIndex]
                    : Position.GK;
            rolePopup.Open(slots[slotIndex], pos, Refresh);
        }

        private static string DutyShort(Duty d)
        {
            switch (d)
            {
                case Duty.Attack:
                    return "공";
                case Duty.Defend:
                    return "수";
                default:
                    return "지";
            }
        }

        private static Color FitColor(Player p, Position slot)
        {
            var primary = p.info.primaryPosition;
            if (primary == slot)
                return FitPrimary;
            if (p.info.secondaryPositions != null && p.info.secondaryPositions.Contains(slot))
                return FitSecondary;
            if (
                FMLite.Application.StartingSquadGacha.LineOf(primary)
                == FMLite.Application.StartingSquadGacha.LineOf(slot)
            )
                return FitSecondary;
            return FitOut;
        }

        // ── Squad 목록 ────────────────────────────────────────────────────
        private void BuildSquadList(List<TacticSlot> slots)
        {
            var assigned = new HashSet<int>();
            if (slots != null)
                foreach (var s in slots)
                    if (s.assignedPlayerId >= 0)
                        assigned.Add(s.assignedPlayerId);

            var players = _club
                .seniorSquadIds.Select(_state.GetPlayer)
                .Where(p => p != null)
                .OrderBy(p =>
                    (int)FMLite.Application.StartingSquadGacha.LineOf(p.info.primaryPosition)
                )
                .ThenByDescending(p => p.currentAbility)
                .ToList();

            EnsurePool(_squadItems, squadItemPrefab, squadContent, players.Count, i => { });
            for (int i = 0; i < _squadItems.Count; i++)
            {
                var item = _squadItems[i];
                bool active = i < players.Count;
                item.gameObject.SetActive(active);
                if (!active)
                    continue;

                var p = players[i];
                bool isAssigned = assigned.Contains(p.id);
                bool available = IsAvailable(p);
                string text = $"{p.info.primaryPosition, -3} {p.info.lastName}  {p.currentAbility}";
                item.Init(this, p.id, text, draggable: !isAssigned && available);
            }
        }

        private static bool IsAvailable(Player p) =>
            p != null && p.state?.injury?.injuryTypeId == -1 && p.state.suspendedMatches <= 0;

        // ── 드롭 처리 (Q5 = Swap) ─────────────────────────────────────────
        public void OnDropOnSlot(int targetSlot, GameObject pointerDrag)
        {
            if (pointerDrag == null || _club?.tactic?.slots == null)
                return;
            var slots = _club.tactic.slots;
            if (targetSlot < 0 || targetSlot >= slots.Count)
                return;

            var sq = pointerDrag.GetComponent<SquadDragItem>();
            if (sq != null)
            {
                if (!sq.Draggable)
                    return; // 이미 배정됐거나 비가용(부상/정지) 선수는 드롭 무시
                slots[targetSlot].assignedPlayerId = sq.PlayerId; // 점유 시 밀린 선수는 벤치 복귀
                Refresh();
                return;
            }
            var src = pointerDrag.GetComponent<PitchSlotView>();
            if (src != null && src.SlotIndex != targetSlot && src.SlotIndex < slots.Count)
            {
                int tmp = slots[targetSlot].assignedPlayerId;
                slots[targetSlot].assignedPlayerId = slots[src.SlotIndex].assignedPlayerId;
                slots[src.SlotIndex].assignedPlayerId = tmp;
                Refresh();
            }
        }

        public void OnDropOnBench(GameObject pointerDrag)
        {
            if (pointerDrag == null || _club?.tactic?.slots == null)
                return;
            var src = pointerDrag.GetComponent<PitchSlotView>();
            if (src != null && src.SlotIndex < _club.tactic.slots.Count)
            {
                _club.tactic.slots[src.SlotIndex].assignedPlayerId = -1;
                Refresh();
            }
        }

        // ── 드래그 고스트 ─────────────────────────────────────────────────
        public void BeginDrag(string label)
        {
            if (dragGhost == null)
                return;
            if (dragGhostText != null)
                dragGhostText.text = label;
            dragGhost.gameObject.SetActive(true);
        }

        public void MoveGhost(Vector2 screenPos)
        {
            if (dragGhost != null)
                dragGhost.position = screenPos; // Screen Space Overlay → screen == world
        }

        public void EndDrag()
        {
            if (dragGhost != null)
                dragGhost.gameObject.SetActive(false);
        }

        // ── 풀링 헬퍼 ─────────────────────────────────────────────────────
        private static void EnsurePool<T>(
            List<T> pool,
            T prefab,
            RectTransform parent,
            int needed,
            System.Action<int> onCreate
        )
            where T : Component
        {
            if (prefab == null || parent == null)
                return;
            while (pool.Count < needed)
            {
                var inst = Object.Instantiate(prefab, parent);
                pool.Add(inst);
                onCreate(pool.Count - 1);
            }
        }
    }
}
