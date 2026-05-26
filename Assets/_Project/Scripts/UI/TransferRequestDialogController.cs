// TransferRequestDialogController.cs
// V1.0 G.4 — Happiness < transferRequestThreshold (20) → TransferRequestEvent 발행 → 본 dialog 자동 노출.
// design-decisions.md #42 (Q9 자동 트리거 + 유저 승인 패턴).
//
// 3 버튼:
//   - 수락 → player.state.transferListed = true (V1.0 단순. 가격 할인 등 K.4 후속)
//   - 거절 → dismiss + 로그 (V1.0 단순. happiness 추가 페널티 V1.x)
//   - 면담 → PlayerProfileScene 진입 (유저가 [면담] 버튼 직접 클릭)

using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class TransferRequestDialogController : MonoBehaviour
    {
        private const string PlayerProfileScene = "PlayerProfileScene";

        [Header("Panel")]
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text messageText;

        [Header("3 Buttons")]
        [SerializeField]
        private Button acceptButton;

        [SerializeField]
        private Button rejectButton;

        [SerializeField]
        private Button interviewButton;

        [Header("Close")]
        [SerializeField]
        private Button closeButton;

        private int _playerId = -1;

        private void Awake()
        {
            if (acceptButton != null)
                acceptButton.onClick.AddListener(OnAccept);
            if (rejectButton != null)
                rejectButton.onClick.AddListener(OnReject);
            if (interviewButton != null)
                interviewButton.onClick.AddListener(OnInterview);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            Hide();
        }

        public void Show(int playerId)
        {
            _playerId = playerId;
            var state = GameManager.Instance?.State;
            var player = state?.GetPlayer(playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={playerId}";
            if (messageText != null)
                messageText.text = Localization.Get(
                    "transfer_request_dialog_message_fmt",
                    playerName
                );
            if (panel != null)
                panel.SetActive(true);
        }

        public void Hide()
        {
            _playerId = -1;
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnAccept()
        {
            var state = GameManager.Instance?.State;
            var player = state?.GetPlayer(_playerId);
            if (player?.state != null)
            {
                player.state.transferListed = true;
                GameLog.Log(
                    LogCategory.Transfer,
                    $"이적 요청 수락 — playerId={_playerId} → transferListed"
                );
            }
            Hide();
        }

        private void OnReject()
        {
            GameLog.Log(LogCategory.Transfer, $"이적 요청 거절 — playerId={_playerId}");
            Hide();
        }

        private void OnInterview()
        {
            if (_playerId == -1)
            {
                Hide();
                return;
            }
            // 유저가 PlayerProfile 의 [면담] 버튼 직접 클릭하는 패턴 (V1.0 단순).
            PlayerPrefs.SetInt(SquadController.SelectedPlayerIdKey, _playerId);
            Hide();
            SceneManager.LoadScene(PlayerProfileScene);
        }
    }
}
