// Task 13.11 (Issue #56) — 시설 화면.
// Scout / Training / Youth 등급 표시 + 업그레이드 발주.

using System;
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

        [Header("자금")]
        [SerializeField]
        private TMP_Text moneyText;

        [Header("Scout")]
        [SerializeField]
        private TMP_Text scoutLevelText;

        [SerializeField]
        private TMP_Text scoutCostText;

        [SerializeField]
        private Button scoutUpgradeButton;

        [Header("Training")]
        [SerializeField]
        private TMP_Text trainingLevelText;

        [SerializeField]
        private TMP_Text trainingCostText;

        [SerializeField]
        private Button trainingUpgradeButton;

        [Header("Youth")]
        [SerializeField]
        private TMP_Text youthLevelText;

        [SerializeField]
        private TMP_Text youthCostText;

        [SerializeField]
        private Button youthUpgradeButton;

        [Header("업그레이드 진행 현황")]
        [SerializeField]
        private TMP_Text pendingText;

        private GameState _state;
        private Club _userClub;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            _userClub = GameManager.Instance.UserClub;
            if (_userClub == null)
                return;

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

        public void OnScoutUpgradeClicked() => TryUpgrade(FacilityType.Scout);

        public void OnTrainingUpgradeClicked() => TryUpgrade(FacilityType.Training);

        public void OnYouthUpgradeClicked() => TryUpgrade(FacilityType.Youth);

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void TryUpgrade(FacilityType type)
        {
            try
            {
                FacilitySystem.StartUpgrade(_userClub, type, _state, GameDatabase.GameBalance);
                Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FacilityController] 업그레이드 실패: {e.Message}");
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
                    (_userClub.finance.money / 1000000.0).ToString("0.0")
                );

            RefreshRow(
                FacilityType.Scout,
                f.scoutLevel,
                scoutLevelText,
                scoutCostText,
                scoutUpgradeButton,
                balance
            );
            RefreshRow(
                FacilityType.Training,
                f.trainingLevel,
                trainingLevelText,
                trainingCostText,
                trainingUpgradeButton,
                balance
            );
            RefreshRow(
                FacilityType.Youth,
                f.youthLevel,
                youthLevelText,
                youthCostText,
                youthUpgradeButton,
                balance
            );

            if (pendingText != null)
            {
                if (f.hasPendingUpgrade)
                    pendingText.text = Localization.Get(
                        "facility_upgrade_progress_fmt",
                        f.pendingUpgradeType,
                        f.upgradeCompletionDate.ToString("yyyy-MM-dd")
                    );
                else
                    pendingText.text = Localization.Get("no_pending_upgrade");
            }
        }

        private void RefreshRow(
            FacilityType type,
            int currentLevel,
            TMP_Text levelText,
            TMP_Text costText,
            Button upgradeButton,
            GameBalanceSO balance
        )
        {
            if (levelText != null)
                levelText.text = $"Lv {currentLevel}";

            bool maxLevel = balance != null && currentLevel >= balance.maxFacilityLevel;
            bool pending = _userClub.facilities.hasPendingUpgrade;

            if (upgradeButton != null)
                upgradeButton.interactable = !maxLevel && !pending;

            if (costText != null)
            {
                if (maxLevel)
                {
                    costText.text = Localization.Get("max_level");
                }
                else
                {
                    var so = GameDatabase.GetFacilityLevel(type, currentLevel + 1);
                    costText.text =
                        so != null
                            ? Localization.Get(
                                "facility_upgrade_cost_fmt",
                                (so.upgradeCost / 1000000.0).ToString("0.0"),
                                so.upgradeDurationDays
                            )
                            : "-";
                }
            }
        }
    }
}
