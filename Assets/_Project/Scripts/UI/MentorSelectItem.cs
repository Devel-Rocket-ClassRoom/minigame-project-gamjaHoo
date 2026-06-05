// MentorSelectItem.cs
// V1.0 I.1 — 멘토 선택 단일선택 리스트 행 (드롭다운 대체).
// MentoringController 가 멘토 후보마다 인스턴스화. 클릭 시 단일 선택 (컨트롤러가 하이라이트 관리).

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class MentorSelectItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text nameLabel;

        [SerializeField]
        private Image background;

        [SerializeField]
        private Button button;

        [SerializeField]
        private GameObject selectedIndicator; // 선택 시 채워지는 라디오 점 (멘티 토글 체크와 동일 위치)

        [SerializeField]
        private Color normalColor = new Color(0.173f, 0.173f, 0.243f, 0.863f);

        [SerializeField]
        private Color selectedColor = new Color(0.22f, 0.34f, 0.52f, 0.92f); // 은은한 accent tint

        private int _playerId;

        public int PlayerId => _playerId;

        public void Setup(int playerId, string label, Action<int> onClick)
        {
            _playerId = playerId;
            if (nameLabel != null)
                nameLabel.text = label;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick(playerId));
            }
        }

        public void SetSelected(bool selected)
        {
            if (background != null)
                background.color = selected ? selectedColor : normalColor;
            if (selectedIndicator != null)
                selectedIndicator.SetActive(selected);
        }
    }
}
