// FacilitySystem.cs
// 시설 업그레이드 발주 + 완료 처리. Stateless (design-decisions.md #3).
// V0.1: 유저 구단만 업그레이드 가능. AI 구단 업그레이드는 V1.0+.

using System;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class FacilitySystem
    {
        public static void StartUpgrade(
            Club club,
            FacilityType type,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (club == null)
                throw new ArgumentNullException(nameof(club));

            var f = club.facilities;
            if (f.hasPendingUpgrade)
                throw new InvalidOperationException("이미 업그레이드 진행 중");

            int currentLevel = GetLevel(f, type);
            if (currentLevel >= balance.maxFacilityLevel)
                throw new InvalidOperationException("이미 최고 등급");

            var so = GameDatabase.GetFacilityLevel(type, currentLevel + 1);
            if (so == null)
                throw new InvalidOperationException(
                    $"FacilityLevelSO 없음: {type} Lv{currentLevel + 1}"
                );

            if (club.finance.money < so.upgradeCost)
                throw new InvalidOperationException("자금 부족");

            club.finance.money -= so.upgradeCost;
            f.hasPendingUpgrade = true;
            f.pendingUpgradeType = type;
            f.upgradeCompletionDate = state.currentDate.AddDays(so.upgradeDurationDays);

            EventBus.Publish(
                new FacilityUpgradeStartedEvent
                {
                    type = type,
                    newLevel = currentLevel + 1,
                    completionDate = f.upgradeCompletionDate,
                }
            );
        }

        public static void ProcessUpgrades(GameState state)
        {
            var userClub = state.GetClub(state.userClubId);
            if (userClub == null)
                return;

            var f = userClub.facilities;
            if (!f.hasPendingUpgrade)
                return;
            if (state.currentDate < f.upgradeCompletionDate)
                return;

            int newLevel = GetLevel(f, f.pendingUpgradeType) + 1;
            SetLevel(f, f.pendingUpgradeType, newLevel);
            var completedType = f.pendingUpgradeType;
            f.hasPendingUpgrade = false;

            EventBus.Publish(
                new FacilityUpgradeCompletedEvent { type = completedType, newLevel = newLevel }
            );
        }

        public static int GetLevel(Facilities f, FacilityType type) =>
            type switch
            {
                FacilityType.Scout => f.scoutLevel,
                FacilityType.Training => f.trainingLevel,
                FacilityType.Youth => f.youthLevel,
                _ => 0,
            };

        private static void SetLevel(Facilities f, FacilityType type, int level)
        {
            switch (type)
            {
                case FacilityType.Scout:
                    f.scoutLevel = level;
                    break;
                case FacilityType.Training:
                    f.trainingLevel = level;
                    break;
                case FacilityType.Youth:
                    f.youthLevel = level;
                    break;
            }
        }
    }
}
