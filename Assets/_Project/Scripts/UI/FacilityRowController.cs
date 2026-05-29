// FacilityRowController.cs
// 시설 1종을 표시하는 row 컨트롤러. FacilityController 가 8 시설마다 인스턴스화.
// V0.5 D.5 — 동적 생성 모델.

using System;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class FacilityRowController : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text levelText;

        [SerializeField]
        private TMP_Text currentEffectText;

        [SerializeField]
        private TMP_Text nextEffectText;

        [SerializeField]
        private TMP_Text costText;

        [SerializeField]
        private Button upgradeButton;

        private FacilityType _type;
        private Action<FacilityType> _onUpgradeClicked;

        public FacilityType Type => _type;

        public void Init(FacilityType type, Action<FacilityType> onUpgradeClicked)
        {
            _type = type;
            _onUpgradeClicked = onUpgradeClicked;
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(() => _onUpgradeClicked?.Invoke(_type));
            }
        }

        public void Render(
            string displayName,
            int currentLevel,
            int maxLevel,
            string currentEffect,
            string nextEffect,
            string costLabel,
            bool canUpgrade
        )
        {
            if (nameText != null)
                nameText.text = displayName;
            if (levelText != null)
                levelText.text = $"Lv {currentLevel} / {maxLevel}";
            if (currentEffectText != null)
                currentEffectText.text = currentEffect;
            if (nextEffectText != null)
                nextEffectText.text = nextEffect;
            if (costText != null)
                costText.text = costLabel;
            if (upgradeButton != null)
                upgradeButton.interactable = canUpgrade;
        }
    }
}
