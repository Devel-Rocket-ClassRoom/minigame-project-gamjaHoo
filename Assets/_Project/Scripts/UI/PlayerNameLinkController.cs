// PlayerNameLinkController.cs
// V1.0 N.1 — 선수 이름 클릭 → PlayerProfileScene.
// Setup(playerId, currentSceneName) 호출 후 버튼 OnClick 연결.

using FMLite.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class PlayerNameLinkController : MonoBehaviour
    {
        internal const string PreviousSceneKey = "PreviousScene";
        private const string PlayerProfileScene = "PlayerProfileScene";

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private Button linkButton;

        private int _playerId;

        public void Setup(int playerId, string currentSceneName)
        {
            _playerId = playerId;

            var state = GameManager.Instance?.State;
            var player = state?.GetPlayer(playerId);
            string displayName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={playerId}";

            if (nameText != null)
                nameText.text = displayName;

            if (linkButton != null)
            {
                linkButton.onClick.RemoveAllListeners();
                linkButton.onClick.AddListener(() => NavigateToProfile(currentSceneName));
            }
        }

        private void NavigateToProfile(string fromScene)
        {
            PlayerPrefs.SetInt(SquadController.SelectedPlayerIdKey, _playerId);
            PlayerPrefs.SetString(PreviousSceneKey, fromScene);
            SceneManager.LoadScene(PlayerProfileScene);
        }
    }
}
