// PositionSO.cs
// 포지션 정의 + PlayerGenerator(Task 6.1) 가 사용할 키 스탯 카테고리 플래그.

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
    }
}
