// ShotPinTooltip.cs
// Stage AA 슛맵 — 슛 핀 호버 시 슈터(+골은 어시스트) 툴팁 표시.
// MatchResultController.BuildShotmap 이 핀마다 부착하고 tip(자식 패널)을 연결.

using UnityEngine;
using UnityEngine.EventSystems;

namespace FMLite.UI
{
    public class ShotPinTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private GameObject tip;

        public void Init(GameObject tipObject) => tip = tipObject;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tip == null)
                return;
            transform.SetAsLastSibling(); // 다른 핀 위로 — 툴팁 가림 방지
            tip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tip != null)
                tip.SetActive(false);
        }
    }
}
