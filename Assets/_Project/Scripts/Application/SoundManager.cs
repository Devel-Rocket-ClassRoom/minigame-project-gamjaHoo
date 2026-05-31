// SoundManager.cs
// V1.0 — AudioMixer 어댑터 + SFX/BGM 재생 (design-decisions.md #60).
// MonoBehaviour 싱글톤 (DontDestroyOnLoad, GameManager 패턴).
//
// A.7 골격: 코드만 — AudioMixer asset (MasterMixer.mixer), AudioClip 자산은 Stage Y.1 에서 할당.
// mixer/clip null 상태에서도 silent fallback (예외 X).
//
// Volume 변환: 0-100 슬라이더 → AudioMixer dB. 일반 공식 dB = 20 × log10(v / 100).
// v=100 → 0 dB, v=10 → −20 dB, v=0 → −80 dB (mute floor).

using UnityEngine;
using UnityEngine.Audio;

namespace FMLite.Application
{
    public class SoundManager : MonoBehaviour
    {
        public const string MasterParam = "MasterVolume";
        public const string SfxParam = "SfxVolume";
        public const string BgmParam = "BgmVolume";
        public const float MuteDb = -80f;

        public static SoundManager Instance { get; private set; }

        [SerializeField]
        private AudioMixer mixer;

        [SerializeField]
        private AudioSource sfxSource;

        [SerializeField]
        private AudioSource bgmSource;

        [SerializeField]
        private AudioClip[] sfxClips; // SfxId 순서대로 12개

        [SerializeField]
        private AudioClip[] bgmClips; // BgmId 순서대로 3개

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad 는 Play Mode 에서만 안전 (EditMode 테스트 회피).
            if (UnityEngine.Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

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

        public void PlaySFX(SfxId id)
        {
            var clip = GetClip(sfxClips, (int)id);
            if (clip == null || sfxSource == null)
                return; // silent fallback
            sfxSource.PlayOneShot(clip);
        }

        public void PlayBGM(BgmId id)
        {
            var clip = GetClip(bgmClips, (int)id);
            if (clip == null || bgmSource == null)
                return; // silent fallback
            if (bgmSource.clip == clip && bgmSource.isPlaying)
                return; // 같은 BGM 재요청 무시
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null)
                bgmSource.Stop();
        }

        private static AudioClip GetClip(AudioClip[] arr, int index)
        {
            if (arr == null || index < 0 || index >= arr.Length)
                return null;
            return arr[index];
        }
    }
}
