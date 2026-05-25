// PlayerRoleSO.cs
// 선수 역할(Role) 카탈로그 에셋 (design-decisions.md #45).
// 포지션별 3-4개 Role × ~40 인스턴스.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    // 매치 이벤트 가중치 보정 (Tactic.TacticSlot.roleId 참조 시 적용)
    [Serializable]
    public class MatchEventModifier
    {
        public string eventType; // "shot" / "keyPass" / "tackle" 등
        public float multiplier; // 1.0 = 기본, >1 = 빈도 ↑
    }

    [CreateAssetMenu(fileName = "PlayerRole", menuName = "FM-Lite/PlayerRoleSO")]
    public class PlayerRoleSO : ScriptableObject
    {
        public int id;
        public string displayName;
        public List<Position> compatiblePositions = new List<Position>();
        public Duty defaultDuty;
        public List<MatchEventModifier> eventModifiers = new List<MatchEventModifier>();
    }
}
