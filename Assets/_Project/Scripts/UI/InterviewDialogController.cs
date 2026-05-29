// InterviewDialogController.cs
// V0.5 G.2 Sub-B — PlayerProfile [면담] 버튼 → 4 멘트 dialog.
// design-decisions.md #43 (V0.5 단순 4-6 멘트) — 본 PR 4 멘트만, 5-6 옵션은 V1.0.
//
// 버튼 → MoraleSystem.OnInterview(state, playerId, type, balance):
//   - Praise           → Morale +5
//   - Criticize        → Morale -3 (professionalism 보정)
//   - PromisePlaytime  → PlaytimeAgreement Promise 생성 (G.2 Sub-A wire)
//   - PromiseRenewal   → Renewal Promise 생성 (G.2 Sub-A wire)
//
// 사용:
//   InterviewDialogController.Show(playerId)
//   - 패널 활성화 + _playerId 저장
//   - 버튼 클릭 → 핸들러 → Hide()

using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class InterviewDialogController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField]
        private GameObject panel;

        [Header("4 멘트 Buttons")]
        [SerializeField]
        private Button praiseButton;

        [SerializeField]
        private Button criticizeButton;

        [SerializeField]
        private Button promisePlaytimeButton;

        [SerializeField]
        private Button promiseRenewalButton;

        [Header("Close")]
        [SerializeField]
        private Button closeButton;

        [Header("Data")]
        [SerializeField]
        private GameBalanceSO balance;

        private int _playerId = -1;

        private void Awake()
        {
            if (praiseButton != null)
                praiseButton.onClick.AddListener(() => OnInterviewClicked(InterviewType.Praise));
            if (criticizeButton != null)
                criticizeButton.onClick.AddListener(() =>
                    OnInterviewClicked(InterviewType.Criticize)
                );
            if (promisePlaytimeButton != null)
                promisePlaytimeButton.onClick.AddListener(() =>
                    OnInterviewClicked(InterviewType.PromisePlaytime)
                );
            if (promiseRenewalButton != null)
                promiseRenewalButton.onClick.AddListener(() =>
                    OnInterviewClicked(InterviewType.PromiseRenewal)
                );
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            Hide();
        }

        public void Show(int playerId)
        {
            _playerId = playerId;
            if (panel != null)
                panel.SetActive(true);
        }

        public void Hide()
        {
            _playerId = -1;
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnInterviewClicked(InterviewType type)
        {
            if (_playerId == -1)
            {
                Hide();
                return;
            }
            var state = GameManager.Instance?.State;
            if (state == null || balance == null)
            {
                Hide();
                return;
            }

            MoraleSystem.OnInterview(state, _playerId, type, balance);
            GameLog.Log(LogCategory.System, $"면담 — playerId={_playerId} / type={type}");
            Hide();
        }
    }
}
