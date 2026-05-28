// Task 13.5 (Issue #50) — 스쿼드 화면.
// 유저 구단 1군 / 유스 명단 탭 표시. 선수 클릭 → PlayerProfileScene.
// 선택 선수 ID는 PlayerPrefs("SelectedPlayerId")로 씬 간 전달.
// J.6: 캡틴/부캡틴 임명 — 선수 선택 패널 내 버튼으로 호출.
// N.6: 이름/포지션 검색 필터 추가.

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

        [Header("검색 필터 (N.6)")]
        [SerializeField]
        private TMP_InputField searchNameInput;

        [SerializeField]
        private TMP_Dropdown searchPositionDropdown;

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

            InitPositionDropdown();
            RefreshLists();
            ShowSenior();
        }

        public void OnSeniorTabClicked() => ShowSenior();

        public void OnYouthTabClicked() => ShowYouth();

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        public void OnSearchClicked() => RefreshLists();

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

        private void InitPositionDropdown()
        {
            if (searchPositionDropdown == null)
                return;
            searchPositionDropdown.ClearOptions();
            var options = new List<string> { Localization.Get("filter_all") };
            foreach (Position pos in System.Enum.GetValues(typeof(Position)))
                options.Add(pos.ToString());
            searchPositionDropdown.AddOptions(options);
        }

        private void RefreshLists()
        {
            if (_state == null || _club == null)
                return;
            var filter = BuildSquadFilter();
            PopulateList(seniorListParent, _club.seniorSquadIds, _state, filter);
            PopulateList(youthListParent, _club.youthSquadIds, _state, filter);
        }

        private TransferSearchFilter BuildSquadFilter()
        {
            var filter = new TransferSearchFilter
            {
                excludeUserClub = false,
                onlyClubId = _state.userClubId,
            };

            var name = searchNameInput?.text?.Trim();
            if (!string.IsNullOrEmpty(name))
                filter.nameContains = name;

            if (searchPositionDropdown != null && searchPositionDropdown.value > 0)
                filter.position = (Position)(searchPositionDropdown.value - 1);

            return filter;
        }

        private void PopulateList(
            Transform parent,
            List<int> ids,
            GameState state,
            TransferSearchFilter filter = null
        )
        {
            foreach (Transform child in parent)
                Destroy(child.gameObject);

            foreach (var id in ids)
            {
                var player = state.GetPlayer(id);
                if (player == null)
                    continue;

                if (filter != null)
                {
                    if (
                        filter.position.HasValue
                        && player.info.primaryPosition != filter.position.Value
                    )
                        continue;
                    if (
                        !string.IsNullOrEmpty(filter.nameContains)
                        && (player.info.firstName + " " + player.info.lastName).IndexOf(
                            filter.nameContains,
                            System.StringComparison.OrdinalIgnoreCase
                        ) < 0
                    )
                        continue;
                }

                var item = Instantiate(playerItemPrefab, parent);
                item.GetComponent<PlayerListItem>().Setup(player, state, OnPlayerSelected);
            }
        }

        private void OnPlayerSelected(int playerId)
        {
            PlayerPrefs.SetInt(SelectedPlayerIdKey, playerId);
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
                PopulateList(seniorListParent, _club.seniorSquadIds, _state, BuildSquadFilter());
        }

        public void OnAssignViceCaptainClicked(int playerId)
        {
            if (_club == null)
                return;
            CaptainSystem.Assign(_club, playerId, isVice: true);
            if (_state != null)
                PopulateList(seniorListParent, _club.seniorSquadIds, _state, BuildSquadFilter());
        }

        // 현재 캡틴/부캡틴 ID 노출 — PlayerListItem 이 (C)/(VC) 배지 표시에 사용.
        public int CaptainId => GameManager.Instance?.UserClub?.season?.captainPlayerId ?? -1;
        public int ViceCaptainId =>
            GameManager.Instance?.UserClub?.season?.viceCaptainPlayerId ?? -1;
    }
}
