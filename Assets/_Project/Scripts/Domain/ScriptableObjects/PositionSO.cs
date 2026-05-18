// PositionSO.cs
// 포지션 정의 + PlayerGenerator(Task 6.1) 가 사용할 키 스탯 카테고리 플래그
// + 2차 포지션 affinity 데이터 (design-decisions.md #26).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "Position", menuName = "FM-Lite/Position")]
    public class PositionSO : ScriptableObject
    {
        public int id;
        public Position position;
        public string displayName;

        [Header("Stat Emphasis (PlayerGenerator weighting)")]
        public bool isGoalkeeper;
        public bool emphasizesTechnical = true;
        public bool emphasizesMental = true;
        public bool emphasizesPhysical = true;

        [Header("Secondary Position Affinity (algorithms.md #1 5단계)")]
        // 이 포지션의 1차 선수가 어떤 2차 포지션을 받기 쉬운지 가중치. 비어있으면 fallback 적용.
        public List<PositionAffinity> affinities = new List<PositionAffinity>();
        // affinities 에 없는 포지션의 기본 weight. 의도된 affinity 가 없어도 2~3% 확률로 뚫림.
        public float fallbackAffinityWeight = 0.05f;
    }

    [Serializable]
    public class PositionAffinity
    {
        public Position position;
        public float weight;        // 1.0 ~ 10.0 권장 (fallback 0.05 대비)
    }
}
