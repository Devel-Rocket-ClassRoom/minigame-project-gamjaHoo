// SoundManager.cs
// V1.0 Stage Y — AudioMixer 어댑터 + BGM(라디오)/매치 앰비언스/SFX 재생.
// MonoBehaviour 싱글톤 (DontDestroyOnLoad). RuntimeInitializeOnLoadMethod 로 자동 부트스트랩
// (Resources/SoundManager.prefab). 씬에 수동 배치 불필요.
//
// 오디오 토폴로지 (끊김 방지 — 사용자 명시 요구):
//   bgmSource     (loop)        → Mixer[BGM]  메인메뉴 곡 / 라디오 3곡
//   ambientSources[] (loop)     → Mixer[SFX]  매치 씬 관중 함성 (각 클립 전용 소스, 레이어)
//   sfxSource     (PlayOneShot) → Mixer[SFX]  일회성 효과음 (휘슬/카드/골…)
//   → 일회성 SFX 는 전부 PlayOneShot (겹쳐 믹스). 앰비언스/BGM 과 물리 분리된 소스라
//     효과음이 몇 개 터지든 관중 함성 루프는 끊기지 않는다.
//
// BGM 모델 (사용자 결정):
//   메뉴 단계(MainMenu/ClubSelect/Gacha) → 메인메뉴 BGM 유지 (팀 선택·리롤까지 안 바뀜).
//   OptionsScene   → 중립 (직전 BGM 유지 — 메뉴에서 들어오면 메뉴곡, 게임 중이면 라디오).
//   게임 진입(가챠 종료 → DashboardScene)부터 → 라디오식 연속 재생 (곡 끝나면 다음 곡).
//   MatchTextScene → BGM 끄고(pause) 관중 앰비언스만.
//   씬 구동은 SceneManager.sceneLoaded 중앙 구독 (per-씬 컴포넌트 배선 없음).
//
// Volume 변환: 0-100 슬라이더 → AudioMixer dB. dB = 20 × log10(v / 100).

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace FMLite.Application
{
    public class SoundManager : MonoBehaviour
    {
        public const string MasterParam = "MasterVolume";
        public const string SfxParam = "SfxVolume";
        public const string BgmParam = "BgmVolume";
        public const float MuteDb = -80f;

        private const string MatchSceneName = "MatchTextScene";
        private const string OptionsSceneName = "OptionsScene";

        // 메뉴 단계 = 메인메뉴 → 구단 선택 → 가챠(리롤). 이 동안 메인메뉴 BGM 유지.
        private static readonly string[] MenuSceneNames =
        {
            "MainMenuScene",
            "ClubSelectScene",
            "GachaScene",
        };
        private const string BgmGroupName = "BGM";
        private const string SfxGroupName = "SFX";
        private const float CrossfadeSeconds = 1.0f; // 씬 전환 BGM 크로스페이드 (오버랩)
        private const float GoalCrowdSeconds = 1.5f; // 골 환호 길이 + 페이드아웃

        public static SoundManager Instance { get; private set; }

        [SerializeField]
        private AudioMixer mixer;

        [Header("BGM")]
        [SerializeField]
        private AudioClip mainMenuBgm;

        [SerializeField]
        private AudioClip[] radioPlaylist; // 비메뉴 씬 라디오 (3곡)

        [Header("Ambient (매치 관중 함성 — 전부 동등, 레이어)")]
        [SerializeField]
        private AudioClip[] ambientClips;

        [Header("SFX (SfxId 순서. Goal 슬롯은 미사용 — net/crowd 별도)")]
        [SerializeField]
        private AudioClip[] sfxClips;

        [SerializeField]
        private AudioClip goalNetClip;

        [SerializeField]
        private AudioClip goalCrowdClip;

        // 런타임 생성 소스 (인스펙터 배선 불필요).
        private AudioSource bgmA; // BGM 크로스페이드용 2소스 — 서로 오버랩하며 전환
        private AudioSource bgmB;
        private AudioSource _activeBgm; // 현재 메인으로 재생 중인 BGM 소스
        private AudioSource sfxSource;
        private AudioSource goalCrowdSource; // 골 환호 — 길이/페이드아웃 제어 위해 전용
        private AudioSource[] ambientSources;

        private enum AudioMode
        {
            None,
            Menu,
            Radio,
            Match,
        }

        private AudioMode _mode = AudioMode.None;
        private int _radioIndex;
        private bool _crossfading;
        private Coroutine _bgmFade;
        private Coroutine _goalFade;
        private AudioListener _listener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;
            var prefab = Resources.Load<SoundManager>("SoundManager");
            if (prefab != null)
                Instantiate(prefab);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (!UnityEngine.Application.isPlaying)
                return; // EditMode 테스트: 소스 생성/씬 구독 생략 (silent fallback).

            DontDestroyOnLoad(gameObject);
            SetupSources();
            OptionsManager.EnsureInitialized();
            ApplyOptionsVolume();
            SceneManager.sceneLoaded += OnSceneLoaded;
            // 부트스트랩이 BeforeSceneLoad 라 보통 첫 씬 sceneLoaded 를 잡지만,
            // 이미 로드된 씬에 직접 배치된 경우도 대비해 현재 씬을 즉시 반영 (SetMode 재진입 무해).
            var active = SceneManager.GetActiveScene();
            if (active.isLoaded)
                ApplySceneAudio(active.name);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void Update()
        {
            // 라디오: 현재 곡이 끝나면 다음 곡으로 (loop=false 라 자연 종료 감지).
            if (
                _mode == AudioMode.Radio
                && !_crossfading
                && _activeBgm != null
                && _activeBgm.clip != null
                && !_activeBgm.isPlaying
                && radioPlaylist != null
                && radioPlaylist.Length > 0
            )
            {
                PlayRadioTrack(_radioIndex + 1);
            }
        }

        // ── 셋업 ─────────────────────────────────────────────────────

        private void SetupSources()
        {
            // 영속 AudioListener — 일부 씬(MatchText/MatchPreview/TacticLineup/Result 등)에
            // AudioListener 가 없어 무음 되는 문제 방지. 씬 로드마다 다른 리스너는 끄고 이것만 유지.
            _listener = gameObject.GetComponent<AudioListener>();
            if (_listener == null)
                _listener = gameObject.AddComponent<AudioListener>();

            var bgmGroup = GetGroup(BgmGroupName);
            var sfxGroup = GetGroup(SfxGroupName);

            bgmA = gameObject.AddComponent<AudioSource>();
            bgmA.playOnAwake = false;
            bgmA.loop = true;
            bgmA.outputAudioMixerGroup = bgmGroup;

            bgmB = gameObject.AddComponent<AudioSource>();
            bgmB.playOnAwake = false;
            bgmB.loop = true;
            bgmB.outputAudioMixerGroup = bgmGroup;

            _activeBgm = bgmA;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.outputAudioMixerGroup = sfxGroup;

            goalCrowdSource = gameObject.AddComponent<AudioSource>();
            goalCrowdSource.playOnAwake = false;
            goalCrowdSource.outputAudioMixerGroup = sfxGroup;

            int n = ambientClips != null ? ambientClips.Length : 0;
            ambientSources = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.outputAudioMixerGroup = sfxGroup;
                ambientSources[i] = s;
            }
        }

        private AudioMixerGroup GetGroup(string groupName)
        {
            if (mixer == null)
                return null;
            var groups = mixer.FindMatchingGroups(groupName);
            return (groups != null && groups.Length > 0) ? groups[0] : null;
        }

        // ── 씬 구동 ──────────────────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySceneAudio(scene.name);

        /// <summary>씬에 AudioListener 가 없거나 여러 개여도 항상 우리 리스너 1개만 활성.</summary>
        private void EnsureSingleListener()
        {
            if (_listener == null)
                return;
            var all = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var l in all)
            {
                if (l != _listener && l.enabled)
                    l.enabled = false;
            }
            _listener.enabled = true;
        }

        private void ApplySceneAudio(string sceneName)
        {
            EnsureSingleListener();

            // OptionsScene 은 중립 — 직전 BGM/앰비언스를 그대로 유지 (메뉴에서 들어오면 메뉴곡,
            // 게임 중 들어오면 라디오). bgmSource 는 DontDestroyOnLoad 라 끊김 없이 이어짐.
            if (sceneName == OptionsSceneName)
                return;
            if (sceneName == MatchSceneName)
                SetMode(AudioMode.Match);
            else if (System.Array.IndexOf(MenuSceneNames, sceneName) >= 0)
                SetMode(AudioMode.Menu);
            else
                SetMode(AudioMode.Radio);
        }

        private void SetMode(AudioMode mode)
        {
            if (_mode == mode)
            {
                // 같은 모드 재진입: 라디오는 끊지 않고 유지 (연속 재생). 그 외도 무동작.
                return;
            }

            var prev = _mode;
            _mode = mode;

            switch (mode)
            {
                case AudioMode.Menu:
                    StopAmbient();
                    CrossfadeBgmTo(mainMenuBgm, loop: true);
                    break;

                case AudioMode.Radio:
                    StopAmbient();
                    if (
                        prev == AudioMode.Match
                        && _activeBgm != null
                        && _activeBgm.clip != null
                        && _activeBgm.clip != mainMenuBgm
                    )
                    {
                        // 매치에서 복귀 → 멈춰둔 라디오 곡 이어서 (진짜 연속).
                        _activeBgm.UnPause();
                        if (!_activeBgm.isPlaying)
                            PlayRadioTrack(_radioIndex);
                    }
                    else
                    {
                        PlayRadioTrack(_radioIndex);
                    }
                    break;

                case AudioMode.Match:
                    PauseBgm();
                    StartAmbient();
                    break;
            }
        }

        // ── BGM ──────────────────────────────────────────────────────

        private void PlayRadioTrack(int index)
        {
            if (radioPlaylist == null || radioPlaylist.Length == 0)
                return;
            int len = radioPlaylist.Length;
            _radioIndex = ((index % len) + len) % len;
            CrossfadeBgmTo(radioPlaylist[_radioIndex], loop: false);
        }

        private void CrossfadeBgmTo(AudioClip clip, bool loop)
        {
            if (_activeBgm == null || clip == null)
                return;
            if (_activeBgm.clip == clip && _activeBgm.isPlaying && !_crossfading)
                return;

            // 들어올 곡 = 비활성 소스에서 0볼륨으로 시작 → 활성 소스와 오버랩하며 교차.
            var incoming = (_activeBgm == bgmA) ? bgmB : bgmA;
            var outgoing = _activeBgm;
            if (_bgmFade != null)
                StopCoroutine(_bgmFade);

            incoming.clip = clip;
            incoming.loop = loop;
            incoming.volume = 0f;
            incoming.Play();
            _activeBgm = incoming;
            _bgmFade = StartCoroutine(CrossfadeRoutine(incoming, outgoing));
        }

        private IEnumerator CrossfadeRoutine(AudioSource incoming, AudioSource outgoing)
        {
            _crossfading = true;
            float startOut = outgoing != null ? outgoing.volume : 0f;
            for (float t = 0f; t < CrossfadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / CrossfadeSeconds;
                incoming.volume = Mathf.Lerp(0f, 1f, k);
                if (outgoing != null)
                    outgoing.volume = Mathf.Lerp(startOut, 0f, k);
                yield return null;
            }
            incoming.volume = 1f;
            if (outgoing != null)
                outgoing.Stop();
            _crossfading = false;
            _bgmFade = null;
        }

        /// <summary>매치 진입 — 진행 중 페이드를 정리하고 활성 BGM 일시정지.</summary>
        private void PauseBgm()
        {
            if (_bgmFade != null)
            {
                StopCoroutine(_bgmFade);
                _bgmFade = null;
            }
            _crossfading = false;
            // 활성 소스만 풀볼륨 유지, 페이드 중이던 반대 소스는 정지.
            var other = (_activeBgm == bgmA) ? bgmB : bgmA;
            if (other != null)
                other.Stop();
            if (_activeBgm != null)
            {
                _activeBgm.volume = 1f;
                if (_activeBgm.isPlaying)
                    _activeBgm.Pause();
            }
        }

        public void StopBGM()
        {
            if (bgmA != null)
                bgmA.Stop();
            if (bgmB != null)
                bgmB.Stop();
        }

        // ── 매치 앰비언스 ────────────────────────────────────────────

        private void StartAmbient()
        {
            if (ambientSources == null)
                return;
            for (int i = 0; i < ambientSources.Length; i++)
            {
                var src = ambientSources[i];
                if (src == null)
                    continue;
                var clip =
                    (ambientClips != null && i < ambientClips.Length) ? ambientClips[i] : null;
                if (clip == null)
                    continue;
                src.clip = clip;
                src.loop = true;
                // 동일 길이 함성 루프가 위상 정렬돼 콤필터 되는 것 방지 — 시작 오프셋 분산.
                src.time = Random.Range(0f, Mathf.Max(0f, clip.length * 0.5f));
                if (!src.isPlaying)
                    src.Play();
            }
        }

        private void StopAmbient()
        {
            if (ambientSources == null)
                return;
            foreach (var src in ambientSources)
            {
                if (src != null)
                    src.Stop();
            }
        }

        // ── SFX ──────────────────────────────────────────────────────

        public void PlaySFX(SfxId id)
        {
            if (sfxSource == null)
                return; // silent fallback (EditMode / 미부트스트랩)

            if (id == SfxId.Goal)
            {
                // 골 = 네트 출렁(짧음, 원샷) + 관중 환호(1.5초 페이드아웃, 전용 소스).
                // 둘 다 sfxSource/ambient 와 별개라 서로/앰비언스 안 끊김.
                if (goalNetClip != null)
                    sfxSource.PlayOneShot(goalNetClip);
                PlayGoalCrowd();
                return;
            }

            var clip = GetClip(sfxClips, (int)id);
            if (clip == null)
                return; // silent fallback
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>골 환호 — 전용 소스에서 재생하며 1.5초간 점점 작아져 자연스럽게 종료.</summary>
        private void PlayGoalCrowd()
        {
            if (goalCrowdSource == null || goalCrowdClip == null)
                return;
            if (_goalFade != null)
                StopCoroutine(_goalFade);
            goalCrowdSource.clip = goalCrowdClip;
            goalCrowdSource.loop = false;
            goalCrowdSource.volume = 1f;
            goalCrowdSource.Play();
            _goalFade = StartCoroutine(GoalCrowdFadeRoutine());
        }

        private IEnumerator GoalCrowdFadeRoutine()
        {
            for (float t = 0f; t < GoalCrowdSeconds; t += Time.unscaledDeltaTime)
            {
                goalCrowdSource.volume = Mathf.Lerp(1f, 0f, t / GoalCrowdSeconds);
                yield return null;
            }
            goalCrowdSource.volume = 0f;
            goalCrowdSource.Stop();
            _goalFade = null;
        }

        private static AudioClip GetClip(AudioClip[] arr, int index)
        {
            if (arr == null || index < 0 || index >= arr.Length)
                return null;
            return arr[index];
        }

        // ── 믹서 볼륨 ────────────────────────────────────────────────

        /// <summary>OptionsManager 값으로 mixer 볼륨 동기화.</summary>
        public void ApplyOptionsVolume()
        {
            SetMixerVolume(MasterParam, OptionsManager.MasterVolume);
            SetMixerVolume(SfxParam, OptionsManager.SfxVolume);
            SetMixerVolume(BgmParam, OptionsManager.BgmVolume);
        }

        public void SetMixerVolume(string param, float volume0to100)
        {
            if (mixer == null)
                return;
            mixer.SetFloat(param, VolumeToDb(volume0to100));
        }

        /// <summary>0-100 슬라이더 값 → AudioMixer dB. 0=−80(mute), 100=0(원본).</summary>
        public static float VolumeToDb(float volume0to100)
        {
            if (volume0to100 <= 0f)
                return MuteDb;
            return Mathf.Log10(Mathf.Clamp(volume0to100 / 100f, 0.0001f, 1f)) * 20f;
        }
    }
}
