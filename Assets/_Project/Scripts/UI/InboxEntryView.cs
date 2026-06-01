// InboxEntryView.cs
// Stage B.1 (V1.0) — 인박스 행 뷰. InboxPanelController 가 행 prefab 으로 인스턴스화.
//   · 타이틀 = InboxTextResolver.ResolveTitle (ID→이름 state 해석)
//   · 안읽음 배경 강조 (#3A3A4E) / 읽음 (#2A2A3E)
//   · 카테고리 색 스트라이프 (좌측)
//   · 기한 표시 (D-N / 만료) — deadline 없으면 숨김
//   · 클릭 → 콜백 (컨트롤러가 isRead + InboxAction 라우팅 담당)
//
// 주의: FMLite.UI.InboxItem (구 V0.5 row MonoBehaviour) 충돌 회피 — 도메인은 alias(D) 로 참조.
//       색상은 표시 전용 (게임 룰 아님 → GameBalanceSO 외부화 대상 아님). muip-reference §18 팔레트.

using System;
using FMLite.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using D = FMLite.Domain;

namespace FMLite.UI
{
    public class InboxEntryView : MonoBehaviour
    {
        [SerializeField]
        private Image background;

        [SerializeField]
        private Image categoryStripe;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text deadlineText;

        [SerializeField]
        private Button rowButton;

        // 표시 색 (muip-reference §18)
        private static readonly Color UnreadBg = Hex(0x3A3A4E);
        private static readonly Color ReadBg = Hex(0x2A2A3E);
        private static readonly Color DeadlineUrgent = Hex(0xE87040); // 경고 (≤2일)
        private static readonly Color DeadlineNormal = Hex(0xCCCCCC); // 보조 텍스트
        private static readonly Color InactiveText = Hex(0x777777); // 만료 비활성

        public void Setup(D.InboxItem item, D.GameState state, Action<D.InboxItem> onClick)
        {
            if (item == null)
                return;

            // Q1 (design-decisions #66): 기한 만료해도 자동처리 X / 사라지지 않음 — 비활성 표시만.
            bool expired = IsExpired(item, state);

            if (titleText != null)
            {
                titleText.text = InboxTextResolver.ResolveTitle(item, state);
                titleText.color = expired ? InactiveText : Color.white;
            }

            if (background != null)
                background.color = item.isRead ? ReadBg : UnreadBg;

            if (categoryStripe != null)
            {
                var c = CategoryColor(item.category);
                if (expired)
                    c.a = 0.35f; // 만료 = 스트라이프 흐리게
                categoryStripe.color = c;
            }

            RefreshDeadline(item, state, expired);

            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                // 만료 = 비활성 (클릭해도 동작 X — 기회 소멸)
                rowButton.interactable = !expired;
                if (!expired)
                    rowButton.onClick.AddListener(() => onClick?.Invoke(item));
            }
        }

        /// <summary>기한 도래(만료) 여부. deadline 없거나 state 없으면 false.</summary>
        public static bool IsExpired(D.InboxItem item, D.GameState state) =>
            item != null
            && state != null
            && item.deadline.HasValue
            && item.deadline.Value.Date < state.currentDate.Date;

        private void RefreshDeadline(D.InboxItem item, D.GameState state, bool expired)
        {
            if (deadlineText == null)
                return;

            if (item.deadline == null || state == null)
            {
                deadlineText.gameObject.SetActive(false);
                return;
            }

            deadlineText.gameObject.SetActive(true);
            if (expired)
            {
                deadlineText.text = Localization.Get("inbox_deadline_expired");
                deadlineText.color = InactiveText;
                return;
            }

            int days = (item.deadline.Value.Date - state.currentDate.Date).Days;
            deadlineText.text = Localization.Get("inbox_deadline_days_fmt", days);
            deadlineText.color = days <= 2 ? DeadlineUrgent : DeadlineNormal;
        }

        public static Color CategoryColor(D.InboxCategory cat) =>
            cat switch
            {
                D.InboxCategory.Match => Hex(0x4A90D9),
                D.InboxCategory.Transfer => Hex(0x4CAF50),
                D.InboxCategory.Morale => Hex(0xE87040),
                D.InboxCategory.Board => Hex(0x9B59B6),
                D.InboxCategory.Youth => Hex(0x2ECC71),
                D.InboxCategory.Cup => Hex(0xFFD93D),
                D.InboxCategory.Award => Hex(0xF39C12),
                _ => Hex(0xCCCCCC),
            };

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
