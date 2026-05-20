// CountrySO.cs
// 국가 정보 (코드 / 표시명 / 깃발색). NamePoolSO 가 countryId 로 연결.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "Country", menuName = "FM-Lite/Country")]
    public class CountrySO : ScriptableObject
    {
        public int id;
        public string code; // ISO 3-letter, "ENG", "FRA"
        public string displayName; // "잉글랜드"
        public Color flagPrimaryColor = Color.white;
        public Color flagSecondaryColor = Color.black;
    }
}
