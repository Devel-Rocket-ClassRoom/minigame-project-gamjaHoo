// GameManager.cs
// 게임 전역 진입점. 씬 어디서나 Instance 로 접근.
// State 는 GameInitializer / SaveSystem 가 SetState 로 주입.
// 위치: Core Layer — 인프라/컨테이너 성격 (design-decisions.md #29 참조).

using FMLite.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.Core
{
    public class GameManager : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenuScene";
        public const string SackedKey = "gameSacked";

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }

        public Club UserClub => State?.GetClub(State.userClubId);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EventBus.Subscribe<ManagerSackedEvent>(OnManagerSacked);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ManagerSackedEvent>(OnManagerSacked);
            if (Instance == this)
                Instance = null;
        }

        public void SetState(GameState state)
        {
            State = state;
            if (state != null)
                GameTime.Reset(state.currentDate);
        }

        private void OnManagerSacked(ManagerSackedEvent e)
        {
            PlayerPrefs.SetInt(SackedKey, 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene(MainMenuScene);
        }
    }
}
