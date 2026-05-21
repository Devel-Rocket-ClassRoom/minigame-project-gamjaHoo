// Task 13.8 (Issue #53) — 이적 검색 화면.
// 필터(포지션/CA) + 검색 결과 목록 + 오퍼 제출 패널 + 활성 오퍼 현황.

using System;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class TransferController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("이적 창 상태")]
        [SerializeField]
        private TMP_Text windowStatusText;

        [Header("필터")]
        [SerializeField]
        private TMP_Dropdown positionDropdown;

        [SerializeField]
        private TMP_InputField minCAInput;

        [SerializeField]
        private TMP_InputField maxCAInput;

        [Header("결과 목록")]
        [SerializeField]
        private Transform resultListParent;

        [SerializeField]
        private GameObject playerItemPrefab;

        [Header("오퍼 패널")]
        [SerializeField]
        private GameObject offerPanel;

        [SerializeField]
        private TMP_Text offerTargetText;

        [SerializeField]
        private TMP_InputField offerAmountInput;

        [SerializeField]
        private TMP_InputField offerWageInput;

        [SerializeField]
        private TMP_InputField offerYearsInput;

        [Header("활성 오퍼")]
        [SerializeField]
        private TMP_Text activeOffersText;

        private GameState _state;
        private int _selectedPlayerId = -1;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            InitPositionDropdown();
            if (minCAInput != null)
                minCAInput.text = "0";
            if (maxCAInput != null)
                maxCAInput.text = "200";

            if (offerPanel != null)
                offerPanel.SetActive(false);

            RefreshWindowStatus();
            RefreshActiveOffers();
        }

        public void OnSearchClicked()
        {
            if (_state == null)
                return;

            var filter = BuildFilter();
            var results = TransferSystem.SearchPlayers(filter, _state);

            foreach (Transform child in resultListParent)
                Destroy(child.gameObject);

            foreach (var player in results)
            {
                var item = Instantiate(playerItemPrefab, resultListParent);
                item.GetComponent<TransferPlayerItem>().Setup(player, _state, ShowOfferPanel);
            }
        }

        public void OnOfferSubmitClicked()
        {
            if (_state == null || _selectedPlayerId < 0)
                return;

            var player = _state.GetPlayer(_selectedPlayerId);
            if (player == null)
                return;

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            if (!TryParseOffer(out int amount, out int wage, out int years))
                return;

            var proposed = new Contract
            {
                weeklyWage = wage,
                startDate = _state.currentDate,
                endDate = _state.currentDate.AddYears(years),
                releaseClause = 0,
            };

            try
            {
                TransferSystem.SubmitOffer(
                    _selectedPlayerId,
                    player.currentClubId,
                    _state.userClubId,
                    amount,
                    proposed,
                    _state,
                    balance
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TransferController] SubmitOffer 실패: {e.Message}");
                return;
            }

            if (offerPanel != null)
                offerPanel.SetActive(false);
            _selectedPlayerId = -1;
            RefreshActiveOffers();
        }

        public void OnOfferCancelClicked()
        {
            if (offerPanel != null)
                offerPanel.SetActive(false);
            _selectedPlayerId = -1;
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        // ── 내부 헬퍼 ──────────────────────────────────────────────────

        private void InitPositionDropdown()
        {
            if (positionDropdown == null)
                return;
            positionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string> { "전체" };
            foreach (Position pos in Enum.GetValues(typeof(Position)))
                options.Add(pos.ToString());
            positionDropdown.AddOptions(options);
        }

        private TransferSearchFilter BuildFilter()
        {
            var filter = new TransferSearchFilter { excludeUserClub = true };

            if (positionDropdown != null && positionDropdown.value > 0)
                filter.position = (Position)(positionDropdown.value - 1);

            if (int.TryParse(minCAInput?.text, out int minCA))
                filter.minCA = minCA;
            if (int.TryParse(maxCAInput?.text, out int maxCA))
                filter.maxCA = maxCA;

            return filter;
        }

        private void ShowOfferPanel(int playerId)
        {
            _selectedPlayerId = playerId;
            var player = _state.GetPlayer(playerId);
            if (player == null)
                return;

            var balance = GameDatabase.GameBalance;
            int mv =
                balance != null ? TransferSystem.CalculateMarketValue(player, _state, balance) : 0;

            if (offerTargetText != null)
            {
                var club = _state.GetClub(player.currentClubId);
                offerTargetText.text =
                    $"{player.info?.firstName} {player.info?.lastName}  "
                    + $"({player.info?.primaryPosition})  {club?.name ?? "-"}  "
                    + $"시장가 £{mv / 1000000.0:0.0}M";
            }

            if (offerAmountInput != null)
                offerAmountInput.text = mv.ToString();
            if (offerWageInput != null)
                offerWageInput.text = "50000";
            if (offerYearsInput != null)
                offerYearsInput.text = "3";

            if (offerPanel != null)
                offerPanel.SetActive(true);
        }

        private bool TryParseOffer(out int amount, out int wage, out int years)
        {
            amount = 0;
            wage = 0;
            years = 0;
            if (!int.TryParse(offerAmountInput?.text, out amount) || amount <= 0)
                return false;
            if (!int.TryParse(offerWageInput?.text, out wage) || wage <= 0)
                return false;
            if (!int.TryParse(offerYearsInput?.text, out years) || years < 1 || years > 10)
                return false;
            return true;
        }

        private void RefreshWindowStatus()
        {
            if (windowStatusText == null)
                return;
            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            bool open = TransferSystem.IsTransferWindowOpen(_state.currentDate, balance);
            windowStatusText.text = open
                ? "이적 창 열림 (체결 가능)"
                : "이적 창 닫힘 (오퍼 제출만 가능)";
        }

        private void RefreshActiveOffers()
        {
            if (activeOffersText == null)
                return;
            if (_state.activeOffers == null || _state.activeOffers.Count == 0)
            {
                activeOffersText.text = "활성 오퍼 없음";
                return;
            }

            var sb = new System.Text.StringBuilder("[활성 오퍼]\n");
            foreach (var offer in _state.activeOffers)
            {
                var player = _state.GetPlayer(offer.playerId);
                string name =
                    player?.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={offer.playerId}";
                sb.AppendLine($"{name}  £{offer.amount / 1000000.0:0.0}M  [{offer.status}]");
            }
            activeOffersText.text = sb.ToString().TrimEnd();
        }
    }
}
