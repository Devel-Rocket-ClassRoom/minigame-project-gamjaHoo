// 구단 선택 화면 목록 아이템 프리팹 컨트롤러.

using System;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class ClubListItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text clubNameText;

        [SerializeField]
        private TMP_Text reputationText;

        [SerializeField]
        private Button selectButton;

        private Action<int> onSelected;

        public void Setup(Club club, Action<int> selectCallback)
        {
            clubNameText.text = club.name;
            reputationText.text = $"명성 {club.reputation}";
            onSelected = selectCallback;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected(club.id));
        }
    }
}
