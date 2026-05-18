// GameManager.cs
// 게임 전역 상태 진입점. 씬 어디서나 Instance 로 접근.
// V0.1: 싱글톤 + DontDestroyOnLoad 만. State 프로퍼티는 Task 3.3 이후 추가.

using UnityEngine;

namespace FMLite.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
