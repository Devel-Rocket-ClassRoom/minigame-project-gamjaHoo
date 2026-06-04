// PlayerNegotiationController.cs
// V1.0 #469 — 선수 개인 조건 협상 화면 (Negotiating 단계).
// 구단 이적료 합의 후 주급/계약기간/출전약속을 제안 → RespondToPersonalTerms.
// 반복 협상: 거절 시 조건을 올려 재제안 (최대 maxPersonalNegotiationRounds).
// 씬 배치 / 프리팹 와이어링은 Claude Code MCP. NegotiationOfferItem 프리팹 재사용.

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
    public class PlayerNegotiationController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("오퍼 목록 (Negotiating)")]
        [SerializeField]
        private Transform offerListParent;

        [SerializeField]
        private GameObject offerItemPrefab;

        [SerializeField]
        private TMP_Text emptyLabel;

        [Header("개인 조건 패널")]
        [SerializeField]
        private GameObject termsPanel;

        [SerializeField]
        private TMP_Text termsTitleText;

        [SerializeField]
        private TMP_InputField wageInput;

        [SerializeField]
        private TMP_InputField yearsInput;

        [SerializeField]
        private Toggle playtimeToggle;

        [SerializeField]
        private TMP_Text reactionText;

        [SerializeField]
        private TMP_Text resultText;

        [SerializeField]
        private Button proposeButton;

        [SerializeField]
        private Button backButton;

        private GameState _state;
        private int _selectedOfferId = -1;

        // ── Unity Lifecycle ──────────────────────────────────────────

        private void Awake()
        {
            if (proposeButton != null)
                proposeButton.onClick.AddListener(OnPropose);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
            if (wageInput != null)
                wageInput.onValueChanged.AddListener(_ => UpdateReaction());
            if (playtimeToggle != null)
                playtimeToggle.onValueChanged.AddListener(_ => UpdateReaction());

            HideTermsPanel();
        }

        private void Start()
        {
            _state = GameManager.Instance?.State;
            RefreshOfferList();
        }

        // ── 공개 메서드 ──────────────────────────────────────────────

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        public void RefreshOfferList()
        {
            RebuildRows();
            HideTermsPanel();
        }

        // ── 내부 ─────────────────────────────────────────────────────

        private void RebuildRows()
        {
            if (offerListParent != null)
                foreach (Transform child in offerListParent)
                    Destroy(child.gameObject);

            // _state null 이어도 EmptyLabel 은 표시해야 함 (GetRelevantOffers 는 null-safe).
            var relevant = GetRelevantOffers();

            if (emptyLabel != null)
                emptyLabel.gameObject.SetActive(relevant.Count == 0);

            foreach (var offer in relevant)
                SpawnOfferItem(offer);

            // 동적 생성 직후 레이아웃 강제 재빌드 — 안 하면 다음 Canvas 업데이트(클릭/호버 등)
            // 전까지 VLG+ContentSizeFitter 가 행 크기·위치를 정착시키지 않아 행이 보이지 않음
            // (R.12 "가만히 있다 클릭하면 복불복으로 뜸" 증상의 근본 원인).
            if (offerListParent is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private List<TransferOffer> GetRelevantOffers()
        {
            var result = new List<TransferOffer>();
            if (_state?.activeOffers == null)
                return result;

            foreach (var offer in _state.activeOffers)
            {
                if (offer == null)
                    continue;
                if (offer.toClubId != _state.userClubId)
                    continue;
                if (offer.status == OfferStatus.Negotiating)
                    result.Add(offer);
            }
            return result;
        }

        private void SpawnOfferItem(TransferOffer offer)
        {
            if (offerItemPrefab == null || offerListParent == null)
                return;

            var go = Instantiate(offerItemPrefab, offerListParent);
            var player = _state.GetPlayer(offer.playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={offer.playerId}";

            string statusLabel = Localization.Get("pnego_status_negotiating");

            var item = go.GetComponent<NegotiationOfferItem>();
            if (item != null)
            {
                item.Setup(offer, playerName, statusLabel, () => SelectOffer(offer.id));
            }
            else
            {
                var text = go.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = $"{playerName}  {statusLabel}";
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    int capturedId = offer.id;
                    btn.onClick.AddListener(() => SelectOffer(capturedId));
                }
            }
        }

        private void SelectOffer(int offerId)
        {
            var offer = _state?.activeOffers.Find(o => o != null && o.id == offerId);
            if (offer == null || offer.status != OfferStatus.Negotiating)
                return;

            _selectedOfferId = offerId;

            // 패널을 먼저 활성화 — 이후 어떤 갱신 로직이 실패해도 패널 표시는 보장.
            ShowTermsPanel();

            var player = _state.GetPlayer(offer.playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={offer.playerId}";

            if (termsTitleText != null)
                termsTitleText.text = Localization.Get("pnego_terms_title_fmt", playerName);

            var balance = GameDatabase.GameBalance;
            int suggested =
                player != null && balance != null
                    ? TransferSystem.SuggestFairWage(player, balance)
                    : 0;
            int initialWage =
                offer.proposed != null && offer.proposed.weeklyWage > 0
                    ? offer.proposed.weeklyWage
                    : suggested;

            // SetTextWithoutNotify — onValueChanged(→UpdateReaction) 가 SelectOffer 도중 끼어들지 않게.
            if (wageInput != null)
                wageInput.SetTextWithoutNotify(initialWage.ToString());
            if (yearsInput != null)
                yearsInput.SetTextWithoutNotify("3");
            if (playtimeToggle != null)
                playtimeToggle.SetIsOnWithoutNotify(offer.includesPlaytimeAgreement);
            if (resultText != null)
                resultText.text = string.Empty;
            if (proposeButton != null)
                proposeButton.interactable = true;

            UpdateReaction();
        }

        // 입력 변경 시 선수 반응 라벨 갱신 (확률 기반 3단계).
        private void UpdateReaction()
        {
            if (reactionText == null || _selectedOfferId < 0 || _state == null)
                return;

            var offer = _state.activeOffers.Find(o => o != null && o.id == _selectedOfferId);
            if (offer == null)
                return;

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            if (!int.TryParse(wageInput?.text, out int wage))
                wage = 0;
            bool playtime = playtimeToggle != null && playtimeToggle.isOn;

            double chance = TransferSystem.EstimatePlayerAcceptChance(
                offer.playerId,
                wage,
                playtime,
                _state,
                balance
            );

            string moodKey;
            Color color;
            if (chance >= 0.7)
            {
                moodKey = "pnego_reaction_happy";
                color = ReactionHappy;
            }
            else if (chance >= 0.4)
            {
                moodKey = "pnego_reaction_think";
                color = ReactionThink;
            }
            else
            {
                moodKey = "pnego_reaction_unhappy";
                color = ReactionUnhappy;
            }

            // "주급 £X / 주" 캡션(단위 명시) + 줄바꿈 + 기분 문구 (색 코딩).
            string wageStr = FMLite.Utils.CurrencyFormatter.Format(
                wage,
                FMLite.Application.OptionsManager.Currency
            );
            reactionText.text =
                Localization.Get("pnego_wage_caption_fmt", wageStr)
                + "\n"
                + Localization.Get(moodKey);
            reactionText.color = color;
        }

        // 반응 색 (만족=녹색 / 고민=호박 / 불만=적색). muip-reference §18 팔레트.
        private static readonly Color ReactionHappy = new Color(0.176f, 0.800f, 0.443f); // #2ECC71
        private static readonly Color ReactionThink = new Color(0.910f, 0.627f, 0.231f); // #E8A03B
        private static readonly Color ReactionUnhappy = new Color(0.910f, 0.314f, 0.314f); // #E85050

        private void OnPropose()
        {
            if (_selectedOfferId < 0 || _state == null)
                return;

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return;

            if (!int.TryParse(wageInput?.text, out int wage) || wage <= 0)
                return;
            if (!int.TryParse(yearsInput?.text, out int years) || years < 1 || years > 10)
                years = 3;
            bool playtime = playtimeToggle != null && playtimeToggle.isOn;

            var proposed = new Contract
            {
                weeklyWage = wage,
                startDate = _state.currentDate,
                endDate = _state.currentDate.AddYears(years),
                releaseClause = 0,
            };

            PersonalTermsResult result;
            try
            {
                result = TransferSystem.RespondToPersonalTerms(
                    _selectedOfferId,
                    proposed,
                    playtime,
                    _state,
                    balance
                );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerNegotiationController] 제안 실패: {e.Message}");
                return;
            }

            var offer = _state.activeOffers.Find(o => o != null && o.id == _selectedOfferId);

            if (resultText != null)
            {
                resultText.text = result switch
                {
                    PersonalTermsResult.Accepted => Localization.Get("pnego_result_accepted"),
                    PersonalTermsResult.Rejected => Localization.Get("pnego_result_rejected"),
                    _ => Localization.Get(
                        "pnego_result_still_fmt",
                        offer?.personalNegotiationRound ?? 0,
                        balance.maxPersonalNegotiationRounds
                    ),
                };
            }

            GlobalNavController.Instance?.RefreshFromState();

            if (result == PersonalTermsResult.StillNegotiating)
            {
                UpdateReaction();
            }
            else
            {
                // 협상 종료 — 더 이상 제안 불가, 목록에서 제거(rows 만 갱신, 결과 메시지 유지).
                if (proposeButton != null)
                    proposeButton.interactable = false;
                RebuildRows();
            }
        }

        private void ShowTermsPanel()
        {
            if (termsPanel != null)
                termsPanel.SetActive(true);
        }

        private void HideTermsPanel()
        {
            _selectedOfferId = -1;
            if (termsPanel != null)
                termsPanel.SetActive(false);
        }
    }
}
