// FacilitySystem.cs
// 시설 업그레이드 발주 + 완료 처리. Stateless (design-decisions.md #3).
// D.3: 병렬 업그레이드 지원 — 자금만 있으면 N개 동시 발주 가능.

using System;
using System.Linq;
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

            if (f.activeUpgrades.Any(u => u.type == type))
                throw new InvalidOperationException($"{type} 업그레이드가 이미 진행 중");

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
            var upgrade = new FacilityUpgrade
            {
                type = type,
                completionDate = state.currentDate.AddDays(so.upgradeDurationDays),
            };
            f.activeUpgrades.Add(upgrade);

            EventBus.Publish(
                new FacilityUpgradeStartedEvent
                {
                    type = type,
                    newLevel = currentLevel + 1,
                    completionDate = upgrade.completionDate,
                }
            );
        }

        public static void ProcessUpgrades(GameState state)
        {
            var userClub = state.GetClub(state.userClubId);
            if (userClub == null)
                return;

            var f = userClub.facilities;
            var completed = f.activeUpgrades
                .Where(u => state.currentDate >= u.completionDate)
                .ToList();

            foreach (var u in completed)
            {
                int newLevel = GetLevel(f, u.type) + 1;
                SetLevel(f, u.type, newLevel);
                f.activeUpgrades.Remove(u);
                EventBus.Publish(
                    new FacilityUpgradeCompletedEvent { type = u.type, newLevel = newLevel }
                );
            }
        }

        public static int GetLevel(Facilities f, FacilityType type) =>
            type switch
            {
                FacilityType.Scout => f.scoutLevel,
                FacilityType.Training => f.trainingLevel,
                FacilityType.YouthCoach => f.youthCoachLevel,
                FacilityType.YouthRecruitment => f.youthRecruitmentLevel,
                FacilityType.YouthFacility => f.youthFacilityLevel,
                FacilityType.Medical => f.medicalLevel,
                FacilityType.Stadium => f.stadiumLevel,
                FacilityType.Gym => f.gymLevel,
                _ => 1,
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
                case FacilityType.YouthCoach:
                    f.youthCoachLevel = level;
                    break;
                case FacilityType.YouthRecruitment:
                    f.youthRecruitmentLevel = level;
                    break;
                case FacilityType.YouthFacility:
                    f.youthFacilityLevel = level;
                    break;
                case FacilityType.Medical:
                    f.medicalLevel = level;
                    break;
                case FacilityType.Stadium:
                    f.stadiumLevel = level;
                    break;
                case FacilityType.Gym:
                    f.gymLevel = level;
                    break;
            }
        }
    }
}
