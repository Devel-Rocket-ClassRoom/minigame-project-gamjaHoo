// CrestProvider.cs
// Stage AD (V1.0) — 구단 크레스트 / 시설 아이콘 스프라이트 provider (뷰 레이어 전용).
//
// 설계 근거:
//   · 크레스트는 뷰 레이어 관심사 — Club 도메인에 Sprite 를 넣지 않는다.
//     Club 은 JSON 직렬화 대상 (design-decisions #1/#2) 이고 Sprite 는 직렬화 불가 + 도메인 순수성 위반.
//   · 구단의 안정적 정체성은 name (LeagueConfigSO.clubNames 인덱스 = 명성 내림차순). clubId 는 런타임 할당이라 취약.
//     → 크레스트는 구단 name 으로 매핑한다 (스프라이트 파일명 = 구단명, 예: "Red Devils").
//   · 시설 아이콘은 FacilityType enum 이름으로 매핑 (스프라이트 파일명 = enum 명, 예: "Scout").
//   · 스프라이트 미존재 시 전부 null 폴백 — 호출부는 Apply() 로 Image 를 자동 숨김 (아트 도착 전 빈칸 회피).
//
// 에셋 경로:
//   · 구단 크레스트:  Assets/_Project/Data/Resources/ClubCrests/<구단명>.png
//   · 시설 아이콘:    Assets/_Project/Data/Resources/FacilityIcons/<FacilityType>.png

using System;
using System.Collections.Generic;
using FMLite.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public static class CrestProvider
    {
        public const string ClubCrestFolder = "ClubCrests";
        public const string FacilityIconFolder = "FacilityIcons";

        private static Dictionary<string, Sprite> _clubCrests;
        private static Dictionary<FacilityType, Sprite> _facilityIcons;

        /// <summary>구단명 → 크레스트 스프라이트. 미존재 / 미생성 시 null.</summary>
        public static Sprite GetClubCrest(string clubName)
        {
            EnsureClubCrests();
            if (string.IsNullOrEmpty(clubName))
                return null;
            return _clubCrests.TryGetValue(clubName, out var s) ? s : null;
        }

        /// <summary>시설 종류 → 아이콘 스프라이트. 미존재 / 미생성 시 null.</summary>
        public static Sprite GetFacilityIcon(FacilityType type)
        {
            EnsureFacilityIcons();
            return _facilityIcons.TryGetValue(type, out var s) ? s : null;
        }

        /// <summary>Image 에 스프라이트 적용 — null 이면 컴포넌트 비활성(아트 도착 전 빈칸/흰박스 회피).</summary>
        public static void Apply(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        public static void ApplyClubCrest(Image image, string clubName) =>
            Apply(image, GetClubCrest(clubName));

        public static void ApplyFacilityIcon(Image image, FacilityType type) =>
            Apply(image, GetFacilityIcon(type));

        private static void EnsureClubCrests()
        {
            if (_clubCrests != null)
                return;
            _clubCrests = new Dictionary<string, Sprite>();
            // 폴더 부재 시 빈 배열 — 안전.
            foreach (var s in Resources.LoadAll<Sprite>(ClubCrestFolder))
            {
                if (s != null && !_clubCrests.ContainsKey(s.name))
                    _clubCrests[s.name] = s;
            }
        }

        private static void EnsureFacilityIcons()
        {
            if (_facilityIcons != null)
                return;
            _facilityIcons = new Dictionary<FacilityType, Sprite>();
            foreach (var s in Resources.LoadAll<Sprite>(FacilityIconFolder))
            {
                if (s != null && Enum.TryParse<FacilityType>(s.name, out var type))
                    _facilityIcons[type] = s;
            }
        }

        /// <summary>테스트 / 도메인 리로드용 캐시 초기화.</summary>
        public static void Clear()
        {
            _clubCrests = null;
            _facilityIcons = null;
        }
    }
}
