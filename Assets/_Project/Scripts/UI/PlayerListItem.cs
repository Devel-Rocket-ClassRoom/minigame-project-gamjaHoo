// 스쿼드 화면 선수 목록 아이템 프리팹 컨트롤러.
// Stage D (V1.0, #459): CA 변화량 화살표/색 (직전 3개월, CaCalculator.GetCaChange + StatColorCoding).

using System;
using FMLite.Application;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text positionText;

        [SerializeField]
        private TMP_Text ageText;

        [SerializeField]
        private TMP_Text caText;

        [Tooltip("Stage D — CA 직전 3개월 변화량 (부호付 숫자 + 색). 없으면 생략.")]
        [SerializeField]
        private TMP_Text caChangeText;

        [SerializeField]
        private Button selectButton;

        public void Setup(Player player, GameState state, Action<int> onSelect)
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

            if (caChangeText != null)
            {
                int caChange = CaCalculator.GetCaChange(player, 3);
                caChangeText.text = StatColorCoding.TrendArrow(caChange);
                caChangeText.color = StatColorCoding.TrendColor(caChange);
            }

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect(player.id));
        }
    }
}
