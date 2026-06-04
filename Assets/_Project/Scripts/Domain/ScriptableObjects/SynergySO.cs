// SynergySO.cs
// V1.0 G.3 — 선수 조합 시너지 카탈로그 (algorithms.md V1.0-3 / design-decisions #70 후속, #474/#478).
// Tactic 평가 시 자동 검출 → 활성 시너지의 strengthBonus 가 매치 팀 effective strength 곱셈 보정.
// 명세 보완 (구현 정합): SynergyCondition 에 positions(복수 OR) / minCount / requireSameNationality 추가.
//   - 단일 Position → List (LW/RW·CM/AM 같은 "둘 중 하나" 표현).
//   - statRequirement = "fieldPath op value" 를 '&' 로 여러 개 AND (예 "technical.passing>=80 & mental.vision>=75").
//     특수 토큰: height / weight / weakFoot → Player.physical (PhysicalAttributes). 그 외 → StatCatalog fieldPath.
//   - minCount = 조건 충족 선수 최소 수 ("2명" 표현).
//   - requireSameNationality = positions 후보 중 같은 nationalityCode 가 minCount 명 (자국인 라인).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    [Serializable]
    public class SynergyCondition
    {
        public List<Position> positions = new List<Position>(); // any-of. 빈 = 전체 후보
        public string statRequirement = ""; // "fieldPath op value" '&' AND. 빈 = stat 무관
        public string roleRequirement = ""; // PlayerRoleSO.displayName 부분일치. 빈 = 무관
        public int minCount = 1; // 조건 충족 선수 최소 수
        public bool requireSameNationality = false; // positions 후보 중 같은 국적 minCount 명
    }

    [CreateAssetMenu(fileName = "Synergy", menuName = "FM-Lite/SynergySO")]
    public class SynergySO : ScriptableObject
    {
        public int id;
        public string nameKey; // Localization 키
        public string descriptionKey;
        public List<SynergyCondition> conditions = new List<SynergyCondition>();
        public float strengthBonus = 1.05f; // 활성 시 팀 strength 곱셈 (5% 기본)
    }
}
