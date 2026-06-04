// SquadDragItem.cs
// Stage H.2 — 우측 Squad 목록 행. 미배정 + 가용(부상/정지 아님) 선수만 드래그 가능.
// 배정됐거나 가용 아닌 선수는 흐리게 표시 + 드래그 비활성.

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FMLite.UI
{
    public class SquadDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private TMP_Text label;

        public int PlayerId { get; private set; }
        public bool Draggable => _draggable;

        private bool _draggable;
        private LineupTabController _ctrl;
        private CanvasGroup _cg;

        public void Init(LineupTabController ctrl, int playerId, string text, bool draggable)
        {
            _ctrl = ctrl;
            PlayerId = playerId;
            _draggable = draggable;
            if (label != null)
            {
                label.text = text;
                label.alpha = draggable ? 1f : 0.4f;
            }
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null)
                _cg = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!_draggable || _ctrl == null)
                return;
            _ctrl.BeginDrag(label != null ? label.text : "");
            if (_cg != null)
                _cg.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (_draggable)
                _ctrl?.MoveGhost(e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _ctrl?.EndDrag();
            if (_cg != null)
                _cg.blocksRaycasts = true;
        }
    }
}
