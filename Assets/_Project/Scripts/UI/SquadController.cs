// Task 13.5 (Issue #50) — 스쿼드 화면.
// 유저 구단 1군 / 유스 명단 탭 표시. 선수 클릭 → PlayerProfileScene.
// 선택 선수 ID는 PlayerPrefs("SelectedPlayerId")로 씬 간 전달.
// J.6: 캡틴/부캡틴 임명 — 선수 선택 패널 내 버튼으로 호출.
// Stage D (V1.0, #459): 검색/필터 제거 (스쿼드 내 검색 불필요 — 사용자 결정). 성장 동향 시각화 집중.

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class SquadController : MonoBehaviour
    {
        internal const string SelectedPlayerIdKey = "SelectedPlayerId";
        private const string PlayerProfileScene = "PlayerProfileScene";
        private const string DashboardScene = "DashboardScene";

        [Header("탭 패널")]
        [SerializeField]
        private GameObject seniorPanel;

        [SerializeField]
        private GameObject youthPanel;

        [Header("선수 목록")]
        [SerializeField]
        private Transform seniorListParent;

        [SerializeField]
        private Transform youthListParent;

        [SerializeField]
        private GameObject playerItemPrefab;

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        private GameState _state;
        private Club _club;

        private void Start()
        {
            _state = GameManager.Instance.State;
            _club = GameManager.Instance.UserClub;
            if (_state == null || _club == null)
                return;

            if (titleText != null)
                titleText.text = _club.name;

            RefreshLists();
            ShowSenior();
        }

        public void OnSeniorTabClicked() => ShowSenior();

        public void OnYouthTabClicked() => ShowYouth();

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void ShowSenior()
        {
            seniorPanel.SetActive(true);
            youthPanel.SetActive(false);
        }

        private void ShowYouth()
        {
            seniorPanel.SetActive(false);
            youthPanel.SetActive(true);
        }

        private void RefreshLists()
        {
            if (_state == null || _club == null)
                return;
            PopulateList(seniorListParent, _club.seniorSquadIds, _state);
            PopulateList(youthListParent, _club.youthSquadIds, _state);
        }

        private void PopulateList(Transform parent, List<int> ids, GameState state)
        {
            foreach (Transform child in parent)
                Destroy(child.gameObject);

            foreach (var id in ids)
            {
                var player = state.GetPlayer(id);
                if (player == null)
                    continue;

                var item = Instantiate(playerItemPrefab, parent);
                item.GetComponent<PlayerListItem>().Setup(player, state, OnPlayerSelected);
            }
        }

        private void OnPlayerSelected(int playerId)
        {
            PlayerPrefs.SetInt(SelectedPlayerIdKey, playerId);
            // Stage D (#459): 프로필 뒤로가기 캐싱 — 어디서 들어왔는지 기록 (스쿼드 복귀).
            PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
            SceneManager.LoadScene(PlayerProfileScene);
        }

        // ── J.6 캡틴/부캡틴 임명 ─────────────────────────────────────────────────
        // Unity AI 가 Squad 씬에서 선수 행 컨텍스트 버튼에 연결.

        public void OnAssignCaptainClicked(int playerId)
        {
            if (_club == null)
                return;
            CaptainSystem.Assign(_club, playerId, isVice: false);
            if (_state != null)
                PopulateList(seniorListParent, _club.seniorSquadIds, _state);
        }

        public void OnAssignViceCaptainClicked(int playerId)
        {
            if (_club == null)
                return;
            CaptainSystem.Assign(_club, playerId, isVice: true);
            if (_state != null)
                PopulateList(seniorListParent, _club.seniorSquadIds, _state);
        }

        // 현재 캡틴/부캡틴 ID 노출 — PlayerListItem 이 (C)/(VC) 배지 표시에 사용.
        public int CaptainId => GameManager.Instance?.UserClub?.season?.captainPlayerId ?? -1;
        public int ViceCaptainId =>
            GameManager.Instance?.UserClub?.season?.viceCaptainPlayerId ?? -1;
    }
}
