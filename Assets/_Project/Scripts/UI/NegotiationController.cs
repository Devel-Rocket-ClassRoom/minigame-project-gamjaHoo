// NegotiationController.cs
// V1.0 K.5 — 협상 진행 화면.
// CounterOffer 상태 오퍼 목록 + 응답 패널 (수락 / 거절 / 재역제안).
// 씬 배치 / 프리팹 와이어링은 Unity AI Assistant. 코드만 Claude Code.

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
    public class NegotiationController : MonoBehaviour
    {
        private const string TransferScene = "TransferScene";

        [Header("오퍼 목록")]
        [SerializeField]
        private Transform offerListParent;

        [SerializeField]
        private GameObject offerItemPrefab;

        [Header("응답 패널 (CounterOffer)")]
        [SerializeField]
        private GameObject responsePanel;

        [SerializeField]
        private TMP_Text responseTitleText;

        [SerializeField]
        private TMP_Text responseDetailText;

        [SerializeField]
        private TMP_InputField reCounterAmountInput;

        [SerializeField]
        private Button acceptButton;

        [SerializeField]
        private Button rejectButton;

        [SerializeField]
        private Button reCounterButton;

        [Header("상태 레이블")]
        [SerializeField]
        private TMP_Text emptyLabel;

        private GameState _state;
        private int _selectedOfferId = -1;

        // ── Unity Lifecycle ──────────────────────────────────────────

        private void Awake()
        {
            if (acceptButton != null)
                acceptButton.onClick.AddListener(OnAccept);
            if (rejectButton != null)
                rejectButton.onClick.AddListener(OnReject);
            if (reCounterButton != null)
                reCounterButton.onClick.AddListener(OnReCounter);

            HideResponsePanel();
        }

        private void Start()
        {
            _state = GameManager.Instance?.State;
            RefreshOfferList();
        }

        // ── 공개 메서드 ──────────────────────────────────────────────

        public void RefreshOfferList()
        {
            if (offerListParent != null)
                foreach (Transform child in offerListParent)
                    Destroy(child.gameObject);

            if (_state == null)
                return;

            var relevant = GetRelevantOffers();

            if (emptyLabel != null)
                emptyLabel.gameObject.SetActive(relevant.Count == 0);

            foreach (var offer in relevant)
                SpawnOfferItem(offer);

            HideResponsePanel();
        }

        public void OnBackClicked() => SceneManager.LoadScene(TransferScene);

        // ── CounterOffer 응답 ────────────────────────────────────────

        private void OnAccept()
        {
            if (_selectedOfferId < 0 || _state == null)
                return;

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            try
            {
                TransferSystem.RespondToCounterOffer(
                    _selectedOfferId,
                    CounterResponse.Accept,
                    0,
                    _state,
                    balance
                );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NegotiationController] Accept 실패: {e.Message}");
            }

            RefreshOfferList();
        }

        private void OnReject()
        {
            if (_selectedOfferId < 0 || _state == null)
                return;

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            try
            {
                TransferSystem.RespondToCounterOffer(
                    _selectedOfferId,
                    CounterResponse.Reject,
                    0,
                    _state,
                    balance
                );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NegotiationController] Reject 실패: {e.Message}");
            }

            RefreshOfferList();
        }

        private void OnReCounter()
        {
            if (_selectedOfferId < 0 || _state == null)
                return;

            if (
                reCounterAmountInput == null
                || !int.TryParse(reCounterAmountInput.text, out int newAmount)
                || newAmount <= 0
            )
            {
                Debug.LogWarning("[NegotiationController] 재역제안 금액 입력 오류");
                return;
            }

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            try
            {
                TransferSystem.RespondToCounterOffer(
                    _selectedOfferId,
                    CounterResponse.ReCounter,
                    newAmount,
                    _state,
                    balance
                );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NegotiationController] ReCounter 실패: {e.Message}");
            }

            RefreshOfferList();
        }

        // ── 오퍼 선택 ────────────────────────────────────────────────

        private void SelectOffer(int offerId)
        {
            var offer = _state?.activeOffers.Find(o => o != null && o.id == offerId);
            if (offer == null || offer.status != OfferStatus.CounterOffer)
                return;

            _selectedOfferId = offerId;

            var player = _state.GetPlayer(offer.playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={offer.playerId}";

            bool isAccept = offer.counterAmount == offer.amount;

            if (responseTitleText != null)
                responseTitleText.text = isAccept
                    ? Localization.Get("negotiation_accepted_title_fmt", playerName)
                    : Localization.Get("negotiation_counter_title_fmt", playerName);

            if (responseDetailText != null)
                responseDetailText.text = isAccept
                    ? Localization.Get("negotiation_accepted_detail_fmt", FormatMoney(offer.amount))
                    : Localization.Get(
                        "negotiation_counter_detail_fmt",
                        FormatMoney(offer.amount),
                        FormatMoney(offer.counterAmount),
                        offer.negotiationRound
                    );

            // 수락 상태에서는 재역제안 불필요
            if (reCounterButton != null)
                reCounterButton.gameObject.SetActive(!isAccept);
            if (reCounterAmountInput != null)
            {
                reCounterAmountInput.gameObject.SetActive(!isAccept);
                if (!isAccept)
                    reCounterAmountInput.text = offer.counterAmount.ToString();
            }

            ShowResponsePanel();
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        private List<TransferOffer> GetRelevantOffers()
        {
            var result = new List<TransferOffer>();
            if (_state?.activeOffers == null)
                return result;

            foreach (var offer in _state.activeOffers)
            {
                if (offer == null)
                    continue;
                // 유저 클럽이 매수 측인 오퍼만 (toClubId == userClubId)
                if (offer.toClubId != _state.userClubId)
                    continue;
                if (
                    offer.status == OfferStatus.CounterOffer
                    || offer.status == OfferStatus.Pending
                    || offer.status == OfferStatus.Negotiating
                    || offer.status == OfferStatus.Accepted
                )
                    result.Add(offer);
            }
            return result;
        }

        private void SpawnOfferItem(TransferOffer offer)
        {
            if (offerItemPrefab == null || offerListParent == null)
                return;

            var go = Instantiate(offerItemPrefab, offerListParent);

            // 플레이어 이름
            var player = _state.GetPlayer(offer.playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={offer.playerId}";

            // 상태 레이블 — AI 수락(counterAmount==amount) vs 역제안 구분
            string statusLabel = offer.status switch
            {
                OfferStatus.CounterOffer => offer.counterAmount == offer.amount
                    ? Localization.Get("negotiation_status_ai_accepted")
                    : Localization.Get("negotiation_status_counter"),
                OfferStatus.Pending => Localization.Get("negotiation_status_pending"),
                OfferStatus.Negotiating => Localization.Get("negotiation_status_negotiating"),
                OfferStatus.Accepted => Localization.Get("negotiation_status_accepted"),
                _ => offer.status.ToString(),
            };

            string line =
                $"{playerName}  {FormatMoney(offer.amount)}  [{statusLabel}]"
                + (offer.negotiationRound > 0 ? $"  R{offer.negotiationRound}" : "");

            // NegotiationOfferItem 컴포넌트가 있으면 Setup, 없으면 TMP_Text 직접
            var item = go.GetComponent<NegotiationOfferItem>();
            if (item != null)
            {
                item.Setup(offer, playerName, statusLabel, () => SelectOffer(offer.id));
            }
            else
            {
                var text = go.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = line;

                var btn = go.GetComponent<Button>();
                if (btn != null && offer.status == OfferStatus.CounterOffer)
                {
                    int capturedId = offer.id;
                    btn.onClick.AddListener(() => SelectOffer(capturedId));
                }
            }
        }

        private void ShowResponsePanel()
        {
            if (responsePanel != null)
                responsePanel.SetActive(true);
        }

        private void HideResponsePanel()
        {
            _selectedOfferId = -1;
            if (responsePanel != null)
                responsePanel.SetActive(false);
        }

        private static string FormatMoney(int amount) =>
            amount >= 1_000_000
                ? $"£{amount / 1_000_000.0:0.0}M"
                : $"£{amount / 1_000.0:0}K";
    }
}
