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

        [SerializeField]
        private TMP_InputField minAgeInput;

        [SerializeField]
        private TMP_InputField maxAgeInput;

        [SerializeField]
        private TMP_InputField nationalityInput;

        [SerializeField]
        private TMP_Dropdown traitDropdown;

        [SerializeField]
        private TMP_InputField minMarketValueInput;

        [SerializeField]
        private TMP_InputField maxMarketValueInput;

        [SerializeField]
        private TMP_InputField minContractMonthsInput;

        [SerializeField]
        private TMP_InputField maxContractMonthsInput;

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
            InitTraitDropdown();
            if (minCAInput != null)
                minCAInput.text = "0";
            if (maxCAInput != null)
                maxCAInput.text = "200";
            if (minAgeInput != null)
                minAgeInput.text = "16";
            if (maxAgeInput != null)
                maxAgeInput.text = "99";

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
            var userClub = _state.GetClub(_state.userClubId);

            foreach (Transform child in resultListParent)
                Destroy(child.gameObject);

            foreach (var player in results)
            {
                var item = Instantiate(playerItemPrefab, resultListParent);
                item.GetComponent<TransferPlayerItem>()
                    .Setup(player, _state, userClub, ShowOfferPanel);
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
            var options = new System.Collections.Generic.List<string>
            {
                Localization.Get("filter_all"),
            };
            foreach (Position pos in Enum.GetValues(typeof(Position)))
                options.Add(pos.ToString());
            positionDropdown.AddOptions(options);
        }

        private void InitTraitDropdown()
        {
            if (traitDropdown == null)
                return;
            traitDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>
            {
                Localization.Get("filter_all"),
            };
            foreach (var t in GameDatabase.AllTraits)
                options.Add(t.displayName);
            traitDropdown.AddOptions(options);
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

            if (int.TryParse(minAgeInput?.text, out int minAge))
                filter.minAge = minAge;
            if (int.TryParse(maxAgeInput?.text, out int maxAge))
                filter.maxAge = maxAge;

            var nat = nationalityInput?.text?.Trim();
            if (!string.IsNullOrEmpty(nat))
                filter.nationalityCode = nat;

            if (traitDropdown != null && traitDropdown.value > 0)
            {
                var traits = GameDatabase.AllTraits.ToList();
                int idx = traitDropdown.value - 1;
                if (idx < traits.Count)
                    filter.traitId = traits[idx].id;
            }

            if (int.TryParse(minMarketValueInput?.text, out int minMV))
                filter.minMarketValue = minMV;
            if (int.TryParse(maxMarketValueInput?.text, out int maxMV))
                filter.maxMarketValue = maxMV;

            if (int.TryParse(minContractMonthsInput?.text, out int minMonths))
                filter.minContractMonths = minMonths;
            if (int.TryParse(maxContractMonthsInput?.text, out int maxMonths))
                filter.maxContractMonths = maxMonths;

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
                    + Localization.Get("market_value_fmt", (mv / 1000000.0).ToString("0.0"));
            }

            if (offerAmountInput != null)
            {
                // AI Accept 조건 ratio >= balance.aiAcceptRatio (1.20). 시장가 그대로면
                // ratio ≈ 1/noise < 1.20 → 거의 항상 Reject. 시장가의 130% 를 권장 디폴트로
                // 채워 사용자가 그대로 보내도 합리적으로 Accept 가능 (#170).
                int suggested = (int)(mv * 1.30);
                offerAmountInput.text = suggested.ToString();
            }
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
                ? Localization.Get("transfer_window_open")
                : Localization.Get("transfer_window_closed");
        }

        private void RefreshActiveOffers()
        {
            if (activeOffersText == null)
                return;
            if (_state.activeOffers == null || _state.activeOffers.Count == 0)
            {
                activeOffersText.text = Localization.Get("no_active_offers");
                return;
            }

            var sb = new System.Text.StringBuilder(Localization.Get("active_offers_header") + "\n");
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
