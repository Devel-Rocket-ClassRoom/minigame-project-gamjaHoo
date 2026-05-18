// NamePoolSO.cs
// 국가별 이름 풀. PlayerGenerator 가 국적에 맞춰 랜덤 추출.

using System.Collections.Generic;
using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "NamePool", menuName = "FM-Lite/Name Pool")]
    public class NamePoolSO : ScriptableObject
    {
        public int countryId;
        public List<string> firstNames = new List<string>();
        public List<string> lastNames = new List<string>();
    }
}
