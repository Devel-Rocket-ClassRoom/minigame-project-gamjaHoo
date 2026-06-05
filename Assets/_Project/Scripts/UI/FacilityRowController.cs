// FacilityRowController.cs
// 시설 1종 카드. FacilityController 가 8 시설마다 인스턴스화 (동적 생성, V0.5 D.5).
// V1.0 L.1/L.2: 색상 배지 + 10-pip 등급 바 + 업그레이드 ProgressBar + 현재→다음 효과 비교 + Tooltip.

using System;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class FacilityRowController : MonoBehaviour
    {
        [Header("아이콘 배지")]
        [SerializeField]
        private Image iconBadge;

        [SerializeField]
        private TMP_Text iconLabel;

        [Header("이름 / 등급")]
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text levelText;

        [SerializeField]
        private Image gradeFill; // fillAmount = currentLevel / maxLevel

        [Header("효과 비교 (현재 → 다음)")]
        [SerializeField]
        private TMP_Text currentEffectText;

        [SerializeField]
        private TMP_Text nextEffectText;

        [Header("업그레이드")]
        [SerializeField]
        private TMP_Text costText;

        [SerializeField]
        private Button upgradeButton;

        [SerializeField]
        private TMP_Text upgradeButtonText;

        [Header("진행 중 표시")]
        [SerializeField]
        private GameObject progressRoot;

        [SerializeField]
        private Image upgradeFill; // fillAmount = progressPercent / 100

        [SerializeField]
        private TMP_Text progressLabel;

        [Header("툴팁")]
        [SerializeField]
        private TooltipContent tooltip;

        private static readonly Color ImproveColor = new Color(0.30f, 0.80f, 0.44f, 1f); // #4CAF50
        private static readonly Color DimColor = new Color(0.80f, 0.80f, 0.80f, 1f); // #CCCCCC
        private static readonly Color CardBg = new Color(0.165f, 0.165f, 0.243f, 1f); // #2A2A3E
        private static readonly Color BarBg = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color Accent = new Color(0.290f, 0.565f, 0.851f, 1f); // #4A90D9
        private static readonly Color Gold = new Color(0.945f, 0.769f, 0.059f, 1f); // #F1C40F
        private static readonly Color SubText = new Color(0.80f, 0.80f, 0.80f, 1f); // #CCCCCC

        private FacilityType _type;
        private Action<FacilityType> _onUpgradeClicked;

        public FacilityType Type => _type;

        // MCP create 시 직렬화 속성(색/정렬/스케일)이 불안정하게 적용돼, 정적 스타일을 런타임에 코드로 강제.
        private void Awake()
        {
            transform.localScale = Vector3.one; // 프리팹 root 보정 스케일 제거 (Content 하위 = world 1)

            var cardImg = GetComponent<Image>();
            if (cardImg != null)
                cardImg.color = CardBg;
            if (gradeFill != null)
            {
                gradeFill.color = Accent;
                MakeHorizontalFill(gradeFill);
                var bg = gradeFill.transform.parent != null
                    ? gradeFill.transform.parent.GetComponent<Image>()
                    : null;
                if (bg != null)
                    bg.color = BarBg;
            }
            if (upgradeFill != null)
            {
                upgradeFill.color = Gold;
                MakeHorizontalFill(upgradeFill);
                var bg = upgradeFill.transform.parent != null
                    ? upgradeFill.transform.parent.GetComponent<Image>()
                    : null;
                if (bg != null)
                    bg.color = BarBg;
            }
            if (upgradeButton != null && upgradeButton.targetGraphic != null)
                upgradeButton.targetGraphic.color = Accent;

            SetAlign(iconLabel, TextAlignmentOptions.Center, Color.white);
            SetAlign(nameText, TextAlignmentOptions.Left, Color.white);
            SetAlign(levelText, TextAlignmentOptions.Right, Accent);
            if (levelText != null)
            {
                var le = levelText.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = 150;
                    le.preferredWidth = 150;
                }
            }
            SetAlign(currentEffectText, TextAlignmentOptions.Left, SubText);
            SetAlign(costText, TextAlignmentOptions.Center, SubText);
            SetAlign(progressLabel, TextAlignmentOptions.Center, Gold);
            if (progressLabel != null)
            {
                progressLabel.enableWordWrapping = false;
                progressLabel.overflowMode = TextOverflowModes.Overflow;
                var ple = progressLabel.GetComponent<LayoutElement>();
                if (ple != null)
                {
                    ple.minWidth = 70;
                    ple.preferredWidth = 70;
                }
            }
            SetAlign(upgradeButtonText, TextAlignmentOptions.Center, Color.white);

            // 진행 표시를 우측 열(비용/버튼 영역)로 이동 — 중앙 효과 줄 겹침 방지.
            // 비용 텍스트와 같은 부모(Right 컬럼)의 비용 바로 다음 위치로 재배치.
            if (progressRoot != null && costText != null && costText.transform.parent != null)
            {
                progressRoot.transform.SetParent(costText.transform.parent, false);
                progressRoot.transform.SetSiblingIndex(costText.transform.GetSiblingIndex() + 1);
            }
        }

        private static void SetAlign(TMP_Text t, TextAlignmentOptions align, Color color)
        {
            if (t == null)
                return;
            t.alignment = align;
            t.color = color;
        }

        // MCP create 가 Image.type=Filled 를 적용 못 해 fillAmount 가 무시되는 문제 보정.
        private static void MakeHorizontalFill(Image img)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        public void Init(
            FacilityType type,
            Color badgeColor,
            string abbreviation,
            Action<FacilityType> onUpgradeClicked
        )
        {
            _type = type;
            _onUpgradeClicked = onUpgradeClicked;

            if (iconBadge != null)
                iconBadge.color = badgeColor;
            if (iconLabel != null)
                iconLabel.text = abbreviation;

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
            string buttonLabel,
            bool canUpgrade,
            bool pending,
            float progressPercent,
            string progressText,
            string tooltipText
        )
        {
            if (nameText != null)
                nameText.text = displayName;
            if (levelText != null)
                levelText.text = $"Lv {currentLevel} / {maxLevel}";

            // 등급 바 — currentLevel / maxLevel 만큼 채움.
            if (gradeFill != null)
                gradeFill.fillAmount = maxLevel > 0 ? (float)currentLevel / maxLevel : 0f;

            if (currentEffectText != null)
                currentEffectText.text = currentEffect;
            if (nextEffectText != null)
            {
                bool atMax = currentLevel >= maxLevel;
                nextEffectText.text = nextEffect;
                nextEffectText.color = atMax ? DimColor : ImproveColor;
            }

            if (costText != null)
                costText.text = costLabel;
            if (upgradeButtonText != null)
                upgradeButtonText.text = buttonLabel;
            if (upgradeButton != null)
                upgradeButton.interactable = canUpgrade;

            // 우측 열: 진행 중이면 [진행 바 + 완료 D-N], 평소엔 [비용].
            if (costText != null)
                costText.gameObject.SetActive(!pending);
            if (progressRoot != null)
                progressRoot.SetActive(pending);
            if (pending && upgradeFill != null)
                upgradeFill.fillAmount = Mathf.Clamp01(progressPercent / 100f);
            if (progressLabel != null)
                progressLabel.text = progressText;

            if (tooltip != null)
                tooltip.description = tooltipText;
        }
    }
}
