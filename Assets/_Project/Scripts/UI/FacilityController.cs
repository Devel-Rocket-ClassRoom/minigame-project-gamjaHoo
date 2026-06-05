// FacilityController.cs
// 시설 화면 — 8 시설 카드 동적 생성. Scout/Training/Youth(3)/Medical/Stadium/Gym.
// V1.0 L.1/L.2 (#526): MUIP 카드 재작업 — 색상 배지 + 등급 바 + 업그레이드 ProgressBar
// + 현재→다음 효과 비교 + Tooltip. (V0.5 D.5 동적 생성 모델 유지, design-decisions #49)

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class FacilityController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text moneyText;

        [Header("Row 동적 생성")]
        [SerializeField]
        private FacilityRowController rowPrefab;

        [SerializeField]
        private Transform rowContainer;

        // 표시 순서 — Scout / Training / Youth (3종) / Medical / Stadium / Gym
        private static readonly FacilityType[] DisplayOrder = new[]
        {
            FacilityType.Scout,
            FacilityType.Training,
            FacilityType.YouthCoach,
            FacilityType.YouthRecruitment,
            FacilityType.YouthFacility,
            FacilityType.Medical,
            FacilityType.Stadium,
            FacilityType.Gym,
        };

        private GameState _state;
        private Club _userClub;
        private readonly List<FacilityRowController> _rows = new List<FacilityRowController>();

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;
            _userClub = GameManager.Instance.UserClub;
            if (_userClub == null)
                return;

            if (titleText != null)
            {
                titleText.text = Localization.Get("facility_scene_title");
                titleText.alignment = TMPro.TextAlignmentOptions.Left;
                titleText.color = Color.white;
            }
            if (moneyText != null)
            {
                moneyText.alignment = TMPro.TextAlignmentOptions.Right;
                moneyText.color = new Color(0.290f, 0.565f, 0.851f, 1f); // #4A90D9
            }

            BuildRows();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<FacilityUpgradeStartedEvent>(OnUpgradeStarted);
            EventBus.Subscribe<FacilityUpgradeCompletedEvent>(OnUpgradeCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FacilityUpgradeStartedEvent>(OnUpgradeStarted);
            EventBus.Unsubscribe<FacilityUpgradeCompletedEvent>(OnUpgradeCompleted);
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void BuildRows()
        {
            if (rowPrefab == null || rowContainer == null)
            {
                Debug.LogWarning("[FacilityController] rowPrefab 또는 rowContainer 미지정");
                return;
            }

            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }
            _rows.Clear();

            foreach (var type in DisplayOrder)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.Init(type, BadgeColor(type), Abbrev(type), OnUpgradeClicked);
                _rows.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            if (rowContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void OnUpgradeClicked(FacilityType type)
        {
            try
            {
                FacilitySystem.StartUpgrade(_userClub, type, _state, GameDatabase.GameBalance);
                Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FacilityController] {type} 업그레이드 실패: {e.Message}");
            }
        }

        private void OnUpgradeStarted(FacilityUpgradeStartedEvent e) => Refresh();

        private void OnUpgradeCompleted(FacilityUpgradeCompletedEvent e) => Refresh();

        private void Refresh()
        {
            var balance = GameDatabase.GameBalance;
            var f = _userClub.facilities;

            if (moneyText != null)
                moneyText.text = Localization.Get(
                    "facility_money_fmt",
                    FMLite.Utils.CurrencyFormatter.Format(
                        _userClub.finance.money,
                        FMLite.Application.OptionsManager.Currency
                    )
                );

            int maxLevel = balance != null ? balance.maxFacilityLevel : 10;

            foreach (var row in _rows)
                RefreshRow(row, f, maxLevel);
        }

        private void RefreshRow(FacilityRowController row, Facilities f, int maxLevel)
        {
            var type = row.Type;
            int currentLevel = FacilitySystem.GetLevel(f, type);
            bool atMax = currentLevel >= maxLevel;
            var activeUpgrade = f.activeUpgrades.Find(u => u.type == type);
            bool pending = activeUpgrade != null;

            var currentSo = GameDatabase.GetFacilityLevel(type, currentLevel);
            var nextSo = atMax ? null : GameDatabase.GetFacilityLevel(type, currentLevel + 1);

            string currentEffect = FacilityEffectFormatter.FormatEffect(type, currentSo);
            string nextEffect = atMax
                ? Localization.Get("max_level")
                : "→ " + FacilityEffectFormatter.FormatEffect(type, nextSo);

            string costLabel;
            string buttonLabel;
            if (atMax)
            {
                costLabel = Localization.Get("max_level");
                buttonLabel = Localization.Get("max_level");
            }
            else if (pending)
            {
                costLabel = "-";
                buttonLabel = Localization.Get("facility_upgrade_inprogress");
            }
            else if (nextSo == null)
            {
                costLabel = "-";
                buttonLabel = Localization.Get("facility_upgrade_button");
            }
            else
            {
                costLabel = Localization.Get(
                    "facility_upgrade_cost_fmt",
                    FMLite.Utils.CurrencyFormatter.Format(
                        nextSo.upgradeCost,
                        FMLite.Application.OptionsManager.Currency
                    ),
                    nextSo.upgradeDurationDays
                );
                buttonLabel = Localization.Get("facility_upgrade_button");
            }

            bool canUpgrade =
                !atMax
                && !pending
                && nextSo != null
                && _userClub.finance.money >= nextSo.upgradeCost;

            // 진행률 계산: 시작일 = 완료일 - 기간(다음 등급 SO).
            float progressPercent = 0f;
            string progressText = string.Empty;
            if (pending)
            {
                int durationDays = nextSo != null ? Math.Max(1, nextSo.upgradeDurationDays) : 1;
                DateTime completion = activeUpgrade.completionDate;
                DateTime start = completion.AddDays(-durationDays);
                double elapsed = (_state.currentDate - start).TotalDays;
                progressPercent = Mathf.Clamp01((float)(elapsed / durationDays)) * 100f;

                int daysLeft = (int)Math.Ceiling((completion - _state.currentDate).TotalDays);
                progressText =
                    daysLeft <= 0
                        ? Localization.Get("facility_progress_today")
                        : Localization.Get("facility_progress_dday_fmt", daysLeft);
            }

            // 툴팁: 현재 → 다음 효과 상세.
            string tooltipText = atMax
                ? $"{FacilityEffectFormatter.GetDisplayName(type)}\n{currentEffect}\n{Localization.Get("max_level")}"
                : $"{FacilityEffectFormatter.GetDisplayName(type)}\n{currentEffect}\n→ {FacilityEffectFormatter.FormatEffect(type, nextSo)}";

            row.Render(
                FacilityEffectFormatter.GetDisplayName(type),
                currentLevel,
                maxLevel,
                currentEffect,
                nextEffect,
                costLabel,
                buttonLabel,
                canUpgrade,
                pending,
                progressPercent,
                progressText,
                tooltipText
            );
        }

        // 시설 카테고리별 배지 색상.
        private static Color BadgeColor(FacilityType type) =>
            type switch
            {
                FacilityType.Scout => new Color(0.204f, 0.596f, 0.859f), // #3498DB
                FacilityType.Training => new Color(0.902f, 0.494f, 0.133f), // #E67E22
                FacilityType.YouthCoach => new Color(0.608f, 0.349f, 0.714f), // #9B59B6
                FacilityType.YouthRecruitment => new Color(0.557f, 0.267f, 0.678f), // #8E44AD
                FacilityType.YouthFacility => new Color(0.424f, 0.361f, 0.906f), // #6C5CE7
                FacilityType.Medical => new Color(0.180f, 0.800f, 0.443f), // #2ECC71
                FacilityType.Stadium => new Color(0.945f, 0.769f, 0.059f), // #F1C40F
                FacilityType.Gym => new Color(0.906f, 0.298f, 0.235f), // #E74C3C
                _ => new Color(0.5f, 0.5f, 0.5f),
            };

        // 배지 약어 (2자).
        private static string Abbrev(FacilityType type) =>
            type switch
            {
                FacilityType.Scout => "스카",
                FacilityType.Training => "훈련",
                FacilityType.YouthCoach => "유코",
                FacilityType.YouthRecruitment => "유모",
                FacilityType.YouthFacility => "유시",
                FacilityType.Medical => "의료",
                FacilityType.Stadium => "경기",
                FacilityType.Gym => "체육",
                _ => "??",
            };
    }
}
