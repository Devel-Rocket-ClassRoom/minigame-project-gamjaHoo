// LocalizedText.cs
// V1.0 (#467) — 하드코딩 정적 라벨 제거용 재사용 컴포넌트.
// TMP_Text 에 부착 + 인스펙터에서 key 지정 → Start 에서 Localization.Get(key) 로 텍스트 세팅.
//
// Start 사용 이유: 모든 Awake (DebugBootstrap 의 LocalizationSystem.Initialize 포함) 이후
// 실행이 보장돼 CurrentLanguage 가 확정된 뒤 적용됨. 씬 로드(언어 전환 후 복귀)마다 재적용.
//
// MUIP ButtonManager 버튼 라벨도 지원: 부모에 ButtonManager 가 있으면 buttonText(소스)+양쪽
// 텍스트를 세팅해 Start 의 UpdateUI 가 덮어써도 유지. 동적 텍스트(컨트롤러가 직접 세팅)엔 부착 X.

using FMLite.Application;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("LocalizationData 의 키. 비우면 아무 동작 안 함.")]
        private string key;

        private TMP_Text _text;

        private void Start() => Apply();

        /// <summary>현재 언어로 텍스트 재적용. 런타임 언어 전환 후 수동 호출 가능.</summary>
        public void Apply()
        {
            if (string.IsNullOrEmpty(key))
                return;
            string value = Localization.Get(key);

            // MUIP 버튼 라벨이면 buttonText(소스)+양쪽 텍스트 세팅 (UpdateUI 덮어쓰기 방지).
            var bm = GetComponentInParent<ButtonManager>();
            if (bm != null)
            {
                bm.buttonText = value;
                if (bm.normalText != null)
                    bm.normalText.text = value;
                if (bm.highlightedText != null)
                    bm.highlightedText.text = value;
                return;
            }

            if (_text == null)
                _text = GetComponent<TMP_Text>();
            if (_text != null)
                _text.text = value;
        }

        /// <summary>키 변경 + 즉시 적용.</summary>
        public void SetKey(string newKey)
        {
            key = newKey;
            Apply();
        }
    }
}
