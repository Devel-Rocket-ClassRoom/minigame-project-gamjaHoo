// LocalizationV10Tests.cs
// Task A.5 DoD:
//   T1 InboxRouter 가 사용하는 10개 fmt 키 등록 검증
//   T2 매치 이벤트 카테고리별 변형 수 검증
//   T3 Options 키 등록 검증
//   T4 Currency 4 통화 심볼 등록 검증
//   T5 시너지 10종 이름/설명 등록 검증
//   T6 FA Cup 라운드 키 등록 검증
//   T7 훈련 시스템 키 등록 검증
//   T8 모든 entry 의 KO/EN 둘 다 비어있지 않음
//   T9 중복 key 없음

using System.Collections.Generic;
using System.Linq;
using FMLite.Editor;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class LocalizationV10Tests
    {
        private static HashSet<string> _keys;

        [OneTimeSetUp]
        public void Setup()
        {
            _keys = new HashSet<string>(SeedV10LocalizationData.BuildEntries().Select(e => e.key));
        }

        // ── T1. InboxRouter 10 fmt 키 ────────────────────────────────

        [Test]
        public void T1_InboxRouter_Fmt_Keys_Registered()
        {
            var required = new[]
            {
                "inbox_promise_created_fmt",
                "inbox_promise_fulfilled_fmt",
                "inbox_promise_broken_fmt",
                "inbox_promise_deadline_fmt",
                "inbox_transfer_request_fmt",
                "inbox_counter_offer_fmt",
                "inbox_offer_accepted_fmt",
                "inbox_offer_rejected_fmt",
                "inbox_contract_renewed_fmt",
                "inbox_contract_rejected_fmt",
                "inbox_youth_intake_fmt",
                "inbox_youth_promotion_fmt",
            };
            foreach (var k in required)
                Assert.IsTrue(_keys.Contains(k), $"T1: '{k}' 누락");
        }

        // ── T2. 매치 이벤트 카테고리별 변형 수 ────────────────────────

        [Test]
        public void T2_MatchEvent_VariantCounts()
        {
            AssertVariants("match_event_goal", 5);
            AssertVariants("match_event_keypass", 5);
            AssertVariants("match_event_save", 5);
            AssertVariants("match_event_shoton", 4);
            AssertVariants("match_event_shotoff", 3);
            AssertVariants("match_event_yellow", 3);
            AssertVariants("match_event_red", 3);
            AssertVariants("match_event_2nd_yellow", 2);
            AssertVariants("match_event_foul", 3);
            AssertVariants("match_event_pk_won", 3);
            AssertVariants("match_event_pk_miss", 3);
            AssertVariants("match_event_pk_saved", 3);
            AssertVariants("match_event_injury_minor", 3);
            AssertVariants("match_event_injury_major", 3);
            AssertVariants("match_event_sub", 4);
            AssertVariants("match_event_tackle", 3);
            AssertVariants("match_event_cross", 3);
            AssertVariants("match_event_corner", 3);
            AssertVariants("match_event_fk_direct", 4);
            AssertVariants("match_event_fk_indirect", 3);
            AssertVariants("match_event_throw", 3);
            AssertVariants("match_event_offside", 3);
            AssertVariants("match_event_interception", 3);
            AssertVariants("match_event_kickoff", 4);
            AssertVariants("match_event_halftime", 3);
            AssertVariants("match_event_fulltime", 4);
            AssertVariants("match_event_et_start", 2);
            AssertVariants("match_event_pso", 3);

            AssertVariants("match_report_win", 5);
            AssertVariants("match_report_loss", 5);
            AssertVariants("match_report_draw", 5);
        }

        // ── T3. Options 키 ─────────────────────────────────────────

        [Test]
        public void T3_Options_Keys_Registered()
        {
            var required = new[]
            {
                "options_master_volume",
                "options_sfx_volume",
                "options_bgm_volume",
                "options_language",
                "options_currency",
                "options_ui_scale",
                "options_auto_save",
                "options_shortcuts",
            };
            foreach (var k in required)
                Assert.IsTrue(_keys.Contains(k), $"T3: '{k}' 누락");
        }

        // ── T4. Currency 4 통화 ───────────────────────────────────

        [Test]
        public void T4_Currency_Symbols_AllRegistered()
        {
            Assert.IsTrue(_keys.Contains("currency_gbp_symbol"), "T4: GBP");
            Assert.IsTrue(_keys.Contains("currency_usd_symbol"), "T4: USD");
            Assert.IsTrue(_keys.Contains("currency_eur_symbol"), "T4: EUR");
            Assert.IsTrue(_keys.Contains("currency_krw_symbol"), "T4: KRW");
            Assert.IsTrue(_keys.Contains("currency_unit_million"), "T4: M unit");
        }

        // ── T5. 시너지 10종 ──────────────────────────────────────

        [Test]
        public void T5_Synergy_TenCatalog_NameAndDesc()
        {
            var ids = new[]
            {
                "big_and_small",
                "target_speedster",
                "possession",
                "defensive_wall",
                "wingback_duo",
                "double_pivot",
                "trequartista",
                "false_nine",
                "diamond_midfield",
                "homegrown_spine",
            };
            foreach (var id in ids)
            {
                Assert.IsTrue(_keys.Contains($"synergy_{id}_name"), $"T5: '{id}_name' 누락");
                Assert.IsTrue(_keys.Contains($"synergy_{id}_desc"), $"T5: '{id}_desc' 누락");
            }
        }

        // ── T6. FA Cup ──────────────────────────────────────────

        [Test]
        public void T6_Cup_RoundKeys_Registered()
        {
            Assert.IsTrue(_keys.Contains("cup_facup_name"), "T6: cup_facup_name");
            Assert.IsTrue(_keys.Contains("cup_round_32"), "T6: round_32");
            Assert.IsTrue(_keys.Contains("cup_round_16"), "T6: round_16");
            Assert.IsTrue(_keys.Contains("cup_quarter"), "T6: quarter");
            Assert.IsTrue(_keys.Contains("cup_semi"), "T6: semi");
            Assert.IsTrue(_keys.Contains("cup_final"), "T6: final");
            Assert.IsTrue(_keys.Contains("cup_winner_fmt"), "T6: winner_fmt");
        }

        // ── T7. 훈련 시스템 ──────────────────────────────────────

        [Test]
        public void T7_Training_Keys_Registered()
        {
            var required = new[]
            {
                "training_title",
                "training_group",
                "training_individual",
                "training_intensity_low",
                "training_intensity_medium",
                "training_intensity_high",
                "training_individual_capacity_fmt",
            };
            foreach (var k in required)
                Assert.IsTrue(_keys.Contains(k), $"T7: '{k}' 누락");
        }

        // ── T8. 모든 entry 의 KO/EN 둘 다 비어있지 않음 ──────────────

        [Test]
        public void T8_AllEntries_HaveBothKoAndEnText()
        {
            var entries = SeedV10LocalizationData.BuildEntries();
            foreach (var e in entries)
            {
                Assert.IsFalse(string.IsNullOrEmpty(e.korean), $"T8: '{e.key}' KO empty");
                Assert.IsFalse(string.IsNullOrEmpty(e.english), $"T8: '{e.key}' EN empty");
            }
        }

        // ── T9. 중복 key 없음 ────────────────────────────────────

        [Test]
        public void T9_NoDuplicateKeys()
        {
            var entries = SeedV10LocalizationData.BuildEntries();
            var dups = entries
                .GroupBy(e => e.key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            Assert.AreEqual(0, dups.Count, $"T9: 중복 키 — {string.Join(", ", dups)}");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────

        private static void AssertVariants(string prefix, int count)
        {
            for (int i = 1; i <= count; i++)
            {
                var key = $"{prefix}_{i}";
                Assert.IsTrue(_keys.Contains(key), $"매치 변형 누락: {key}");
            }
        }
    }
}
