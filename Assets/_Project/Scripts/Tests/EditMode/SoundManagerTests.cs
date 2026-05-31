// SoundManagerTests.cs
// Task A.7 DoD:
//   T1  VolumeToDb 변환 — 100→0dB / 0→MuteDb(-80) / 10→약 -20dB
//   T2  Singleton — 인스턴스화 정상 / 두 번째 인스턴스 Destroy
//   T3  PlaySFX(null clip) → 예외 X (silent fallback)
//   T4  PlayBGM(null clip) → 예외 X (silent fallback)
//   T5  SfxId 12종 / BgmId 3종 enum count 검증

using System;
using FMLite.Application;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class SoundManagerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        // ── T1. VolumeToDb 변환 ──────────────────────────────────────

        [Test]
        public void T1_VolumeToDb_Conversion()
        {
            Assert.AreEqual(0f, SoundManager.VolumeToDb(100f), 0.01f, "T1: 100→0dB");
            Assert.AreEqual(SoundManager.MuteDb, SoundManager.VolumeToDb(0f), 0.01f, "T1: 0→mute");
            Assert.AreEqual(SoundManager.MuteDb, SoundManager.VolumeToDb(-10f), 0.01f, "T1: 음수→mute");
            // Mathf.Log10(0.1) × 20 = -20
            Assert.AreEqual(-20f, SoundManager.VolumeToDb(10f), 0.01f, "T1: 10→-20dB");
            // Mathf.Log10(0.5) × 20 ≈ -6.02
            Assert.AreEqual(-6.02f, SoundManager.VolumeToDb(50f), 0.05f, "T1: 50→약 -6dB");
        }

        // T2 (Singleton Instance 설정) 는 EditMode 에서 Awake 가 호출되지 않으므로 PlayMode 로 이관 — V1.x.

        // ── T3. PlaySFX silent fallback ─────────────────────────────

        [Test]
        public void T3_PlaySFX_NullClip_NoException()
        {
            _go = new GameObject("SoundManager");
            var sm = _go.AddComponent<SoundManager>();

            // clip 미할당 상태 — 예외 X
            Assert.DoesNotThrow(() => sm.PlaySFX(SfxId.ButtonClick), "T3: silent fallback");
            Assert.DoesNotThrow(() => sm.PlaySFX(SfxId.Goal), "T3: silent fallback");
        }

        // ── T4. PlayBGM silent fallback ─────────────────────────────

        [Test]
        public void T4_PlayBGM_NullClip_NoException()
        {
            _go = new GameObject("SoundManager");
            var sm = _go.AddComponent<SoundManager>();

            Assert.DoesNotThrow(() => sm.PlayBGM(BgmId.MainMenu), "T4: silent fallback");
            Assert.DoesNotThrow(() => sm.PlayBGM(BgmId.Match), "T4: silent fallback");
            Assert.DoesNotThrow(() => sm.StopBGM(), "T4: StopBGM safe");
        }

        // ── T5. Enum count 검증 ──────────────────────────────────────

        [Test]
        public void T5_Enum_Counts_MatchSpec()
        {
            Assert.AreEqual(12, Enum.GetValues(typeof(SfxId)).Length, "T5: SFX 12종");
            Assert.AreEqual(3, Enum.GetValues(typeof(BgmId)).Length, "T5: BGM 3곡");
        }
    }
}
