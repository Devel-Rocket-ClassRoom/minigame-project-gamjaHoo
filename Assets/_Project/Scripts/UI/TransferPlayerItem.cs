// TransferPlayerItem.cs
// 이적 검색 결과 목록 아이템 프리팹 컨트롤러.
// V0.5 E.4 — 가시성 분기: 명단 ∈ 정확 / ∉ 정성적 라벨 (회색조/자물쇠는 V1.0).

using System;
using FMLite.Application;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private Image clubCrest; // Stage AD — 선수 소속 구단 크레스트 (미배선/미생성 시 자동 숨김)

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
            {
                nameText.text =
                    player.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={player.id}";
                // R.6 (#77-1): 이름 클릭 → 선수 프로필 (타팀 = 스카우팅 게이팅).
                WireNameLink(player.id);
            }

            if (positionText != null)
                positionText.text = player.info?.primaryPosition.ToString() ?? "-";

            if (ageText != null)
                ageText.text = age.ToString();

            if (caText != null)
                caText.text = FormatCa(player.currentAbility, scouted);

            var currentClub = state.GetClub(player.currentClubId);
            if (clubText != null)
                clubText.text = currentClub?.name ?? "-";
            CrestProvider.ApplyClubCrest(clubCrest, currentClub?.name);

            if (marketValueText != null)
                marketValueText.text = FormatMarketValue(player, state, scouted);

            if (offerButton == null)
                return;
            offerButton.onClick.RemoveAllListeners();
            offerButton.onClick.AddListener(() => onOffer(player.id));
        }

        // R.6 (#77-1): 이름 TMP 에 런타임 Button 부착 → PlayerProfileScene 진입.
        private void WireNameLink(int playerId)
        {
            var go = nameText.gameObject;
            // 프리팹 Name TMP 는 raycastTarget=false → 클릭 받도록 활성화.
            nameText.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            if (btn == null)
                btn = go.AddComponent<Button>();
            btn.targetGraphic = nameText;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => NavigateToProfile(playerId));
        }

        private static void NavigateToProfile(int playerId)
        {
            PlayerPrefs.SetInt(SquadController.SelectedPlayerIdKey, playerId);
            PlayerPrefs.SetString(
                PlayerNameLinkController.PreviousSceneKey,
                SceneManager.GetActiveScene().name
            );
            SceneManager.LoadScene("PlayerProfileScene");
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
                return FMLite.Utils.CurrencyFormatter.Format(
                    mv,
                    FMLite.Application.OptionsManager.Currency
                );

            // 명단 ∉ — 정성적 라벨. market value 의 max 임계는 명성/시즌에 따라 다르나 단순화: ~£100M 기준.
            var tier = ScoutingVisibility.GetTier(mv, 100_000_000);
            return Localization.Get(ScoutingVisibility.GetTierLocalizationKey(tier));
        }
    }
}
