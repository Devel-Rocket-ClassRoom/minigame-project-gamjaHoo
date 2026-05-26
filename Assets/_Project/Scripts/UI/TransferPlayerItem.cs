// TransferPlayerItem.cs
// 이적 검색 결과 목록 아이템 프리팹 컨트롤러.
// V1.0 E.4 — 가시성 분기: 명단 ∈ 정확 / ∉ 정성적 라벨 (회색조/자물쇠는 V1.x).

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

        public void Setup(Player player, GameState state, Club userClub, Action<int> onOffer)
        {
            int age =
                player.info != null
                    ? (int)((state.currentDate - player.info.birthDate).TotalDays / 365.25)
                    : 0;

            bool scouted = ScoutingVisibility.IsScouted(userClub, player.id);

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
                caText.text = FormatCa(player.currentAbility, scouted);

            if (clubText != null)
            {
                var club = state.GetClub(player.currentClubId);
                clubText.text = club?.name ?? "-";
            }

            if (marketValueText != null)
                marketValueText.text = FormatMarketValue(player, state, scouted);

            if (offerButton == null)
                return;
            offerButton.onClick.RemoveAllListeners();
            offerButton.onClick.AddListener(() => onOffer(player.id));
        }

        // 명단 ∈: 정확 수치 (예: "145")
        // 명단 ∉: 정성적 라벨 (예: "높음")
        private static string FormatCa(int ca, bool scouted)
        {
            if (scouted)
                return ca.ToString();
            var tier = ScoutingVisibility.GetTier(ca, 200);
            return Localization.Get(ScoutingVisibility.GetTierLocalizationKey(tier));
        }

        // 명단 ∈: 정확 금액 (예: "£12.5M")
        // 명단 ∉: 정성적 라벨 (CA 기반 추정 — market value 도 CA 와 강하게 연관)
        private static string FormatMarketValue(Player player, GameState state, bool scouted)
        {
            var balance = GameDatabase.GameBalance;
            int mv =
                balance != null ? TransferSystem.CalculateMarketValue(player, state, balance) : 0;

            if (scouted)
                return $"£{mv / 1000000.0:0.0}M";

            // 명단 ∉ — 정성적 라벨. market value 의 max 임계는 명성/시즌에 따라 다르나 단순화: ~£100M 기준.
            var tier = ScoutingVisibility.GetTier(mv, 100_000_000);
            return Localization.Get(ScoutingVisibility.GetTierLocalizationKey(tier));
        }
    }
}
