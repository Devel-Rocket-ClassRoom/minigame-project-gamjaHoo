// SeedV10LocalizationData.cs
// V1.0 LocalizationSO 인스턴스 생성 + 모든 UI 키 일괄 시드.
// FM-Lite > Seed > Generate V1.0 Localization 에서 실행.
// 기존 asset 재실행 시 entries 를 덮어쓰되 GUID 는 유지.

using System.Collections.Generic;
using FMLite.Domain;
using UnityEditor;
using UnityEngine;

namespace FMLite.Editor
{
    public static class SeedV10LocalizationData
    {
        private const string AssetPath =
            "Assets/_Project/Data/Resources/Localization/LocalizationData.asset";

        [MenuItem("FM-Lite/Seed/Generate V1.0 Localization")]
        public static void Generate()
        {
            EnsureFolder("Assets/_Project/Data/Resources", "Localization");

            var so = AssetDatabase.LoadAssetAtPath<LocalizationSO>(AssetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<LocalizationSO>();
                AssetDatabase.CreateAsset(so, AssetPath);
            }

            so.entries = BuildEntries();
            so.BuildIndex();
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[SeedV10LocalizationData] {so.entries.Count} entries saved → {AssetPath}"
            );
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static List<LocalizationEntry> BuildEntries() =>
            new List<LocalizationEntry>
            {
                // ── Dashboard ──────────────────────────────────────────────
                E("reroll_token_fmt", "리롤 토큰 {0}", "Reroll Tokens: {0}"),
                E("no_next_match", "다음 경기 없음", "No upcoming match"),
                E("home", "홈", "Home"),
                E("away", "원정", "Away"),
                // ── Gacha ──────────────────────────────────────────────────
                E("reroll_fmt", "리롤 {0}", "Reroll {0}"),
                // ── Youth ──────────────────────────────────────────────────
                E("no_current_inspection", "현재 인스펙션 없음", "No current inspection"),
                E("youth_inspection_title_fmt", "유스 인스펙션 {0}", "Youth Inspection {0}"),
                E(
                    "youth_facility_full_fmt",
                    "유스 시설 레벨 {0} / 정원 {1}",
                    "Youth Facility Lv.{0} / Size {1}"
                ),
                E("youth_facility_fmt", "유스 시설 레벨 {0}", "Youth Facility Lv.{0}"),
                // ── Transfer ───────────────────────────────────────────────
                E("filter_all", "전체", "All"),
                E("market_value_fmt", "시장가 £{0}M", "Market Value £{0}M"),
                E("transfer_window_open", "이적 창 열림 (체결 가능)", "Transfer Window Open"),
                E(
                    "transfer_window_closed",
                    "이적 창 닫힘 (오퍼 제출만 가능)",
                    "Transfer Window Closed"
                ),
                E("no_active_offers", "활성 오퍼 없음", "No Active Offers"),
                E("active_offers_header", "[활성 오퍼]", "[Active Offers]"),
                // ── Facility ───────────────────────────────────────────────
                E("facility_money_fmt", "잔고: £{0}M", "Balance: £{0}M"),
                E(
                    "facility_upgrade_progress_fmt",
                    "{0} 업그레이드 중 (완료: {1})",
                    "{0} upgrading (done: {1})"
                ),
                E("no_pending_upgrade", "진행 중인 업그레이드 없음", "No pending upgrade"),
                E("max_level", "최고 등급", "Max Level"),
                E(
                    "facility_upgrade_cost_fmt",
                    "업그레이드 £{0}M ({1}일)",
                    "Upgrade £{0}M ({1} days)"
                ),
                // ── Facility (V1.0 D.5 — 8 시설 이름 + 효과 fmt) ────────────
                E("facility_name_scout", "스카우트", "Scouting"),
                E("facility_name_training", "훈련 시설", "Training"),
                E("facility_name_youth_coach", "유스 코치", "Youth Coaching"),
                E("facility_name_youth_recruitment", "유스 모집", "Youth Recruitment"),
                E("facility_name_youth_facility", "유스 시설", "Youth Facilities"),
                E("facility_name_medical", "의료", "Medical"),
                E("facility_name_stadium", "스타디움", "Stadium"),
                E("facility_name_gym", "체육관", "Gym"),
                E(
                    "facility_effect_scout_fmt",
                    "명단 {0}명 · ±{1} CA",
                    "List {0} · ±{1} CA"
                ),
                E(
                    "facility_effect_training_fmt",
                    "훈련 효율 ×{0}",
                    "Training ×{0}"
                ),
                E(
                    "facility_effect_youth_coach_fmt",
                    "유스 +{0} PA · 트레잇 {1}%",
                    "Youth +{0} PA · Trait {1}%"
                ),
                E(
                    "facility_effect_youth_recruitment_fmt",
                    "풀 {0}명",
                    "Pool {0}"
                ),
                E(
                    "facility_effect_youth_facility_fmt",
                    "유스 성장 ×{0}",
                    "Youth Growth ×{0}"
                ),
                E(
                    "facility_effect_medical_fmt",
                    "부상률 ×{0} · 회복 ×{1}",
                    "Injury ×{0} · Recovery ×{1}"
                ),
                E(
                    "facility_effect_stadium_fmt",
                    "입장료 £{0} · 명성 +{1}",
                    "Ticket £{0} · Rep +{1}"
                ),
                E(
                    "facility_effect_gym_fmt",
                    "피지컬 성장 ×{0}",
                    "Physical ×{0}"
                ),
                // ── Scouting Tier (V1.0 E.3 — 정성적 라벨 5단계) ─────────────
                E("scout_tier_very_high", "매우 높음", "Very High"),
                E("scout_tier_high", "높음", "High"),
                E("scout_tier_average", "중간", "Average"),
                E("scout_tier_low", "낮음", "Low"),
                E("scout_tier_very_low", "매우 낮음", "Very Low"),
                // ── Club select ────────────────────────────────────────────
                E("reputation_fmt", "명성 {0}", "Reputation {0}"),
                // ── Fixture ────────────────────────────────────────────────
                E("fixture_title_fmt", "{0} {1} 일정", "{0} {1} Fixtures"),
                E("fixture_title_fallback_fmt", "일정 {0}", "Fixtures {0}"),
                // ── Standings ──────────────────────────────────────────────
                E("standings_title_fmt", "{0} {1} 순위표", "{0} {1} Standings"),
                E("standings_title_fallback_fmt", "순위표 {0}", "Standings {0}"),
                // ── Save slot ──────────────────────────────────────────────
                E("club_not_selected", "미선택", "Not Selected"),
                // ── Player profile — header ────────────────────────────────
                E("player_not_found_fmt", "선수 없음 (id={0})", "Player not found (id={0})"),
                E("player_position_age_fmt", "{0} · {1}세", "{0} · Age {1}"),
                // ── Player profile — technical ─────────────────────────────
                E("no_stats_tech", "기술: -", "Technical: -"),
                E("section_tech", "[기술]", "[Technical]"),
                E("stat_passing", "패스", "Passing"),
                E("stat_shooting", "슈팅", "Shooting"),
                E("stat_tackling", "태클", "Tackling"),
                E("stat_dribbling", "드리블", "Dribbling"),
                E("stat_heading", "헤딩", "Heading"),
                E("stat_crossing", "크로스", "Crossing"),
                E("stat_first_touch", "퍼스트터치", "First Touch"),
                E("stat_finishing", "마무리", "Finishing"),
                E("stat_long_shots", "중거리", "Long Shots"),
                E("stat_free_kick", "프리킥", "Free Kick"),
                E("stat_penalty", "패널티", "Penalty"),
                E("stat_corners", "코너", "Corners"),
                E("stat_marking", "마킹", "Marking"),
                E("stat_technique", "테크닉", "Technique"),
                E("stat_long_throws", "롱스로우", "Long Throws"),
                // ── Player profile — mental ────────────────────────────────
                E("no_stats_mental", "정신: -", "Mental: -"),
                E("section_mental", "[정신]", "[Mental]"),
                E("stat_vision", "시야", "Vision"),
                E("stat_anticipation", "예측", "Anticipation"),
                E("stat_composure", "침착", "Composure"),
                E("stat_concentration", "집중", "Concentration"),
                E("stat_decisions", "판단", "Decisions"),
                E("stat_determination", "투지", "Determination"),
                E("stat_leadership", "리더십", "Leadership"),
                E("stat_off_the_ball", "오프더볼", "Off The Ball"),
                E("stat_positioning", "포지셔닝", "Positioning"),
                E("stat_teamwork", "팀워크", "Teamwork"),
                E("stat_work_rate", "활동량", "Work Rate"),
                E("stat_aggression", "공격성", "Aggression"),
                E("stat_bravery", "용기", "Bravery"),
                E("stat_flair", "재간", "Flair"),
                // ── Player profile — physical ──────────────────────────────
                E("no_stats_physical", "신체: -", "Physical: -"),
                E("section_physical", "[신체]", "[Physical]"),
                E("stat_acceleration", "가속", "Acceleration"),
                E("stat_agility", "민첩", "Agility"),
                E("stat_balance", "밸런스", "Balance"),
                E("stat_jumping", "점프", "Jumping"),
                E("stat_natural_fitness", "피지컬", "Natural Fitness"),
                E("stat_pace", "스피드", "Pace"),
                E("stat_stamina", "스태미나", "Stamina"),
                E("stat_strength", "체력", "Strength"),
                // ── Player profile — goalkeeper ────────────────────────────
                E("section_gk", "[골키퍼]", "[Goalkeeper]"),
                E("stat_aerial_reach", "공중장악", "Aerial Reach"),
                E("stat_command_of_area", "박스장악", "Command of Area"),
                E("stat_communication", "지시", "Communication"),
                E("stat_eccentricity", "돌발", "Eccentricity"),
                E("stat_handling", "핸들링", "Handling"),
                E("stat_kicking", "킥", "Kicking"),
                E("stat_one_on_ones", "1대1", "One on Ones"),
                E("stat_reflexes", "반응", "Reflexes"),
                E("stat_rushing_out", "돌진", "Rushing Out"),
                E("stat_throwing", "스로인", "Throwing"),
                E("stat_first_touch_gk", "퍼스트터치(GK)", "First Touch (GK)"),
                E("stat_passing_gk", "패스(GK)", "Passing (GK)"),
                E("stat_punching_tendency", "펀칭경향", "Punching Tendency"),
                // ── Player profile — traits / contract / state / career ────
                E("section_traits", "[트레잇]", "[Traits]"),
                E("no_traits", "없음", "None"),
                E("section_contract", "[계약]", "[Contract]"),
                E("no_info", "정보 없음", "No Info"),
                E("contract_wage_fmt", "주급: £{0}", "Weekly Wage: £{0}"),
                E("contract_end_fmt", "계약 만료: {0}", "Contract End: {0}"),
                E("contract_release_debug_fmt", "바이아웃: £{0}", "Release Clause: £{0}"),
                E("contract_release", "바이아웃: 있음", "Release Clause: Yes"),
                E("section_state", "[상태]", "[State]"),
                E("state_fatigue_fmt", "피로: {0}", "Fatigue: {0}"),
                E("state_morale_fmt", "사기: {0}", "Morale: {0}"),
                E("state_form_fmt", "폼: {0}", "Form: {0}"),
                E("state_appearances_fmt", "출전: {0}경기", "Appearances: {0}"),
                E(
                    "state_injury_return_fmt",
                    "부상: 복귀 예정 {0}",
                    "Injury: Expected return {0}"
                ),
                E("state_no_injury", "부상: 없음", "No injury"),
                E("state_transfer_listed", "이적 리스트 등재", "Transfer Listed"),
                E("section_career", "[커리어]", "[Career]"),
                E("no_career", "기록 없음", "No records"),
                E(
                    "career_entry_fmt",
                    "{0}-{1}  {2}  {3}경기 {4}골 {5}도움",
                    "{0}-{1}  {2}  {3} apps {4} goals {5} assists"
                ),
                // ── V1.0 G.2 Sub-B 면담 + 인박스 ──────────────────────────
                E("interview_button", "면담", "Interview"),
                E("interview_dialog_title", "선수 면담", "Player Interview"),
                E("interview_close", "닫기", "Close"),
                E("interview_praise", "현재 성과 칭찬", "Praise recent form"),
                E("interview_criticize", "더 노력해야 한다", "Demand more effort"),
                E(
                    "interview_promise_playtime",
                    "출전시간 보장하겠다",
                    "Promise more playing time"
                ),
                E(
                    "interview_promise_renewal",
                    "다음 시즌 새 계약 협상하자",
                    "Promise contract renewal next season"
                ),
                // Promise 타입 라벨
                E("promise_type_playtime", "출전시간 약속", "Playtime Agreement"),
                E("promise_type_renewal", "재계약 약속", "Renewal Promise"),
                E("promise_type_transfer_in", "영입 약속", "Transfer-In Promise"),
                E("promise_type_transfer_out", "이적 허용 약속", "Transfer-Out Promise"),
                // 인박스 포맷
                E(
                    "inbox_promise_created_fmt",
                    "[약속] {0} — {1} 등록",
                    "[Promise] {0} — {1} created"
                ),
                E(
                    "inbox_promise_fulfilled_fmt",
                    "[약속] {0} — {1} 이행",
                    "[Promise] {0} — {1} fulfilled"
                ),
                E(
                    "inbox_promise_broken_fmt",
                    "[약속] {0} — {1} 미이행",
                    "[Promise] {0} — {1} broken"
                ),
                E(
                    "inbox_promise_approaching_fmt",
                    "[약속] {0} — {1} 마감 {2}일 남음",
                    "[Promise] {0} — {1} due in {2} days"
                ),
                E(
                    "inbox_transfer_request_fmt",
                    "[이적 요청] {0} 가 이적 요청",
                    "[Transfer Request] {0} requested transfer"
                ),
                // ── V1.0 G.4 이적 요청 다이얼로그 ─────────────────────────
                E("transfer_request_dialog_title", "이적 요청", "Transfer Request"),
                E(
                    "transfer_request_dialog_message_fmt",
                    "{0} 선수가 이적을 요청했습니다.",
                    "{0} has requested a transfer."
                ),
                E(
                    "transfer_request_accept",
                    "수락 (이적 리스트 등재)",
                    "Accept (Add to Transfer List)"
                ),
                E("transfer_request_reject", "거절", "Reject"),
                E("transfer_request_interview", "면담", "Interview"),
                E("transfer_request_close", "나중에", "Later"),
            };

        private static LocalizationEntry E(string key, string ko, string en) =>
            new LocalizationEntry
            {
                key = key,
                korean = ko,
                english = en,
            };
    }
}
