// SeedV10LocalizationData.cs
// V0.5 + V1.0 LocalizationSO 인스턴스 생성 + 모든 UI 키 일괄 시드.
// FM-Lite > Seed > Generate V1.0 Localization 에서 실행 (V1.0 기준).
// 기존 asset 재실행 시 entries 를 덮어쓰되 GUID 는 유지.
//
// V1.0 신규 entries 는 BuildV10Entries() 메서드들에 분리 (매치 이벤트 ~100, Options, Inbox, Currency,
// Synergy, FA Cup, 훈련 시스템). algorithms.md V1.0-2 ~ V1.0-4 카탈로그.

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

        public static List<LocalizationEntry> BuildEntries()
        {
            var entries = BuildV05Entries();
            entries.AddRange(BuildV10MatchEvents());
            entries.AddRange(BuildV10OptionsAndInbox());
            entries.AddRange(BuildV10CurrencySynergyCupTraining());
            entries.AddRange(BuildV10PlayerProfile());
            entries.AddRange(BuildV10Nav());
            entries.AddRange(BuildV10CompletedSceneLabels());
            return entries;
        }

        // 완료 V1.0 씬 하드코딩 라벨 로컬라이즈 (#467) — Dashboard/Squad/PlayerProfile/YouthManagement/Options.
        // 공통 라벨(컬럼/탭)은 공유 키. 동적 텍스트(컨트롤러 Localization.Get)는 제외.
        private static List<LocalizationEntry> BuildV10CompletedSceneLabels() =>
            new List<LocalizationEntry>
            {
                // 공통 컬럼 헤더 (Squad / YouthManagement)
                E("col_position", "포지션", "Position"),
                E("col_name", "이름", "Name"),
                E("col_age", "나이", "Age"),
                E("col_trend", "추세", "Trend"),
                // Squad 탭 (유스 탭은 nav_youth 재사용)
                E("tab_first_team", "1군", "First Team"),
                // Dashboard 다이얼로그 (이사회 / 이적 요청)
                E("dlg_board_demands_title", "이사회 요구사항", "Board Demands"),
                E("dlg_transfer_request_title", "이적 요청", "Transfer Request"),
                E("btn_interview", "면담", "Interview"),
                E("btn_reject", "거절", "Reject"),
                E("btn_later", "나중에", "Later"),
                E("btn_accept_transfer_list", "수락 (이적 리스트 등재)", "Accept (List for Transfer)"),
                E("btn_close", "닫기", "Close"),
                // PlayerProfile 섹션 / 스탯 카테고리
                E("pp_section_contract", "[계약]", "[Contract]"),
                E("pp_section_career", "[커리어]", "[Career]"),
                E("pp_section_status", "[상태]", "[Status]"),
                E("pp_section_traits", "[트레잇]", "[Traits]"),
                E("stat_cat_goalkeeping", "골키퍼", "Goalkeeping"),
                E("stat_cat_physical", "신체", "Physical"),
                E("stat_cat_technical", "기술", "Technical"),
                E("stat_cat_mental", "정신", "Mental"),
                // Options 라벨 (저장=nav_save, 뒤로=nav_back 재사용)
                E("opt_title", "옵션", "Options"),
                E("opt_sound", "사운드", "Sound"),
                E("opt_language", "언어", "Language"),
                E("opt_currency", "통화", "Currency"),
                E("opt_autosave", "자동 저장", "Auto Save"),
                E("opt_shortcuts", "단축키 안내", "Shortcuts"),
                // Dashboard 다음 매치 미리보기 (누락 키 — raw 표시되던 것)
                E("dashboard_form_fmt", "상대 폼 {0}", "Form: {0}"),
                E("dashboard_last_result_fmt", "직전 결과 {0}", "Last: {0}"),
                E("dashboard_h2h_fmt", "전적 {0}", "H2H: {0}"),
            };

        // GlobalNav 라벨 (#463) — 사이드바 10 + 탑바 5. SideBar 키는 GlobalNavController.SideBarScenes 순서.
        private static List<LocalizationEntry> BuildV10Nav() =>
            new List<LocalizationEntry>
            {
                E("nav_dashboard", "대시보드", "Dashboard"),
                E("nav_squad", "스쿼드", "Squad"),
                E("nav_tactic", "전술", "Tactics"),
                E("nav_lineup", "라인업", "Lineup"),
                E("nav_transfer", "이적", "Transfers"),
                E("nav_schedule", "일정", "Schedule"),
                E("nav_standings", "순위", "Standings"),
                E("nav_facility", "시설", "Facilities"),
                E("nav_youth", "유스", "Youth"),
                E("nav_mentoring", "멘토링", "Mentoring"),
                E("nav_back", "뒤로", "Back"),
                E("nav_inbox", "인박스", "Inbox"),
                E("nav_options", "옵션", "Options"),
                E("nav_save", "저장", "Save"),
                E("nav_home", "홈", "Home"),
            };

        private static List<LocalizationEntry> BuildV05Entries() =>
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
                E("market_value_fmt", "시장가 {0}", "Market Value {0}"),
                E("transfer_window_open", "이적 창 열림 (체결 가능)", "Transfer Window Open"),
                E(
                    "transfer_window_closed",
                    "이적 창 닫힘 (오퍼 제출만 가능)",
                    "Transfer Window Closed"
                ),
                E("no_active_offers", "활성 오퍼 없음", "No Active Offers"),
                E("active_offers_header", "[활성 오퍼]", "[Active Offers]"),
                // ── Facility ───────────────────────────────────────────────
                E("facility_money_fmt", "잔고: {0}", "Balance: {0}"),
                E(
                    "facility_upgrade_progress_fmt",
                    "{0} 업그레이드 중 (완료: {1})",
                    "{0} upgrading (done: {1})"
                ),
                E("no_pending_upgrade", "진행 중인 업그레이드 없음", "No pending upgrade"),
                E("max_level", "최고 등급", "Max Level"),
                E(
                    "facility_upgrade_cost_fmt",
                    "업그레이드 {0} ({1}일)",
                    "Upgrade {0} ({1} days)"
                ),
                // ── Facility (V0.5 D.5 — 8 시설 이름 + 효과 fmt) ────────────
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
                // ── Scouting Tier (V0.5 E.3 — 정성적 라벨 5단계) ─────────────
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
                E("contract_wage_fmt", "주급: {0}", "Weekly Wage: {0}"),
                E("contract_end_fmt", "계약 만료: {0}", "Contract End: {0}"),
                E("contract_release_debug_fmt", "바이아웃: {0}", "Release Clause: {0}"),
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
                // ── V0.5 G.2 Sub-B 면담 + 인박스 ──────────────────────────
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
                E(
                    "inbox_youth_promotion_fmt",
                    "[콜업] {0} (나이 {1}, CA {2}) — 1군 승격 가능",
                    "[Call-Up] {0} (age {1}, CA {2}) — eligible for promotion"
                ),
                E("profile_promote_to_senior", "1군 승격", "Promote to Senior"),
                E("profile_decline_promotion", "거절", "Decline"),
                // ── V0.5 G.4 이적 요청 다이얼로그 ─────────────────────────
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
                // ── V0.5 I.5 매치 텍스트 이벤트 ──────────────────────────
                // 시스템 이벤트
                E("match_kickoff", "킥오프", "Kick-off"),
                E("match_halftime", "전반전 종료", "Half time"),
                E("match_fulltime", "경기 종료", "Full time"),
                // 슈팅 / 골
                E(
                    "match_goal_fmt",
                    "{playerName} 골! ({minute}')",
                    "{playerName} scores! ({minute}')"
                ),
                E(
                    "match_goal_assist_fmt",
                    "{playerName} 골 (어시스트: {assistName}) ({minute}')",
                    "{playerName} scores (assist: {assistName}) ({minute}')"
                ),
                E(
                    "match_shot_on_target_fmt",
                    "{playerName} 유효슛 ({minute}')",
                    "{playerName} shot on target ({minute}')"
                ),
                E(
                    "match_shot_saved_fmt",
                    "{gkName} 선방! ({minute}')",
                    "{gkName} saves! ({minute}')"
                ),
                E(
                    "match_shot_off_target_fmt",
                    "{playerName} 빗나가는 슛 ({minute}')",
                    "{playerName} shot off target ({minute}')"
                ),
                // 페널티
                E(
                    "match_penalty_awarded_fmt",
                    "페널티킥! ({minute}')",
                    "Penalty! ({minute}')"
                ),
                E(
                    "match_penalty_goal_fmt",
                    "{playerName} 페널티 성공! ({minute}')",
                    "{playerName} converts the penalty! ({minute}')"
                ),
                E(
                    "match_penalty_miss_fmt",
                    "{playerName} 페널티 실패 ({minute}')",
                    "{playerName} misses the penalty ({minute}')"
                ),
                // 카드
                E(
                    "match_yellow_card_fmt",
                    "{playerName} 경고 ({minute}')",
                    "{playerName} yellow card ({minute}')"
                ),
                E(
                    "match_red_card_fmt",
                    "{playerName} 퇴장! ({minute}')",
                    "{playerName} sent off! ({minute}')"
                ),
                E(
                    "match_second_yellow_fmt",
                    "{playerName} 두 번째 경고 퇴장! ({minute}')",
                    "{playerName} second yellow, sent off! ({minute}')"
                ),
                // 파울 / 세트피스
                E(
                    "match_foul_fmt",
                    "{foulerName}이(가) {fouledName}에게 파울 ({minute}')",
                    "{foulerName} fouls {fouledName} ({minute}')"
                ),
                E("match_free_kick_fmt", "프리킥 ({minute}')", "Free kick ({minute}')"),
                E("match_corner_fmt", "코너킥 ({minute}')", "Corner ({minute}')"),
                E("match_long_throw_fmt", "롱스로인 ({minute}')", "Long throw ({minute}')"),
                E("match_extra_time_kickoff", "연장전 시작", "Extra time kick-off"),
                E("match_extra_time_halftime", "연장 전반 종료", "Extra time half time"),
                E("match_extra_time_end", "연장전 종료 — 승부차기", "Extra time end — penalties"),
                E(
                    "match_penalty_shootout_goal_fmt",
                    "{playerName} 승부차기 성공 ({minute}')",
                    "{playerName} scores penalty ({minute}')"
                ),
                E(
                    "match_penalty_shootout_miss_fmt",
                    "{playerName} 승부차기 실패 ({minute}')",
                    "{playerName} misses penalty ({minute}')"
                ),
                // 드리블 / 태클
                E(
                    "match_dribble_fmt",
                    "{playerName} 드리블 돌파 ({minute}')",
                    "{playerName} dribbles past ({minute}')"
                ),
                // 부상
                E(
                    "match_injury_fmt",
                    "{playerName} 부상! ({minute}')",
                    "{playerName} injured! ({minute}')"
                ),
                // 교체
                E(
                    "match_substitution_fmt",
                    "{playerIn} IN / {playerOut} OUT ({minute}')",
                    "{playerIn} ON / {playerOut} OFF ({minute}')"
                ),
                // MatchReport 헤드라인 — OFM headline.{outcome}.{variant} 패턴 (3 변형)
                E(
                    "match_report_win_headline_0",
                    "승리! {homeTeam} {homeScore}:{awayScore} {awayTeam}",
                    "Victory! {homeTeam} {homeScore}:{awayScore} {awayTeam}"
                ),
                E(
                    "match_report_win_headline_1",
                    "{homeTeam}, {homeScore}:{awayScore} 완승",
                    "{homeTeam} win {homeScore}:{awayScore}"
                ),
                E(
                    "match_report_win_headline_2",
                    "{homeTeam} {homeScore}:{awayScore} {awayTeam} — 값진 승점 3",
                    "{homeTeam} {homeScore}:{awayScore} {awayTeam} — crucial three points"
                ),
                E(
                    "match_report_loss_headline_0",
                    "패배... {homeTeam} {homeScore}:{awayScore} {awayTeam}",
                    "Defeat... {homeTeam} {homeScore}:{awayScore} {awayTeam}"
                ),
                E(
                    "match_report_loss_headline_1",
                    "{homeTeam} {homeScore}:{awayScore} {awayTeam}에 완패",
                    "{homeTeam} lose {homeScore}:{awayScore} to {awayTeam}"
                ),
                E(
                    "match_report_loss_headline_2",
                    "실망스러운 패배, {homeTeam} {homeScore}:{awayScore} {awayTeam}",
                    "Disappointing loss, {homeTeam} {homeScore}:{awayScore} {awayTeam}"
                ),
                E(
                    "match_report_draw_headline_0",
                    "무승부, {homeTeam} {homeScore}:{awayScore} {awayTeam}",
                    "Draw, {homeTeam} {homeScore}:{awayScore} {awayTeam}"
                ),
                E(
                    "match_report_draw_headline_1",
                    "{homeTeam} {homeScore}:{awayScore} {awayTeam} — 승점 1 획득",
                    "{homeTeam} {homeScore}:{awayScore} {awayTeam} — a point each"
                ),
                E(
                    "match_report_draw_headline_2",
                    "팽팽한 접전 끝 무승부, {homeTeam} {homeScore}:{awayScore} {awayTeam}",
                    "Hard-fought draw, {homeTeam} {homeScore}:{awayScore} {awayTeam}"
                ),
                E(
                    "match_report_possession_fmt",
                    "점유율: 홈 {homePct}% 원정 {awayPct}%",
                    "Possession: Home {homePct}% Away {awayPct}%"
                ),
                E(
                    "match_report_shots_fmt",
                    "슈팅: 홈 {homeShots} 원정 {awayShots}",
                    "Shots: Home {homeShots} Away {awayShots}"
                ),
                E(
                    "match_report_shots_on_target_fmt",
                    "유효슛: 홈 {homeOnTarget} 원정 {awayOnTarget}",
                    "Shots on Target: Home {homeOnTarget} Away {awayOnTarget}"
                ),
                // ── V0.5 M.5 보드 약속 ────────────────────────────────────
                E(
                    "board_promise_transfer_in_desc",
                    "이사회에서 {0} 포지션 선수 영입을 요구합니다.\n수락하면 여름 이적 시장 종료까지 이행해야 합니다.",
                    "The board demands you sign a {0}.\nYou must fulfil this before the summer transfer window closes."
                ),
                E("board_promise_accept", "수락", "Accept"),
                E("board_promise_reject", "거절 (-10 신뢰도)", "Reject (-10 confidence)"),
            };

        // ─────────────────────────────────────────────────────────────
        // V1.0 — Match Event Texts (~100 keys, algorithms.md V1.0-2)
        // 1차 텍스트. G.6 (Stage G.6) 에서 다듬기.
        // ─────────────────────────────────────────────────────────────

        private static List<LocalizationEntry> BuildV10MatchEvents() =>
            new List<LocalizationEntry>
            {
                // ── Goal (5) ─────────────────────────────────────────
                E("match_event_goal_1", "{player}의 환상적인 슈팅! 골인!",
                    "{player} unleashes a stunning strike — and it's in!"),
                E("match_event_goal_2", "행운의 굴절 — {player}의 골!",
                    "A lucky deflection — and {player} gets the goal!"),
                E("match_event_goal_3", "{player}가 PK를 침착하게 성공!",
                    "{player} keeps his composure from the spot!"),
                E("match_event_goal_4", "{player}의 헤더 결정타!",
                    "{player} powers in the header!"),
                E("match_event_goal_5", "{player}의 장거리 폭격이 골망을 흔든다!",
                    "{player} unleashes a thunderbolt from distance!"),

                // ── KeyPass / Assist (5) ─────────────────────────────
                E("match_event_keypass_1", "{player}의 절묘한 스루패스!",
                    "A defence-splitting pass from {player}!"),
                E("match_event_keypass_2", "{player}가 박스 안으로 정확한 크로스!",
                    "{player} whips in a perfect cross!"),
                E("match_event_keypass_3", "{player}의 환상적인 어시스트!",
                    "A sublime assist from {player}!"),
                E("match_event_keypass_4", "{player}의 백힐로 찬스 메이킹!",
                    "{player} backheels it into space!"),
                E("match_event_keypass_5", "{player}의 노룩 패스가 찬스를 만든다!",
                    "{player} with a no-look pass to create the chance!"),

                // ── Save (5) ─────────────────────────────────────────
                E("match_event_save_1", "{gk}의 슈퍼 세이브!",
                    "Brilliant save by {gk}!"),
                E("match_event_save_2", "{gk}가 가까스로 펀칭!",
                    "{gk} punches it clear at full stretch!"),
                E("match_event_save_3", "{gk}의 다이빙 캐치!",
                    "{gk} dives to gather it safely!"),
                E("match_event_save_4", "{gk}가 발로 막아냈다!",
                    "{gk} sticks out a foot to deny the strike!"),
                E("match_event_save_5", "{gk}의 반사신경이 빛난다!",
                    "Lightning reflexes from {gk}!"),

                // ── Shot On Target (4) ───────────────────────────────
                E("match_event_shoton_1", "{player}의 슛이 골키퍼 정면!",
                    "{player}'s shot is straight at the keeper."),
                E("match_event_shoton_2", "{player}의 슛, 골대 맞고 튕겨나온다!",
                    "{player} hits the woodwork!"),
                E("match_event_shoton_3", "{player}의 강슛, 골키퍼가 쳐낸다!",
                    "{player}'s fierce strike — pushed away!"),
                E("match_event_shoton_4", "{player}의 슛이 골라인 위에서 클리어!",
                    "Cleared off the line from {player}'s effort!"),

                // ── Shot Off Target (3) ──────────────────────────────
                E("match_event_shotoff_1", "{player}의 슛이 크로스바를 살짝 넘긴다!",
                    "{player}'s shot flies just over the bar!"),
                E("match_event_shotoff_2", "{player}의 슛이 옆그물!",
                    "{player} sends it wide of the post!"),
                E("match_event_shotoff_3", "{player}의 위협적인 슛이 빗나간다!",
                    "{player}'s effort goes narrowly wide!"),

                // ── Yellow Card (3) ──────────────────────────────────
                E("match_event_yellow_1", "{player}, 거친 태클로 경고!",
                    "{player} is booked for a reckless challenge!"),
                E("match_event_yellow_2", "{player}, 항의로 옐로카드!",
                    "{player} sees yellow for dissent."),
                E("match_event_yellow_3", "{player}, 시간 지연으로 경고!",
                    "{player} is cautioned for time-wasting."),

                // ── Red Card (3) ─────────────────────────────────────
                E("match_event_red_1", "{player}, 폭력 행위로 퇴장!",
                    "{player} is sent off for violent conduct!"),
                E("match_event_red_2", "{player}, 명백한 득점 기회 저지로 퇴장!",
                    "{player} is shown red — a clear goal-scoring opportunity denied."),
                E("match_event_red_3", "{player}, 심각한 반칙으로 퇴장!",
                    "{player} is dismissed for a serious foul!"),

                // ── Second Yellow → Red (2) ──────────────────────────
                E("match_event_2nd_yellow_1", "{player}, 두 번째 경고 — 퇴장!",
                    "Two yellows — {player} is off!"),
                E("match_event_2nd_yellow_2", "{player}가 어리석은 반칙으로 두 번째 옐로!",
                    "A foolish foul earns {player} a second yellow!"),

                // ── Foul (3) ─────────────────────────────────────────
                E("match_event_foul_1", "{player}가 반칙을 범한다.",
                    "{player} concedes a foul."),
                E("match_event_foul_2", "{player}의 격한 태클로 휘슬!",
                    "Whistle blows — {player} with a robust tackle."),
                E("match_event_foul_3", "{player}, 위험 지역에서 반칙!",
                    "{player} fouls in a dangerous area!"),

                // ── Penalty Won (3) ──────────────────────────────────
                E("match_event_pk_won_1", "{player}가 박스 안에서 넘어진다 — PK!",
                    "{player} goes down in the box — penalty!"),
                E("match_event_pk_won_2", "심판이 가리킨 곳은 페널티 스폿! {player} 가 얻어냈다.",
                    "The referee points to the spot — {player} has won the penalty!"),
                E("match_event_pk_won_3", "VAR 확인 결과 PK 판정! {player}.",
                    "After a VAR check, a penalty is awarded to {player}!"),

                // ── Penalty Missed (3) ───────────────────────────────
                E("match_event_pk_miss_1", "{player}의 PK가 골대를 맞춘다!",
                    "{player} smashes the penalty against the post!"),
                E("match_event_pk_miss_2", "{player}의 PK가 크로스바를 넘긴다!",
                    "{player} skies the penalty over the bar!"),
                E("match_event_pk_miss_3", "{player}의 PK가 옆그물로 빗나간다!",
                    "{player} drags the penalty wide!"),

                // ── Penalty Saved (3) ────────────────────────────────
                E("match_event_pk_saved_1", "{gk}의 환상적인 PK 선방!",
                    "{gk} makes a stunning penalty save!"),
                E("match_event_pk_saved_2", "{gk}가 정확히 방향을 읽었다 — 선방!",
                    "{gk} guesses right and saves the penalty!"),
                E("match_event_pk_saved_3", "{gk}가 발로 PK를 막아낸다!",
                    "{gk} blocks the spot-kick with his legs!"),

                // ── Injury Minor (3) ─────────────────────────────────
                E("match_event_injury_minor_1", "{player}, 가벼운 부상 — 자가 치료 가능.",
                    "{player} picks up a knock — should be fine to continue."),
                E("match_event_injury_minor_2", "{player}, 잠시 그라운드에 누웠지만 곧 일어선다.",
                    "{player} is down briefly but back on his feet."),
                E("match_event_injury_minor_3", "{player}, 경미한 부상으로 치료 받는다.",
                    "{player} receives some quick treatment on the pitch."),

                // ── Injury Major (3) ─────────────────────────────────
                E("match_event_injury_major_1", "{player}, 심각한 부상으로 들것에 실려나간다.",
                    "{player} is stretchered off with a serious injury."),
                E("match_event_injury_major_2", "{player}, 출전 불가 — 즉시 교체!",
                    "{player} can't continue — immediate substitution!"),
                E("match_event_injury_major_3", "{player}, 무릎을 잡고 쓰러진다 — 심각한 모습.",
                    "{player} clutches his knee — this looks serious."),

                // ── Substitution (4) ─────────────────────────────────
                E("match_event_sub_1", "교체: {playerOut} → {playerIn}",
                    "Substitution: {playerIn} replaces {playerOut}."),
                E("match_event_sub_2", "{playerOut}가 벤치로, {playerIn}가 투입된다.",
                    "{playerOut} comes off, {playerIn} comes on."),
                E("match_event_sub_3", "전술 변화 — {playerIn} 투입!",
                    "Tactical change — {playerIn} is brought on!"),
                E("match_event_sub_4", "{playerOut}가 박수를 받으며 교체된다. {playerIn} 등장.",
                    "{playerOut} receives an ovation as he is replaced by {playerIn}."),

                // ── Tackle (3) ───────────────────────────────────────
                E("match_event_tackle_1", "{player}의 강력한 태클로 공 탈취!",
                    "{player} wins the ball with a strong tackle!"),
                E("match_event_tackle_2", "{player}, 슬라이딩 태클 성공!",
                    "{player} slides in to win the ball!"),
                E("match_event_tackle_3", "{player}의 깔끔한 태클로 공격 차단!",
                    "{player} dispossesses the attacker cleanly!"),

                // ── Cross (3) ────────────────────────────────────────
                E("match_event_cross_1", "{player}의 크로스가 박스를 가른다!",
                    "{player} whips in a dangerous cross!"),
                E("match_event_cross_2", "{player}의 얼리 크로스!",
                    "An early cross from {player}!"),
                E("match_event_cross_3", "{player}의 크로스, 수비수에게 차단!",
                    "{player}'s cross is cleared by the defender."),

                // ── Corner (3) ───────────────────────────────────────
                E("match_event_corner_1", "코너킥 — {player} 가 준비한다.",
                    "Corner — {player} steps up to take it."),
                E("match_event_corner_2", "짧은 코너 — 박스 외곽으로 빠진다.",
                    "Short corner — played back to the edge of the area."),
                E("match_event_corner_3", "코너에서 헤더 시도!",
                    "Header attempt from the corner!"),

                // ── Free Kick Direct (4) ─────────────────────────────
                E("match_event_fk_direct_1", "직접 프리킥 — {player} 가 준비.",
                    "Direct free kick — {player} stands over it."),
                E("match_event_fk_direct_2", "{player}의 프리킥이 벽을 넘긴다!",
                    "{player}'s free kick clears the wall!"),
                E("match_event_fk_direct_3", "{player}의 프리킥이 벽에 막힌다.",
                    "{player}'s free kick is blocked by the wall."),
                E("match_event_fk_direct_4", "{player}의 환상적인 감아차기 프리킥!",
                    "A beautiful curling free kick from {player}!"),

                // ── Free Kick Indirect (3) ───────────────────────────
                E("match_event_fk_indirect_1", "간접 프리킥 — 짧게 연결.",
                    "Indirect free kick — played short."),
                E("match_event_fk_indirect_2", "{player}가 박스 안으로 띄운다.",
                    "{player} lofts it into the box."),
                E("match_event_fk_indirect_3", "간접 프리킥에서 헤더 찬스!",
                    "Header chance from the indirect free kick!"),

                // ── Long Throw (3) ───────────────────────────────────
                E("match_event_throw_1", "{player}의 롱 스로인이 박스로!",
                    "{player} hurls in a long throw!"),
                E("match_event_throw_2", "{player}의 스로인에서 혼전 발생!",
                    "Scramble in the box from {player}'s long throw!"),
                E("match_event_throw_3", "{player}의 스로인, 수비수가 헤더로 처리.",
                    "{player}'s throw is headed clear by the defender."),

                // ── Offside (3) ──────────────────────────────────────
                E("match_event_offside_1", "오프사이드 깃발이 올라간다 — {player}.",
                    "Flag is up — offside on {player}."),
                E("match_event_offside_2", "{player}가 아슬아슬하게 오프사이드!",
                    "{player} caught marginally offside!"),
                E("match_event_offside_3", "라인을 너무 일찍 깬 {player}.",
                    "{player} times his run too early."),

                // ── Interception (3) ─────────────────────────────────
                E("match_event_interception_1", "{player}의 정확한 인터셉트!",
                    "Brilliant interception from {player}!"),
                E("match_event_interception_2", "{player}가 패스 길을 읽어낸다!",
                    "{player} reads the pass perfectly!"),
                E("match_event_interception_3", "{player}, 공을 가로채 역습!",
                    "{player} picks it off and launches a counter!"),

                // ── Kick Off (4) ─────────────────────────────────────
                E("match_event_kickoff_1", "킥오프! 경기가 시작된다.",
                    "Kick-off! The match is underway."),
                E("match_event_kickoff_2", "후반전 시작!",
                    "Second half is underway!"),
                E("match_event_kickoff_3", "연장 전반전 시작!",
                    "First half of extra time begins!"),
                E("match_event_kickoff_4", "연장 후반전 시작!",
                    "Second half of extra time begins!"),

                // ── Half Time (3) ────────────────────────────────────
                E("match_event_halftime_1", "전반 종료 휘슬.",
                    "Whistle blows for half-time."),
                E("match_event_halftime_2", "휘슬 — 라커룸으로 향한다.",
                    "Half-time — teams head to the dressing room."),
                E("match_event_halftime_3", "전반 종료 — 점수는 {homeScore}-{awayScore}.",
                    "Half-time — score is {homeScore}-{awayScore}."),

                // ── Full Time (4) ────────────────────────────────────
                E("match_event_fulltime_1", "경기 종료!",
                    "Full time!"),
                E("match_event_fulltime_2", "최종 휘슬 — {homeScore}-{awayScore}.",
                    "Final whistle — {homeScore}-{awayScore}."),
                E("match_event_fulltime_3", "경기 종료 — 결과 확정.",
                    "It's all over — the result is final."),
                E("match_event_fulltime_4", "마지막 휘슬이 울린다.",
                    "The final whistle blows."),

                // ── Extra Time Start (2) ─────────────────────────────
                E("match_event_et_start_1", "연장전 돌입!",
                    "Heading to extra time!"),
                E("match_event_et_start_2", "정규 시간 무승부 — 연장전 시작.",
                    "Level after 90 — extra time it is."),

                // ── Penalty Shootout (3) ─────────────────────────────
                E("match_event_pso_1", "승부차기 — 운명의 순간.",
                    "Penalty shootout — the moment of truth."),
                E("match_event_pso_2", "승부차기 시작!",
                    "The shootout begins!"),
                E("match_event_pso_3", "{player}가 페널티 스폿으로 향한다.",
                    "{player} walks up to the spot."),

                // ── Match Report — Win (5) ───────────────────────────
                E("match_report_win_1", "{home}, {away} 상대로 {homeScore}-{awayScore} 완승!",
                    "{home} cruise past {away} {homeScore}-{awayScore}!"),
                E("match_report_win_2", "{home}의 압도적 승리, {away} 상대 {homeScore}-{awayScore}.",
                    "Dominant {home} win {homeScore}-{awayScore} against {away}."),
                E("match_report_win_3", "{home}, {away}에 {homeScore}-{awayScore} 승.",
                    "{home} beat {away} {homeScore}-{awayScore}."),
                E("match_report_win_4", "치열한 접전 끝에 {home}이 {away}를 꺾었다.",
                    "{home} edge {away} in a tight contest."),
                E("match_report_win_5", "{home}의 결정적인 골들이 {away}를 침몰시켰다.",
                    "Clinical finishing from {home} sinks {away}."),

                // ── Match Report — Loss (5) ──────────────────────────
                E("match_report_loss_1", "{home}, {away}에 {homeScore}-{awayScore} 패.",
                    "{home} fall {homeScore}-{awayScore} to {away}."),
                E("match_report_loss_2", "{away}의 일격에 {home}이 무릎 꿇었다.",
                    "{home} undone by a sharp {away} performance."),
                E("match_report_loss_3", "{home}의 마무리 부족이 {away}에 점수를 내줬다.",
                    "Profligate finishing costs {home} the points against {away}."),
                E("match_report_loss_4", "{home}, 안방에서 {away}에 무릎 꿇다.",
                    "{home} stunned at home by {away}."),
                E("match_report_loss_5", "{away}의 효율적인 경기 운영이 {home}을 압도했다.",
                    "Efficient {away} prove too much for {home}."),

                // ── Match Report — Draw (5) ──────────────────────────
                E("match_report_draw_1", "{home}과 {away}, {homeScore}-{awayScore} 무승부.",
                    "{home} and {away} share the spoils {homeScore}-{awayScore}."),
                E("match_report_draw_2", "{home} vs {away}, 점수만큼 치열한 무승부.",
                    "An evenly-matched draw between {home} and {away}."),
                E("match_report_draw_3", "양 팀 모두 결정타 부족 — 무승부로 마무리.",
                    "Neither side could find the winner — a stalemate."),
                E("match_report_draw_4", "{home} vs {away}, 박빙의 무승부.",
                    "{home} and {away} cancel each other out."),
                E("match_report_draw_5", "한 점도 양보 없는 끝장 무승부.",
                    "A back-and-forth draw with both sides giving nothing."),
            };

        // ─────────────────────────────────────────────────────────────
        // V1.0 — Options + Inbox keys
        // ─────────────────────────────────────────────────────────────

        private static List<LocalizationEntry> BuildV10OptionsAndInbox() =>
            new List<LocalizationEntry>
            {
                // ── Options (라벨) ───────────────────────────────────
                E("options_title", "옵션", "Options"),
                E("options_master_volume", "마스터 볼륨", "Master Volume"),
                E("options_sfx_volume", "효과음 볼륨", "SFX Volume"),
                E("options_bgm_volume", "배경음악 볼륨", "BGM Volume"),
                E("options_language", "언어", "Language"),
                E("options_language_ko", "한국어", "Korean"),
                E("options_language_en", "English", "English"),
                E("options_currency", "통화", "Currency"),
                E("options_ui_scale", "UI 크기", "UI Scale"),
                E("options_auto_save", "자동 저장", "Auto Save"),
                E("options_shortcuts", "단축키 안내", "Shortcuts"),
                E("options_on", "켜기", "On"),
                E("options_off", "끄기", "Off"),
                E("options_save_apply", "저장", "Save"),

                // ── Inbox 카테고리 (7) ───────────────────────────────
                E("inbox_category_match", "경기", "Match"),
                E("inbox_category_transfer", "이적", "Transfer"),
                E("inbox_category_morale", "사기", "Morale"),
                E("inbox_category_board", "이사회", "Board"),
                E("inbox_category_youth", "유스", "Youth"),
                E("inbox_category_cup", "컵", "Cup"),
                E("inbox_category_award", "시상", "Award"),

                // ── Inbox 우선순위 (4) ───────────────────────────────
                E("inbox_priority_low", "낮음", "Low"),
                E("inbox_priority_medium", "보통", "Medium"),
                E("inbox_priority_high", "높음", "High"),
                E("inbox_priority_requires_action", "처리 필요", "Requires Action"),

                // ── Inbox 알림 fmt (V1.0 신규 — V0.5 기존 5개는 중복 회피 위해 제외) ─
                // V0.5 기존 (positional placeholder 유지, DashboardController 호환):
                //   inbox_promise_created_fmt / _fulfilled_fmt / _broken_fmt
                //   inbox_transfer_request_fmt / inbox_youth_promotion_fmt
                // V1.0 InboxRouter 는 Dictionary<string,string> titleArgs 만 저장,
                // string.Format 은 InboxPanel UI (Stage B.1) 책임 — 텍스트 형식 호환성 분리.
                E(
                    "inbox_promise_deadline_fmt",
                    "[약속] {0} — {1} 마감 {2}일 남음",
                    "[Promise] {0} — {1} due in {2} days"
                ),
                E(
                    "inbox_counter_offer_fmt",
                    "[역제안] {0} 영입 — 역제안 도착",
                    "[Counter-offer] {0} — counter-offer received"
                ),
                E(
                    "inbox_personal_negotiation_fmt",
                    "[개인협상] {0} 영입 — 구단 합의, 개인 조건 협상 필요",
                    "[Personal Terms] {0} — club agreed, negotiate personal terms"
                ),
                E(
                    "inbox_offer_accepted_fmt",
                    "[오퍼 수락] {0} 영입 — 구단 합의, 이적창 열리면 성사",
                    "[Offer Accepted] {0} — agreed, completes when window opens"
                ),
                E(
                    "inbox_offer_rejected_fmt",
                    "[오퍼 거절] {0} 영입 — 협상 결렬",
                    "[Offer Rejected] {0} — negotiation fell through"
                ),
                E(
                    "inbox_contract_renewed_fmt",
                    "[재계약] {0} 재계약 체결",
                    "[Renewal] {0} has signed a new contract"
                ),
                E(
                    "inbox_contract_rejected_fmt",
                    "[재계약 거절] {0} 재계약 거절",
                    "[Rejected] {0} rejected the contract offer"
                ),
                E(
                    "inbox_youth_intake_fmt",
                    "[유스] {0} 유스 인스펙션 가능",
                    "[Youth] {0} youth intake available"
                ),

                // ── Player Negotiation (개인 조건 협상, #469) ─────────
                E("pnego_title", "개인 조건 협상", "Personal Terms"),
                E("pnego_empty", "진행 중인 개인 협상이 없습니다", "No ongoing personal negotiations"),
                E("pnego_status_negotiating", "개인협상", "Negotiating"),
                E("pnego_terms_title_fmt", "{0} 개인 조건 협상", "{0} — Personal Terms"),
                E("pnego_wage_label", "주급", "Weekly Wage"),
                E("pnego_years_label", "계약 기간(년)", "Contract Years"),
                E("pnego_playtime_label", "출전 시간 약속", "Playtime Promise"),
                E("pnego_propose", "제안", "Propose"),
                E("pnego_wage_caption_fmt", "주급 {0} / 주", "Wage {0} / week"),
                E("pnego_reaction_happy", "선수가 만족스러워합니다", "The player is pleased"),
                E("pnego_reaction_think", "선수가 고민 중입니다", "The player is undecided"),
                E("pnego_reaction_unhappy", "선수가 불만족스러워합니다", "The player is unhappy"),
                E(
                    "pnego_result_accepted",
                    "합의 성사! 이적창이 열리면 영입이 완료됩니다.",
                    "Agreed! The transfer completes when the window opens."
                ),
                E(
                    "pnego_result_rejected",
                    "선수가 조건을 거부했습니다 — 협상 결렬.",
                    "The player rejected the terms — negotiation broke down."
                ),
                E(
                    "pnego_result_still_fmt",
                    "선수가 망설입니다 (R{0}/{1}) — 조건을 올려 다시 제안해 보세요.",
                    "The player hesitates (R{0}/{1}) — improve the terms and propose again."
                ),

                // ── Negotiation (구단 이적료 협상 / 역제안) ───────────
                E("negotiation_counter_title_fmt", "{0} 이적료 협상", "{0} — Fee Negotiation"),
                E(
                    "negotiation_counter_detail_fmt",
                    "내 제안 {0} → 구단 역제안 {1}  (라운드 {2})",
                    "Your bid {0} → club counter {1}  (Round {2})"
                ),
                E("negotiation_accepted_title_fmt", "{0} 이적료 합의", "{0} — Fee Agreed"),
                E(
                    "negotiation_accepted_detail_fmt",
                    "구단이 {0} 에 합의했습니다. 수락 시 개인 조건 협상으로 진행합니다.",
                    "The club agreed to {0}. Accept to proceed to personal terms."
                ),
                E("negotiation_status_counter", "역제안", "Counter"),
                E("negotiation_status_ai_accepted", "수락", "Accepted"),
                E("negotiation_status_pending", "대기 중", "Pending"),
                E("negotiation_status_negotiating", "개인협상", "Negotiating"),
                E("negotiation_status_accepted", "합의", "Agreed"),

                // ── Inbox UI ─────────────────────────────────────────
                E("inbox_title", "인박스", "Inbox"),
                E("inbox_empty", "받은 알림이 없습니다.", "No notifications."),
                E("inbox_mark_all_read", "모두 읽음 처리", "Mark all as read"),
                E("inbox_open", "열기", "Open"),
                // 행 기한 표시 (InboxEntryView, Stage B.1) — 전체 탭은 filter_all 재사용
                E("inbox_deadline_days_fmt", "기한 D-{0}", "Due D-{0}"),
                E("inbox_deadline_expired", "기한 만료", "Expired"),
            };

        // ─────────────────────────────────────────────────────────────
        // V1.0 — Currency / Synergy / FA Cup / Training keys
        // ─────────────────────────────────────────────────────────────

        private static List<LocalizationEntry> BuildV10CurrencySynergyCupTraining() =>
            new List<LocalizationEntry>
            {
                // ── Currency 심볼 + 단위 ─────────────────────────────
                E("currency_gbp_symbol", "£", "£"),
                E("currency_usd_symbol", "$", "$"),
                E("currency_eur_symbol", "€", "€"),
                E("currency_krw_symbol", "₩", "₩"),
                E("currency_unit_thousand", "K", "K"),
                E("currency_unit_million", "M", "M"),
                E("currency_unit_billion", "B", "B"),
                E("currency_label_gbp", "파운드 (GBP)", "Pound (GBP)"),
                E("currency_label_usd", "달러 (USD)", "Dollar (USD)"),
                E("currency_label_eur", "유로 (EUR)", "Euro (EUR)"),
                E("currency_label_krw", "원 (KRW)", "Won (KRW)"),

                // ── 시너지 카탈로그 (10종, algorithms.md V1.0-3) ──
                E("synergy_big_and_small_name", "빅앤스몰", "Big & Small"),
                E("synergy_big_and_small_desc", "장신 스트라이커 + 단신 윙어 조합. 헤더골 +10% / 크로스 결정 +10%.",
                    "Tall striker paired with short wingers. +10% headed goals / +10% cross finishing."),

                E("synergy_target_speedster_name", "타겟+발마니", "Target & Speedster"),
                E("synergy_target_speedster_desc", "타겟맨 스트라이커 + 빠른 윙어. 카운터 어택 +15%.",
                    "Target Man with pacy wingers. +15% counter attacks."),

                E("synergy_possession_name", "온볼이마이웨이", "Possession"),
                E("synergy_possession_desc", "패스 + 비전 좋은 미드필더 2명. 점유율 +5% / 패스 성공 +5%.",
                    "Two midfielders with elite passing and vision. +5% possession / +5% pass success."),

                E("synergy_defensive_wall_name", "골니아", "Defensive Wall"),
                E("synergy_defensive_wall_desc", "센터백 2명의 강력한 태클·마킹. 실점 -10%.",
                    "Two centre-backs locking down the area. -10% goals conceded."),

                E("synergy_wingback_duo_name", "서프-스테파", "Wing-Back Duo"),
                E("synergy_wingback_duo_desc", "스태미나·근면성 좋은 양쪽 풀백. 크로스 빈도 +20%.",
                    "High-stamina and hard-working full-backs. +20% cross frequency."),

                E("synergy_double_pivot_name", "더블 피보테", "Double Pivot"),
                E("synergy_double_pivot_desc", "수비형 미드필더 2명. 중원 점유 +10% / 차단 +10%.",
                    "Two defensive midfielders. +10% midfield control / +10% interceptions."),

                E("synergy_trequartista_name", "트레자르테", "Trequartista"),
                E("synergy_trequartista_desc", "공격형 미드필더의 창의성. 키패스 빈도 +20%.",
                    "Creative attacking midfielder. +20% key pass frequency."),

                E("synergy_false_nine_name", "펄스9", "False 9"),
                E("synergy_false_nine_desc", "거짓 9번의 박스 침투. 드리블 박스 진입 +15%.",
                    "False 9 dropping deep and arriving late. +15% dribble box entries."),

                E("synergy_diamond_midfield_name", "다이아몬드 미드", "Diamond Midfield"),
                E("synergy_diamond_midfield_desc", "DM + CM 2 + AM 다이아몬드 형태. 점유 +8% / 슛 +10%.",
                    "DM + 2 CM + AM diamond shape. +8% possession / +10% shots."),

                E("synergy_homegrown_spine_name", "자국인 라인", "Homegrown Spine"),
                E("synergy_homegrown_spine_desc", "GK + CB + DM + ST 자국 선수. 사기 +5 영구 / 매치 +3%.",
                    "GK + CB + DM + ST all homegrown. +5 permanent morale / +3% match strength."),

                E("synergy_active_label", "활성 시너지", "Active Synergies"),
                E("synergy_none_active", "활성 시너지 없음", "No active synergies"),

                // ── H.5 매치업 / Duty (H.3 이월) ─────────────────────
                E("matchup_label", "전술 상성", "Formation Matchup"),
                E("matchup_even", "대등", "Even"),
                E("duty_attack", "공격", "Attack"),
                E("duty_support", "지원", "Support"),
                E("duty_defend", "수비", "Defend"),
                E("duty_attack_short", "공", "A"),
                E("duty_support_short", "지", "S"),
                E("duty_defend_short", "수", "D"),

                // ── FA Cup ──────────────────────────────────────────
                E("cup_facup_name", "FA컵", "FA Cup"),
                E("cup_round_32", "32강", "Round of 32"),
                E("cup_round_16", "16강", "Round of 16"),
                E("cup_quarter", "8강", "Quarter-finals"),
                E("cup_semi", "4강", "Semi-finals"),
                E("cup_final", "결승", "Final"),
                E("cup_winner_fmt", "{club}이(가) {season} 시즌 FA컵 우승!",
                    "{club} win the {season} FA Cup!"),
                E("cup_match_label_fmt", "FA컵 {round}", "FA Cup {round}"),
                E("cup_eliminated_fmt", "{club}, FA컵 {round} 탈락",
                    "{club} eliminated in FA Cup {round}"),

                // ── 훈련 시스템 (V1.0-4) ─────────────────────────────
                E("training_title", "훈련", "Training"),
                E("training_group", "그룹 훈련", "Group Training"),
                E("training_individual", "개인 훈련", "Individual Training"),
                E("training_intensity_low", "낮음", "Low"),
                E("training_intensity_medium", "보통", "Medium"),
                E("training_intensity_high", "높음", "High"),
                E("training_group_gk", "GK", "GK"),
                E("training_group_df", "수비", "Defence"),
                E("training_group_mf", "미드필드", "Midfield"),
                E("training_group_at", "공격", "Attack"),
                E("training_individual_target", "타겟 스탯", "Target Stat"),
                E("training_individual_start", "시작일", "Start Date"),
                E("training_individual_end", "종료일", "End Date"),
                E("training_individual_capacity_fmt", "동시 인원 {used}/{cap}",
                    "Active trainees: {used}/{cap}"),
                E("training_individual_full", "훈련 인원 초과 — 시설 업그레이드 필요",
                    "Training capacity full — upgrade facility"),
                E("training_button_start", "훈련 시작", "Start Training"),
                E("training_button_cancel", "취소", "Cancel"),
            };

        // ── Stage C — PlayerProfile stat 등급 / 신체 조건 (#455) ───────────────
        private static List<LocalizationEntry> BuildV10PlayerProfile() =>
            new List<LocalizationEntry>
            {
                // 등급명 (C.2 — 색상 코딩 툴팁)
                E("stat_grade_elite", "엘리트 (80+)", "Elite (80+)"),
                E("stat_grade_good", "우수 (65-79)", "Good (65-79)"),
                E("stat_grade_average", "평범 (50-64)", "Average (50-64)"),
                E("stat_grade_weak", "약함 (35-49)", "Weak (35-49)"),
                E("stat_grade_poor", "부족 (-34)", "Poor (≤34)"),
                // 주발 (C.3)
                E("foot_left", "왼발", "Left"),
                E("foot_right", "오른발", "Right"),
                E("foot_both", "양발", "Both"),
                // 신체 조건 (C.3) — 헤더 fmt (구) + FM식 신체 컬럼 행 라벨 (신규)
                E("physical_height_weight_fmt", "{0}cm · {1}kg", "{0}cm · {1}kg"),
                E("physical_weak_foot_fmt", "약발 {0}", "Weak Foot {0}"),
                E("label_height", "키", "Height"),
                E("label_weight", "몸무게", "Weight"),
                E("label_preferred_foot", "주발", "Preferred Foot"),
                E("label_weak_foot", "약발", "Weak Foot"),
                // ── Stage E — YouthManagementScene (#461) ───────────────────
                E("youth_mgmt_title", "유스 관리", "Youth Management"),
                E("youth_inspection_section", "다음 인스펙션", "Next Inspection"),
                E("youth_current_section", "현 유스", "Current Youth"),
                E("youth_callup_section", "1군 콜업 후보", "Call-up Candidates"),
                E("youth_mentoring_section", "멘토링", "Mentoring"),
                E("youth_next_inspection_fmt", "{0} (D-{1})", "{0} (D-{1})"),
                E(
                    "youth_pool_prediction_fmt",
                    "예상 풀 {0}명 · 영입 가능 ~{1}명 (모집 Lv.{2})",
                    "Pool ~{0} · Sign up to {1} (Recruit Lv.{2})"
                ),
                E("youth_count_fmt", "현 유스 {0}명", "Youth Squad: {0}"),
                E("youth_callup_empty", "콜업 후보 없음", "No call-up candidates"),
                E(
                    "youth_mentoring_summary_fmt",
                    "멘토링 그룹 {0} · 멘티 {1}명",
                    "Mentoring Groups {0} · Mentees {1}"
                ),
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
