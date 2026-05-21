// 유스 후보 목록 아이템 프리팹 컨트롤러.

using System;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class YouthCandidateItem : MonoBehaviour
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
        private Toggle selectToggle;

        public int PlayerId { get; private set; }

        public void Setup(Player player, GameState state, Action<int, bool> onToggle)
        {
            PlayerId = player.id;

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

            if (selectToggle != null)
            {
                selectToggle.isOn = false;
                selectToggle.onValueChanged.RemoveAllListeners();
                selectToggle.onValueChanged.AddListener(isOn => onToggle(player.id, isOn));
            }
        }
    }
}
