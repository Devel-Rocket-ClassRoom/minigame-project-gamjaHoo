// 이적 검색 결과 목록 아이템 프리팹 컨트롤러.

using System;
using FMLite.Application;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class TransferPlayerItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text positionText;

        [SerializeField]
        private TMP_Text ageText;

        [SerializeField]
        private TMP_Text caText;

        [SerializeField]
        private TMP_Text clubText;

        [SerializeField]
        private TMP_Text marketValueText;

        [SerializeField]
        private Button offerButton;

        public void Setup(Player player, GameState state, Action<int> onOffer)
        {
            int age =
                player.info != null
                    ? (int)((state.currentDate - player.info.birthDate).TotalDays / 365.25)
                    : 0;

            if (nameText != null)
                nameText.text =
                    player.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={player.id}";

            if (positionText != null)
                positionText.text = player.info?.primaryPosition.ToString() ?? "-";

            if (ageText != null)
                ageText.text = age.ToString();

            if (caText != null)
                caText.text = player.currentAbility.ToString();

            if (clubText != null)
            {
                var club = state.GetClub(player.currentClubId);
                clubText.text = club?.name ?? "-";
            }

            if (marketValueText != null)
            {
                var balance = GameDatabase.GameBalance;
                int mv =
                    balance != null
                        ? TransferSystem.CalculateMarketValue(player, state, balance)
                        : 0;
                marketValueText.text = $"£{mv / 1000000.0:0.0}M";
            }

            offerButton.onClick.RemoveAllListeners();
            offerButton.onClick.AddListener(() => onOffer(player.id));
        }
    }
}
