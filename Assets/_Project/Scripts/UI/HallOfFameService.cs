// HallOfFameService.cs
// 명예의 전당 — 게임 이벤트와 클라우드를 잇는 통합 서비스 (MonoBehaviour).
// SeasonEndedEvent 구독 → 커리어 점수 계산 → careers + leaderboard fan-out 업로드.
//
// 배치: GameManager 오브젝트(DontDestroyOnLoad)에 컴포넌트로 부착되는 영속 서비스.
// 레이어: UI/통합 — Application(CareerScore) + Persistence(리포지토리) + Core 를 조율.

using System;
using System.Threading.Tasks;
using FMLite.Application;
using FMLite.Core;
using FMLite.Persistence.Cloud;
using UnityEngine;

namespace FMLite.UI
{
    public class HallOfFameService : MonoBehaviour
    {
        public static HallOfFameService Instance { get; private set; }

        private Action<SeasonEndedEvent> _onSeasonEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _onSeasonEnded = _ => UploadFireAndForget();
            EventBus.Subscribe(_onSeasonEnded);
        }

        private void OnDestroy()
        {
            if (_onSeasonEnded != null)
                EventBus.Unsubscribe(_onSeasonEnded);
            if (Instance == this)
                Instance = null;
        }

        private async void UploadFireAndForget()
        {
            try
            {
                await UploadCurrentCareerAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoF] 시즌 종료 업로드 실패: {ex.Message}");
            }
        }

        // 현재 GameState 기준 커리어를 클라우드에 업로드. 디버그/수동 호출도 이 메서드 사용.
        public async Task UploadCurrentCareerAsync()
        {
            var fb = FirebaseBootstrap.Instance;
            if (fb == null || !fb.IsReady)
            {
                Debug.LogWarning("[HoF] Firebase 미준비 — 업로드 건너뜀.");
                return;
            }

            var state = GameManager.Instance?.State;
            var club = state?.GetClub(state.userClubId);
            if (club == null)
            {
                Debug.LogWarning("[HoF] 유저 구단 없음 — 업로드 건너뜀.");
                return;
            }

            int total = CareerScore.ComputeTotal(state);

            SeasonOutcome latest = CareerScore.GetLatestSeason(state);
            if (latest != null)
            {
                await CareerRepository.RecordSeasonAsync(
                    latest.clubName,
                    latest.year,
                    latest.position,
                    latest.points,
                    total
                );
            }

            // 닉네임: 저장된 프로필 닉네임 우선, 없으면 구단명.
            UserProfile profile = await CloudProfileRepository.GetProfileAsync();
            string nickname = !string.IsNullOrEmpty(profile?.nickname)
                ? profile.nickname
                : club.name;

            await LeaderboardRepository.SubmitEntryAsync(nickname, club.name, total);

            Debug.Log($"[HoF] 업로드 완료: {nickname} / {club.name} — score={total}");
        }
    }
}
