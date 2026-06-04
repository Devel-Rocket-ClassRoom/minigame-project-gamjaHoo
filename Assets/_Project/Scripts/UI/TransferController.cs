// Task 13.8 (Issue #53) — 이적 검색 화면.
// Stage F (#472): CA 필터 폐기(F.1) + 세부 stat/주급 필터(F.2) + 필터 패널 모달 분리(F.3).
//   메인 = 이름 검색창 + [필터] 버튼 + 결과 listing + 활성 오퍼.
//   [필터] → MUIP ModalWindow (포지션/나이/국적/트레잇/세부 stats/주급/시장가/계약) → [적용] → 닫힘+갱신.
//   활성 오퍼는 *제시 금액*(offer.amount) 표시 — 시장가 아님(F.4 #2).

using System;
using System.Collections.Generic;
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

        // 세부 stat 필터 한 줄 — stat 선택(드롭다운) + 최소값(입력). statDropdown.value==0 → 미사용.
        [Serializable]
        public class StatFilterRow
        {
            public TMP_Dropdown statDropdown;
            public TMP_InputField minValueInput;
        }

        [Header("이적 창 상태")]
        [SerializeField]
        private TMP_Text windowStatusText;

        [Header("메인 — 검색창 + 필터 버튼")]
        [SerializeField]
        private TMP_InputField searchNameInput;

        [SerializeField]
        private Button filterButton;

        [SerializeField]
        private Button searchButton;

        [Header("필터 모달 (MUIP ModalWindow)")]
        [SerializeField]
        private GameObject filterModal;

        [SerializeField]
        private Button applyFilterButton;

        [SerializeField]
        private Button closeFilterButton;

        [Header("필터 — 모달 내부 컨트롤 (CA 필터 폐기: F.1)")]
        [SerializeField]
        private TMP_Dropdown positionDropdown;

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

        [Header("필터 — 주급 범위 (F.2)")]
        [SerializeField]
        private TMP_InputField minWageInput;

        [SerializeField]
        private TMP_InputField maxWageInput;

        [Header("필터 — 세부 stat 임계 (F.2)")]
        [SerializeField]
        private StatFilterRow[] statFilterRows = Array.Empty<StatFilterRow>();

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
            InitStatFilterRows();

            if (minAgeInput != null)
                minAgeInput.text = "16";
            if (maxAgeInput != null)
                maxAgeInput.text = "99";

            WireButtons();

            if (filterModal != null)
                filterModal.SetActive(false);
            if (offerPanel != null)
                offerPanel.SetActive(false);

            RefreshWindowStatus();
            RefreshActiveOffers();
            OnSearchClicked();
        }

        private void WireButtons()
        {
            if (filterButton != null)
            {
                filterButton.onClick.RemoveAllListeners();
                filterButton.onClick.AddListener(OnFilterClicked);
            }
            if (searchButton != null)
            {
                searchButton.onClick.RemoveAllListeners();
                searchButton.onClick.AddListener(OnSearchClicked);
            }
            if (applyFilterButton != null)
            {
                applyFilterButton.onClick.RemoveAllListeners();
                applyFilterButton.onClick.AddListener(OnApplyFilterClicked);
            }
            if (closeFilterButton != null)
            {
                closeFilterButton.onClick.RemoveAllListeners();
                closeFilterButton.onClick.AddListener(OnCloseFilterClicked);
            }
            if (searchNameInput != null)
            {
                searchNameInput.onSubmit.RemoveAllListeners();
                searchNameInput.onSubmit.AddListener(_ => OnSearchClicked());
            }
        }

        // ── 필터 모달 ────────────────────────────────────────────────

        public void OnFilterClicked()
        {
            if (filterModal != null)
                filterModal.SetActive(true);
        }

        public void OnCloseFilterClicked()
        {
            if (filterModal != null)
                filterModal.SetActive(false);
        }

        public void OnApplyFilterClicked()
        {
            if (filterModal != null)
                filterModal.SetActive(false);
            OnSearchClicked();
        }

        // ── 검색 ─────────────────────────────────────────────────────

        public void OnSearchClicked()
        {
            if (_state == null || resultListParent == null || playerItemPrefab == null)
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

            if (!TryParseAmount(out int amount))
                return;

            // 오퍼는 이적료만 (#469). 개인 조건(주급/계약기간/출전약속)은 구단 합의 후
            // PlayerNegotiationScene 에서 협상. 여기선 공정 주급 / 3년 기본 계약을 placeholder 로 부여.
            int defaultWage = TransferSystem.SuggestFairWage(player, balance);
            var proposed = new Contract
            {
                weeklyWage = defaultWage,
                startDate = _state.currentDate,
                endDate = _state.currentDate.AddYears(3),
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
            var options = new List<string> { Localization.Get("filter_all") };
            foreach (Position pos in Enum.GetValues(typeof(Position)))
                options.Add(pos.ToString());
            positionDropdown.AddOptions(options);
            positionDropdown.value = 0;
        }

        private void InitTraitDropdown()
        {
            if (traitDropdown == null)
                return;
            traitDropdown.ClearOptions();
            var options = new List<string> { Localization.Get("filter_all") };
            foreach (var t in GameDatabase.AllTraits)
                options.Add(t.displayName);
            traitDropdown.AddOptions(options);
            traitDropdown.value = 0;
        }

        // 세부 stat 드롭다운 = "전체"(0, 미사용) + 49 stat (StatCatalog 순서, 로컬라이즈 라벨).
        private void InitStatFilterRows()
        {
            if (statFilterRows == null)
                return;
            foreach (var row in statFilterRows)
            {
                if (row?.statDropdown == null)
                    continue;
                row.statDropdown.ClearOptions();
                var options = new List<string> { Localization.Get("filter_all") };
                foreach (var d in StatCatalog.All)
                    options.Add(Localization.Get(d.labelKey));
                row.statDropdown.AddOptions(options);
                row.statDropdown.value = 0;
            }
        }

        private TransferSearchFilter BuildFilter()
        {
            var filter = new TransferSearchFilter { excludeUserClub = true };

            if (positionDropdown != null && positionDropdown.value > 0)
                filter.position = (Position)(positionDropdown.value - 1);

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

            // F.2 주급 범위
            if (int.TryParse(minWageInput?.text, out int minWage))
                filter.minWage = minWage;
            if (int.TryParse(maxWageInput?.text, out int maxWage))
                filter.maxWage = maxWage;

            // F.2 세부 stat 임계 — 드롭다운 value>0 인 행만, fieldPath → 최소값.
            if (statFilterRows != null)
            {
                foreach (var row in statFilterRows)
                {
                    if (row?.statDropdown == null || row.statDropdown.value <= 0)
                        continue;
                    int catIdx = row.statDropdown.value - 1;
                    if (catIdx < 0 || catIdx >= StatCatalog.All.Count)
                        continue;
                    if (int.TryParse(row.minValueInput?.text, out int minVal) && minVal > 0)
                        filter.statThresholds[StatCatalog.All[catIdx].fieldPath] = minVal;
                }
            }

            // 이름 검색
            var name = searchNameInput?.text?.Trim();
            if (!string.IsNullOrEmpty(name))
                filter.nameContains = name;

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
                    + Localization.Get(
                        "market_value_fmt",
                        FMLite.Utils.CurrencyFormatter.Format(
                            mv,
                            FMLite.Application.OptionsManager.Currency
                        )
                    );
            }

            if (offerAmountInput != null)
            {
                // AI Accept 조건 ratio >= balance.aiAcceptThreshold (1.30). 시장가 그대로면
                // ratio ≈ 1/noise < 1.30 → 거의 항상 Reject. 시장가의 130% 를 권장 디폴트로
                // 채워 사용자가 그대로 보내도 합리적으로 Accept 가능 (#170).
                int suggested = (int)(mv * 1.30);
                offerAmountInput.text = suggested.ToString();
            }

            if (offerPanel != null)
                offerPanel.SetActive(true);
        }

        // 오퍼는 이적료만 검증 (#469 — 개인 조건은 PlayerNegotiationScene).
        private bool TryParseAmount(out int amount)
        {
            amount = 0;
            return int.TryParse(offerAmountInput?.text, out amount) && amount > 0;
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

        // 활성 오퍼는 *제시 금액*(offer.amount) 표시 (시장가 아님 — F.4 #2).
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
                sb.AppendLine(
                    $"{name}  {FMLite.Utils.CurrencyFormatter.Format(offer.amount, FMLite.Application.OptionsManager.Currency)}  [{offer.status}]"
                );
            }
            activeOffersText.text = sb.ToString().TrimEnd();
        }
    }
}
