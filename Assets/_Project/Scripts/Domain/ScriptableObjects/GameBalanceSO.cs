// GameBalanceSO.cs
// V0.1 밸런싱 수치 외부화 (design-decisions.md #11). 알고리즘 명세 진행에 따라 필드 점진 추가.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "FM-Lite/Game Balance")]
    public class GameBalanceSO : ScriptableObject
    {
        [Header("Debug")]
        public bool isDebugMode = true;

        [Header("Player Generation")]
        public int minCA = 30;
        public int maxCA = 200;
        public int minPA = 50;
        public int maxPA = 200;
        public float traitProbabilityPerPlayer = 0.30f;

        [Header("Starting Squad Gacha")]
        public int initialRerollTokens = 3;
        public int maxRerollStockpile = 5;
        // 5단계 티어 (Elite / Strong / Average / Weak / Poor) 누적 분포 임계점.
        // 예: {0.10, 0.40, 0.80, 0.95} → Elite ≤10%, Strong 10~40%, Average 40~80%, Weak 80~95%, Poor 95~100%
        public float[] tierThresholdsAccumulated = new[] { 0.10f, 0.40f, 0.80f, 0.95f };

        [Header("Match Simulation")]
        public float avgGoalsPerMatch = 2.70f;
        public float homeAdvantageBonus = 0.10f;

        [Header("Daily / Season")]
        public int fatigueRecoveryPerDay = 15;
        public int fatigueGainPerMatch = 30;
        public int retirementMinAge = 33;
        public float retirementProbabilityPerYear = 0.15f;
        public int seasonRerollTokenGrant = 3;

        [Header("Transfer")]
        public float marketValueAgeFactor = 1.0f;
        public float marketValueCAFactor = 100f;
    }
}
