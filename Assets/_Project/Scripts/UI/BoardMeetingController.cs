// BoardMeetingController.cs
// V1.0 M.5 — 보드 약속 수락/거절 모달 (DashboardScene 내 패널).
// DashboardController.Start 에서 PendingReview 약속 존재 시 Show 호출.

using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class BoardMeetingController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private GameBalanceSO balance;

        private BoardPromise _current;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Show(BoardPromise promise, GameState state)
        {
            _current = promise;
            if (descriptionText != null)
                descriptionText.text = LocalizationSystem.Get(
                    "board_promise_transfer_in_desc",
                    promise.targetPosition.ToString()
                );
            gameObject.SetActive(true);
        }

        public void OnAcceptClicked()
        {
            if (_current == null)
                return;
            _current.status = BoardPromiseStatus.Active;
            gameObject.SetActive(false);
            _current = null;
        }

        public void OnRejectClicked()
        {
            if (_current == null)
                return;
            var state = GameManager.Instance?.State;
            if (state != null && balance != null)
                BoardSystem.RejectPromise(state, balance, _current);
            gameObject.SetActive(false);
            _current = null;
        }
    }
}
