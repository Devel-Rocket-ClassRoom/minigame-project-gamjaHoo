// BenchDropZone.cs
// Stage H.2 — Squad 목록 영역 = 드롭 존. 슬롯에서 끌어다 놓으면 미배정(벤치 복귀).

using UnityEngine;
using UnityEngine.EventSystems;

namespace FMLite.UI
{
    public class BenchDropZone : MonoBehaviour, IDropHandler
    {
        private LineupTabController _ctrl;

        public void Init(LineupTabController ctrl) => _ctrl = ctrl;

        public void OnDrop(PointerEventData e) => _ctrl?.OnDropOnBench(e.pointerDrag);
    }
}
