// DebugBootstrap.cs
// V0.1 Stage 13 (UI) 진입 전 임시 부트스트랩 — PlayMode 진입 시 자동으로 NewGame.
// Stage 13 정식 메인 메뉴 도입 후 폐기 예정.
//
// 사용법:
//   1. Hierarchy 에 빈 GameObject 추가 (예: "Bootstrap")
//   2. 이 컴포넌트 첨부 — GameManager 가 RequireComponent 로 자동 추가됨
//   3. Inspector 에서 seed / seasonStart / userClubId 조정 (기본값 그대로도 OK)
//   4. PlayMode 진입 → 자동으로 GameDatabase.LoadAll + NewGame + GameManager.SetState

using System;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;
using UnityEngine;

namespace FMLite.Application
{
    [RequireComponent(typeof(GameManager))]
    public class DebugBootstrap : MonoBehaviour
    {
        [Header("New Game Settings")]
        public int seed = 42;
        public int seasonStartYear = 2025;
        public int seasonStartMonth = 7;
        public int seasonStartDay = 1; // 7/1 프리시즌 시작 (decisions #38 보강)

        [Tooltip("PlayMode 진입 시 자동으로 설정할 유저 구단 ID. 0 이면 미선택 (-1 유지).")]
        public int userClubId = 1;

        private void Awake()
        {
            // GameDatabase는 항상 로드 — NamePool 등 런타임 조회에 필요
            GameDatabase.LoadAll();
            // 언어 선호 적용 (#463) — 시스템 언어가 아닌 저장된 OptionsManager.Language 가 source of truth.
            // (DebugBootstrap 은 매 씬 Awake 마다 실행되므로 여기서 시스템 언어로 덮어쓰면 옵션 선택이 무효화됨)
            OptionsManager.EnsureInitialized();
            LocalizationSystem.Initialize(GameDatabase.LocalizationData, OptionsManager.Language);

            // 이미 다른 씬에서 GameManager + State 가 초기화된 경우 스킵
            // (MainMenu → ClubSelect → Gacha → Dashboard 실제 플로우 보호)
            if (GameManager.Instance != null && GameManager.Instance.State != null)
            {
                Debug.Log("[DebugBootstrap] 기존 State 감지 — 초기화 스킵");
                return;
            }

            var balance = GameDatabase.GameBalance;
            var leagueConfig = Resources.LoadAll<LeagueConfigSO>(string.Empty).FirstOrDefault();
            if (balance == null)
            {
                Debug.LogError("[DebugBootstrap] GameBalance.asset Resources 로드 실패");
                return;
            }
            if (leagueConfig == null)
            {
                Debug.LogError("[DebugBootstrap] LeagueConfigSO Resources 로드 실패");
                return;
            }

            // NewGame
            var seasonStart = new DateTime(seasonStartYear, seasonStartMonth, seasonStartDay);
            var state = GameInitializer.NewGame(seed, seasonStart, leagueConfig, balance);
            if (userClubId > 0)
                state.userClubId = userClubId;

            // GameManager 주입
            var gm = GetComponent<GameManager>();
            gm.SetState(state); // SetState 내부에서 GameTime.Reset 호출

            Debug.Log(
                $"[DebugBootstrap] NewGame OK — seed={seed} / start={seasonStart:yyyy-MM-dd} / "
                    + $"userClub={state.userClubId} ({state.GetClub(state.userClubId)?.name ?? "-"}) / "
                    + $"clubs={state.allClubs.Count} / players={state.allPlayers.Count}"
            );
        }
    }
}
