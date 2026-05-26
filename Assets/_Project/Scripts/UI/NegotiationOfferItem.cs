// NegotiationOfferItem.cs
// V1.0 K.5 — 협상 오퍼 목록 아이템. NegotiationController 가 Setup 호출.

using System;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class NegotiationOfferItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text playerNameText;

        [SerializeField]
        private TMP_Text amountText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private TMP_Text roundText;

        [SerializeField]
        private Button selectButton;

        public void Setup(
            TransferOffer offer,
            string playerName,
            string statusLabel,
            Action onSelect
        )
        {
            if (playerNameText != null)
                playerNameText.text = playerName;

            if (amountText != null)
                amountText.text = FormatMoney(offer.amount);

            if (statusText != null)
                statusText.text = statusLabel;

            if (roundText != null)
                roundText.text = offer.negotiationRound > 0 ? $"R{offer.negotiationRound}" : "";

            if (selectButton != null)
            {
                selectButton.interactable = offer.status == OfferStatus.CounterOffer;
                if (onSelect != null)
                    selectButton.onClick.AddListener(() => onSelect());
            }
        }

        private static string FormatMoney(int amount) =>
            amount >= 1_000_000 ? $"£{amount / 1_000_000.0:0.0}M" : $"£{amount / 1_000.0:0}K";
    }
}
