// YouthManagementController.cs
// Stage E (V1.0, #461) — 평시 유스 관리 화면. YouthScene(인스펙션/풀)과 분리.
//   · E.2 다음 인스펙션 예고 (6/15 또는 1/15 다음 날짜 + D-N) + 시설 기반 예측 풀/영입수
//   · E.1 현 유스 명단 (club.youthSquadIds)
//   · E.3 1군 콜업 후보 (club.season.pendingPromotionPlayerIds — 18+CA70%, YouthSystem.CheckPromotionCandidates)
//   · E.1 멘토링 요약 (club.season.mentoringGroups)
// 선수 클릭 → PlayerProfileScene (PreviousScene 캐싱, Stage D).

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class YouthManagementController : MonoBehaviour
    {
        private const string PlayerProfileScene = "PlayerProfileScene";
        private const string DashboardScene = "DashboardScene";

        [Header("다음 인스펙션 (E.2)")]
        [SerializeField]
        private TMP_Text nextInspectionText;

        [SerializeField]
        private TMP_Text poolPredictionText;

        [Header("현 유스 (E.1)")]
        [SerializeField]
        private TMP_Text youthCountText;

        [SerializeField]
        private Transform youthListParent;

        [Header("1군 콜업 후보 (E.3)")]
        [SerializeField]
        private Transform callupListParent;

        [SerializeField]
        private TMP_Text callupEmptyText;

        [Header("멘토링 요약 (E.1)")]
        [SerializeField]
        private TMP_Text mentoringSummaryText;

        [Header("공통")]
        [SerializeField]
        private GameObject playerItemPrefab;

        [SerializeField]
        private Button backButton;

        private GameState _state;
        private Club _club;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            _club = GameManager.Instance?.UserClub;
            if (_state == null || _club == null)
                return;

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }

            BuildInspection();
            BuildYouthList();
            BuildCallup();
            BuildMentoring();
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        // ── E.2 다음 인스펙션 예고 + 시설 예측 ────────────────────────────
        private void BuildInspection()
        {
            var b = GameDatabase.GameBalance;
            if (b == null)
                return;

            DateTime today = _state.currentDate.Date;
            DateTime next = NextInspectionDate(
                today,
                b.youthIntakeMainMonth,
                b.youthIntakeMainDay,
                b.youthIntakeSecondMonth,
                b.youthIntakeSecondDay
            );
            int dDay = (next - today).Days;

            if (nextInspectionText != null)
                nextInspectionText.text = Localization.Get(
                    "youth_next_inspection_fmt",
                    next.ToString("yyyy-MM-dd"),
                    dDay
                );

            if (poolPredictionText != null)
            {
                var recruit = GameDatabase.GetFacilityLevel(
                    FacilityType.YouthRecruitment,
                    _club.facilities.youthRecruitmentLevel
                );
                int pool = recruit?.youthPoolSize ?? 0;
                int maxSign =
                    recruit != null
                        ? Math.Max(1, (int)Math.Round(pool * (double)recruit.signRatio))
                        : 0;
                poolPredictionText.text = Localization.Get(
                    "youth_pool_prediction_fmt",
                    pool,
                    maxSign,
                    _club.facilities.youthRecruitmentLevel
                );
            }
        }

        // 오늘 이후 가장 가까운 인스펙션 날짜 (두 후보 중 최소 >= today).
        private static DateTime NextInspectionDate(DateTime today, int m1, int d1, int m2, int d2)
        {
            DateTime best = DateTime.MaxValue;
            foreach (var (mm, dd) in new[] { (m1, d1), (m2, d2) })
            {
                for (int yearOffset = 0; yearOffset <= 1; yearOffset++)
                {
                    var cand = SafeDate(today.Year + yearOffset, mm, dd);
                    if (cand >= today && cand < best)
                        best = cand;
                }
            }
            return best == DateTime.MaxValue ? today : best;
        }

        private static DateTime SafeDate(int year, int month, int day)
        {
            if (month < 1 || month > 12)
                return DateTime.MaxValue;
            int dim = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, Math.Min(day, dim));
        }

        // ── E.1 현 유스 명단 ──────────────────────────────────────────────
        private void BuildYouthList()
        {
            if (youthCountText != null)
                youthCountText.text = Localization.Get(
                    "youth_count_fmt",
                    _club.youthSquadIds?.Count ?? 0
                );

            PopulateList(youthListParent, _club.youthSquadIds);
        }

        // ── E.3 1군 콜업 후보 ─────────────────────────────────────────────
        private void BuildCallup()
        {
            var pending = _club.season?.pendingPromotionPlayerIds;
            int count = pending?.Count ?? 0;
            if (callupEmptyText != null)
                callupEmptyText.gameObject.SetActive(count == 0);
            PopulateList(callupListParent, pending);
        }

        // ── E.1 멘토링 요약 ───────────────────────────────────────────────
        private void BuildMentoring()
        {
            if (mentoringSummaryText == null)
                return;
            var groups = _club.season?.mentoringGroups;
            int groupCount = groups?.Count ?? 0;
            int menteeCount = 0;
            if (groups != null)
                foreach (var g in groups)
                    menteeCount += g.menteePlayerIds?.Count ?? 0;
            mentoringSummaryText.text = Localization.Get(
                "youth_mentoring_summary_fmt",
                groupCount,
                menteeCount
            );
        }

        // ── 공통 리스트 채우기 ────────────────────────────────────────────
        private void PopulateList(Transform parent, System.Collections.Generic.IList<int> ids)
        {
            if (parent == null)
                return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);

            if (ids == null || playerItemPrefab == null)
                return;
            foreach (var id in ids)
            {
                var player = _state.GetPlayer(id);
                if (player == null)
                    continue;
                var item = Instantiate(playerItemPrefab, parent);
                item.GetComponent<PlayerListItem>()?.Setup(player, _state, OnPlayerSelected);
            }
        }

        private void OnPlayerSelected(int playerId)
        {
            PlayerPrefs.SetInt(SquadController.SelectedPlayerIdKey, playerId);
            PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
            SceneManager.LoadScene(PlayerProfileScene);
        }
    }
}
