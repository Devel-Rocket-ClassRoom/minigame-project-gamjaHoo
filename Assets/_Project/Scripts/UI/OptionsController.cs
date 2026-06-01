// OptionsController.cs
// Stage X (V1.0) — OptionsScene UI 컨트롤러. OptionsManager(PlayerPrefs) 연동.
// 사운드 슬라이더 3 / 언어·통화·UIScale HorizontalSelector / 자동저장 Switch / 단축키 모달 / 뒤로·저장.
// prefab / 인스펙터 와이어링은 Sub-C. selector index↔값 매핑은 순수 static 헬퍼 (테스트 대상).
//
// AudioMixer(MasterMixer.mixer)는 Stage Y.1 — SoundManager silent fallback 라 미배치여도 무해.
// OptionsScene 은 §3.19.4 예외 (TopBar 만, SideBar 없음) — GlobalNav 미포함.

using FMLite.Application;
using FMLite.Domain;
using FMLite.Utils;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class OptionsController : MonoBehaviour
    {
        internal const string PreviousSceneKey = "PreviousScene";

        // UI Scale 4단계 (§3.20.4). index 가 uiScaleSelector 와 1:1.
        public static readonly int[] UiScaleValues = { 90, 100, 110, 125 };

        [Header("사운드 슬라이더")]
        [SerializeField]
        private SliderManager masterSlider;

        [SerializeField]
        private SliderManager sfxSlider;

        [SerializeField]
        private SliderManager bgmSlider;

        [Header("HorizontalSelector")]
        [SerializeField]
        private HorizontalSelector languageSelector; // KO / EN

        [SerializeField]
        private HorizontalSelector currencySelector; // £ / $ / € / ₩

        [SerializeField]
        private HorizontalSelector uiScaleSelector; // 90 / 100 / 110 / 125

        [Header("자동 저장 / 단축키")]
        [SerializeField]
        private SwitchManager autoSaveSwitch;

        [SerializeField]
        private Button shortcutButton;

        [SerializeField]
        private GameObject shortcutModal;

        [SerializeField]
        private Button shortcutCloseButton;

        [Header("TopBar")]
        [SerializeField]
        private Button backButton;

        [SerializeField]
        private Button saveButton;

        private void Start()
        {
            OptionsManager.Initialize(); // PlayerPrefs 로드 (idempotent)
            SetupSelectors();
            LoadValuesIntoUI();
            WireEvents();
            if (shortcutModal != null)
                shortcutModal.SetActive(false);
        }

        // ── 셀렉터 항목 구성 ─────────────────────────────────────────

        private void SetupSelectors()
        {
            if (languageSelector != null)
            {
                languageSelector.itemList.Clear();
                languageSelector.CreateNewItem("한국어");
                languageSelector.CreateNewItem("English");
                languageSelector.defaultIndex = LanguageEnumToIndex(OptionsManager.Language);
                languageSelector.SetupSelector();
            }
            if (currencySelector != null)
            {
                currencySelector.itemList.Clear();
                foreach (var sym in new[] { "£", "$", "€", "₩" })
                    currencySelector.CreateNewItem(sym);
                currencySelector.defaultIndex = CurrencyEnumToIndex(OptionsManager.Currency);
                currencySelector.SetupSelector();
            }
            if (uiScaleSelector != null)
            {
                uiScaleSelector.itemList.Clear();
                foreach (var v in UiScaleValues)
                    uiScaleSelector.CreateNewItem($"{v}%");
                uiScaleSelector.defaultIndex = UiScaleValueToIndex(OptionsManager.UiScale);
                uiScaleSelector.SetupSelector();
            }
        }

        private void LoadValuesIntoUI()
        {
            SetSlider(masterSlider, OptionsManager.MasterVolume);
            SetSlider(sfxSlider, OptionsManager.SfxVolume);
            SetSlider(bgmSlider, OptionsManager.BgmVolume);
            if (autoSaveSwitch != null)
            {
                autoSaveSwitch.isOn = OptionsManager.AutoSave;
                autoSaveSwitch.UpdateUI();
            }
        }

        private static void SetSlider(SliderManager s, float v)
        {
            if (s == null || s.mainSlider == null)
                return;
            s.mainSlider.value = v;
            s.UpdateUI();
        }

        // ── 이벤트 와이어링 ──────────────────────────────────────────

        private void WireEvents()
        {
            if (masterSlider?.mainSlider != null)
                masterSlider.mainSlider.onValueChanged.AddListener(OnMasterChanged);
            if (sfxSlider?.mainSlider != null)
                sfxSlider.mainSlider.onValueChanged.AddListener(OnSfxChanged);
            if (bgmSlider?.mainSlider != null)
                bgmSlider.mainSlider.onValueChanged.AddListener(OnBgmChanged);
            if (languageSelector != null)
                languageSelector.onValueChanged.AddListener(OnLanguageChanged);
            if (currencySelector != null)
                currencySelector.onValueChanged.AddListener(OnCurrencyChanged);
            if (uiScaleSelector != null)
                uiScaleSelector.onValueChanged.AddListener(OnUiScaleChanged);
            if (autoSaveSwitch != null)
            {
                // MUIP SwitchManager 는 OnEvents / OffEvents (UnityEvent) 분리 — bool 콜백 없음.
                autoSaveSwitch.OnEvents.AddListener(() => OnAutoSaveChanged(true));
                autoSaveSwitch.OffEvents.AddListener(() => OnAutoSaveChanged(false));
            }

            WireButton(shortcutButton, () => shortcutModal?.SetActive(true));
            WireButton(shortcutCloseButton, () => shortcutModal?.SetActive(false));
            WireButton(saveButton, OptionsManager.Save);
            WireButton(backButton, OnBackClicked);
        }

        private static void WireButton(Button b, UnityEngine.Events.UnityAction h)
        {
            if (b == null)
                return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(h);
        }

        // ── 핸들러 ───────────────────────────────────────────────────

        private void OnMasterChanged(float v)
        {
            OptionsManager.MasterVolume = v;
            SoundManager.Instance?.SetMixerVolume(SoundManager.MasterParam, v);
        }

        private void OnSfxChanged(float v)
        {
            OptionsManager.SfxVolume = v;
            SoundManager.Instance?.SetMixerVolume(SoundManager.SfxParam, v);
        }

        private void OnBgmChanged(float v)
        {
            OptionsManager.BgmVolume = v;
            SoundManager.Instance?.SetMixerVolume(SoundManager.BgmParam, v);
        }

        private void OnLanguageChanged(int index)
        {
            var lang = LanguageIndexToEnum(index);
            OptionsManager.Language = lang;
            LocalizationSystem.SetLanguage(lang);
        }

        private void OnCurrencyChanged(int index)
        {
            OptionsManager.Currency = CurrencyIndexToEnum(index);
            GlobalNavController.Instance?.RefreshMoney(); // TopBar 자금 즉시 재계산
        }

        private void OnUiScaleChanged(int index)
        {
            float pct = UiScaleIndexToValue(index);
            OptionsManager.UiScale = pct;
            ApplyUiScale(pct);
        }

        private void OnAutoSaveChanged(bool on) => OptionsManager.AutoSave = on;

        private void OnBackClicked()
        {
            string prev = PlayerPrefs.GetString(PreviousSceneKey, "DashboardScene");
            if (string.IsNullOrEmpty(prev) || prev == SceneManager.GetActiveScene().name)
                prev = "DashboardScene";
            SceneManager.LoadScene(prev);
        }

        /// <summary>UI Scale 적용 — ScaleWithScreenSize 캔버스의 referenceResolution 을 역비례 조정 (작을수록 UI 큼).</summary>
        public static void ApplyUiScale(float pct)
        {
            float factor = Mathf.Max(0.1f, pct / 100f);
            var baseRes = new Vector2(1920f, 1080f);
            var scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None);
            foreach (var sc in scalers)
            {
                if (sc.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    sc.referenceResolution = baseRes / factor;
            }
        }

        // ── 순수 매핑 헬퍼 (테스트 대상) ─────────────────────────────

        public static int UiScaleValueToIndex(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            for (int i = 0; i < UiScaleValues.Length; i++)
                if (UiScaleValues[i] == rounded)
                    return i;
            return 1; // default 100%
        }

        public static float UiScaleIndexToValue(int index) =>
            UiScaleValues[Mathf.Clamp(index, 0, UiScaleValues.Length - 1)];

        public static Currency CurrencyIndexToEnum(int index) =>
            (Currency)Mathf.Clamp(index, 0, 3);

        public static int CurrencyEnumToIndex(Currency c) => (int)c;

        public static Language LanguageIndexToEnum(int index) =>
            index == 0 ? Language.Korean : Language.English;

        public static int LanguageEnumToIndex(Language lang) =>
            lang == Language.Korean ? 0 : 1;
    }
}
