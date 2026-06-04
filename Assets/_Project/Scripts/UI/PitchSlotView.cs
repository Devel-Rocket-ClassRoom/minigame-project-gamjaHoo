// PitchSlotView.cs
// Stage H.2 — 피치 도식의 슬롯 점. 드롭 타깃(선수 배정) + 드래그 소스(슬롯↔슬롯 스왑).
// 빈 슬롯은 드래그 불가. 배정 선수명/CA/포지션 라벨 + 포지션 적합 색 표시.

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class PitchSlotView
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler,
            IPointerClickHandler
    {
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text posText;

        [SerializeField]
        private Image background;

        public int SlotIndex { get; private set; }
        public int AssignedPlayerId { get; private set; } = -1;

        private LineupTabController _ctrl;
        private CanvasGroup _cg;

        public void Init(LineupTabController ctrl, int slotIndex)
        {
            _ctrl = ctrl;
            SlotIndex = slotIndex;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null)
                _cg = gameObject.AddComponent<CanvasGroup>();
        }

        // playerId < 0 = 빈 슬롯.
        public void SetContent(
            string slotPosLabel,
            int playerId,
            string playerName,
            int ca,
            Color fitColor
        )
        {
            AssignedPlayerId = playerId;
            if (posText != null)
                posText.text = slotPosLabel;
            if (nameText != null)
                nameText.text = playerId >= 0 ? $"{playerName}\n{ca}" : "—";
            if (background != null)
                background.color = fitColor;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (AssignedPlayerId < 0 || _ctrl == null)
                return; // 빈 슬롯은 드래그 안 함
            _ctrl.BeginDrag(nameText != null ? nameText.text : "");
            if (_cg != null)
                _cg.blocksRaycasts = false; // 아래 드롭 타깃이 레이캐스트 받도록
        }

        public void OnDrag(PointerEventData e)
        {
            if (AssignedPlayerId >= 0)
                _ctrl?.MoveGhost(e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _ctrl?.EndDrag();
            if (_cg != null)
                _cg.blocksRaycasts = true;
        }

        public void OnDrop(PointerEventData e) => _ctrl?.OnDropOnSlot(SlotIndex, e.pointerDrag);

        // 클릭(드래그 아님) → Role/Duty 팝업. 드래그가 일어났으면 OnPointerClick 미발화.
        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging)
                return;
            _ctrl?.OnSlotClicked(SlotIndex);
        }
    }
}
