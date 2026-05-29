// FacilityController.cs
// V0.5 D.5 — 동적 row 생성. 8 시설 × FacilityRowPrefab 인스턴스화.
// (V0.1 = 3 시설 SerializeField 폐기, design-decisions.md #49 / v0.5-plan.md §3.10.6)

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

        [Header("자금")]
        [SerializeField]
        private TMP_Text moneyText;

        [Header("Row 동적 생성")]
        [SerializeField]
        private FacilityRowController rowPrefab;

        [SerializeField]
        private Transform rowContainer;

        [Header("업그레이드 진행 현황")]
        [SerializeField]
        private TMP_Text pendingText;

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

            // 기존 row 정리 (재진입 안전)
            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }
            _rows.Clear();

            foreach (var type in DisplayOrder)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.Init(type, OnUpgradeClicked);
                _rows.Add(row);
            }

            // 동적 Instantiate 후 LayoutGroup dirty flag 강제 갱신 —
            // VerticalLayoutGroup + ContentSizeFitter 가 같은 프레임에 자식 크기 합산 못 하는 경우 대응.
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
                    (_userClub.finance.money / 1000000.0).ToString("0.0")
                );

            int maxLevel = balance != null ? balance.maxFacilityLevel : 10;

            foreach (var row in _rows)
                RefreshRow(row, f, maxLevel);

            RefreshPending(f);
        }

        private void RefreshRow(FacilityRowController row, Facilities f, int maxLevel)
        {
            var type = row.Type;
            int currentLevel = FacilitySystem.GetLevel(f, type);
            bool atMax = currentLevel >= maxLevel;
            bool pending = f.activeUpgrades.Exists(u => u.type == type);

            var currentSo = GameDatabase.GetFacilityLevel(type, currentLevel);
            var nextSo = atMax ? null : GameDatabase.GetFacilityLevel(type, currentLevel + 1);

            string currentEffect = FacilityEffectFormatter.FormatEffect(type, currentSo);
            string nextEffect = atMax
                ? Localization.Get("max_level")
                : FacilityEffectFormatter.FormatEffect(type, nextSo);

            string costLabel;
            if (atMax)
                costLabel = Localization.Get("max_level");
            else if (nextSo == null)
                costLabel = "-";
            else
                costLabel = Localization.Get(
                    "facility_upgrade_cost_fmt",
                    (nextSo.upgradeCost / 1000000.0).ToString("0.0"),
                    nextSo.upgradeDurationDays
                );

            bool canUpgrade = !atMax
                && !pending
                && nextSo != null
                && _userClub.finance.money >= nextSo.upgradeCost;

            row.Render(
                FacilityEffectFormatter.GetDisplayName(type),
                currentLevel,
                maxLevel,
                currentEffect,
                nextEffect,
                costLabel,
                canUpgrade
            );
        }

        private void RefreshPending(Facilities f)
        {
            if (pendingText == null)
                return;

            if (f.activeUpgrades.Count == 0)
            {
                pendingText.text = Localization.Get("no_pending_upgrade");
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < f.activeUpgrades.Count; i++)
            {
                if (i > 0)
                    sb.AppendLine();
                var u = f.activeUpgrades[i];
                sb.Append(
                    Localization.Get(
                        "facility_upgrade_progress_fmt",
                        FacilityEffectFormatter.GetDisplayName(u.type),
                        u.completionDate.ToString("yyyy-MM-dd")
                    )
                );
            }
            pendingText.text = sb.ToString();
        }
    }
}
