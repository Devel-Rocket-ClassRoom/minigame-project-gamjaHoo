// DebugWindow.cs
// V0.1 Stage 14 Task 14.1 (#57). Editor 전용 디버그 도구.
//
// 위치: Editor Layer (FMLite.Editor.asmdef, includePlatforms=Editor).
// PlayMode 런타임에서만 동작 — GameManager.Instance.State 가 있어야 함.
//
// 기능 (issue #57 DoD):
//   - 시간 강제 진행 (1일 / 1주 / 1개월) — GameLoop.AdvanceDay 반복
//   - 자금 추가 — userClub.finance.money +=
//   - 리롤 토큰 추가 — state.rerollTokens += (max cap 적용)
//   - 강제 부상 / 회복 — InjuryInfo 직접 조작
//
// 추가 디버그 가치:
//   - 매치 / 시즌 이벤트 구독 (MatchFinished / SeasonEnded / SeasonStarted)
//     → Console 로그. 정상 매치 처리 흐름 시각화 (Application 코어에 Debug.Log 미주입).
//   - 리그 standings 패널.
//
// V1.0+ 확장 — Stage 14 Task 14.2 (#58 isDebugMode 토글) 연결 후
// 능력치 노출 / 시드 표시 / 매치 강제 결과 등 추가.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using UnityEditor;
using UnityEngine;
using UnityApplication = UnityEngine.Application;

namespace FMLite.Editor
{
    public class DebugWindow : EditorWindow
    {
        [MenuItem("FM-Lite/Debug Window")]
        public static void Open() => GetWindow<DebugWindow>("FM-Lite Debug");

        private int _moneyAdd = 100_000;
        private int _tokenAdd = 1;
        private int _injuryPlayerId = 1;
        private int _injuryDays = 30;

        private int _offerPlayerId = 1;
        private int _offerAmount = 1_000_000;
        private int _offerWeeklyWage = 50_000;
        private int _offerYears = 3;

        private Vector2 _scroll;

        // 이벤트 핸들러 참조 (Unsubscribe 위해 필드 보관)
        private Action<MatchFinishedEvent> _onMatchFinished;
        private Action<SeasonEndedEvent> _onSeasonEnded;
        private Action<SeasonStartedEvent> _onSeasonStarted;

        private void OnEnable()
        {
            _onMatchFinished = HandleMatchFinished;
            _onSeasonEnded = e => Debug.Log($"[Debug] SeasonEnded — year {e.seasonYear}");
            _onSeasonStarted = e => Debug.Log($"[Debug] SeasonStarted — year {e.seasonYear}");
            EventBus.Subscribe(_onMatchFinished);
            EventBus.Subscribe(_onSeasonEnded);
            EventBus.Subscribe(_onSeasonStarted);
        }

        private void OnDisable()
        {
            if (_onMatchFinished != null)
                EventBus.Unsubscribe(_onMatchFinished);
            if (_onSeasonEnded != null)
                EventBus.Unsubscribe(_onSeasonEnded);
            if (_onSeasonStarted != null)
                EventBus.Unsubscribe(_onSeasonStarted);
        }

        private void HandleMatchFinished(MatchFinishedEvent e)
        {
            if (e.result == null)
                return;
            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            // matchId 로 schedule 검색 (V0.1 단순 — leagues 적음)
            Match found = null;
            foreach (var l in state.leagues)
            {
                if (l?.schedule == null)
                    continue;
                foreach (var m in l.schedule)
                    if (m != null && m.id == e.matchId)
                    {
                        found = m;
                        break;
                    }
                if (found != null)
                    break;
            }
            if (found == null)
            {
                Debug.Log(
                    $"[Debug] Match #{e.matchId} finished — {e.result.homeScore}:{e.result.awayScore}"
                );
                return;
            }
            var home = state.GetClub(found.homeClubId)?.name ?? "?";
            var away = state.GetClub(found.awayClubId)?.name ?? "?";
            Debug.Log(
                $"[Debug] Match #{e.matchId} {found.date:MM-dd}: {home} {e.result.homeScore}:{e.result.awayScore} {away}"
            );
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FM-Lite Debug Window", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (!UnityApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("PlayMode 에서만 동작합니다.", MessageType.Info);
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null || gm.State == null)
            {
                EditorGUILayout.HelpBox(
                    "GameManager / GameState 미초기화. 새 게임 시작 후 사용하세요.",
                    MessageType.Warning
                );
                return;
            }

            var state = gm.State;
            var balance = GameDatabase.GameBalance;
            if (balance == null)
            {
                EditorGUILayout.HelpBox(
                    "GameBalance asset 누락. Resources/Balance/GameBalance.asset 확인.",
                    MessageType.Error
                );
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawStatus(state);
            EditorGUILayout.Space();
            DrawDebugModeSection(balance);
            EditorGUILayout.Space();
            DrawTimeSection(state, balance);
            EditorGUILayout.Space();
            DrawStandingsSection(state);
            EditorGUILayout.Space();
            DrawUpcomingMatchesSection(state);
            EditorGUILayout.Space();
            DrawInjuredListSection(state);
            EditorGUILayout.Space();
            DrawActiveOffersSection(state);
            EditorGUILayout.Space();
            DrawMoneySection(state);
            EditorGUILayout.Space();
            DrawTokenSection(state, balance);
            EditorGUILayout.Space();
            DrawInjurySection(state);
            EditorGUILayout.Space();
            DrawSubmitOfferSection(state, balance);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawStandingsSection(GameState state)
        {
            EditorGUILayout.LabelField("── 리그 순위 (상위 8) ──", EditorStyles.miniBoldLabel);
            var league = state.leagues.Count > 0 ? state.leagues[0] : null;
            if (league?.standings?.entries == null || league.standings.entries.Count == 0)
            {
                EditorGUILayout.LabelField("(standings 없음)");
                return;
            }
            // 점수 내림차순 정렬 (copy)
            var sorted = new System.Collections.Generic.List<StandingEntry>(
                league.standings.entries
            );
            sorted.Sort(
                (a, b) =>
                {
                    int cmp = b.points.CompareTo(a.points);
                    if (cmp != 0)
                        return cmp;
                    return (b.goalsFor - b.goalsAgainst).CompareTo(a.goalsFor - a.goalsAgainst);
                }
            );
            EditorGUILayout.LabelField(
                "#  Club             P  W D L  GF:GA  Pts",
                EditorStyles.miniLabel
            );
            int top = Math.Min(8, sorted.Count);
            for (int i = 0; i < top; i++)
            {
                var e = sorted[i];
                var name = state.GetClub(e.clubId)?.name ?? $"id={e.clubId}";
                if (name.Length > 14)
                    name = name.Substring(0, 14);
                EditorGUILayout.LabelField(
                    $"{(i + 1), 2} {name, -14} {e.played, 2} {e.won, 2} {e.drawn, 1} {e.lost, 1} "
                        + $"{e.goalsFor, 2}:{e.goalsAgainst, -2} {e.points, 3}",
                    EditorStyles.miniLabel
                );
            }
        }

        // ── 섹션들 ──────────────────────────────────────────────────────

        private void DrawDebugModeSection(GameBalanceSO balance)
        {
            EditorGUILayout.LabelField("── 디버그 모드 ──", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"현재: {(balance.isDebugMode ? "ON (CA/PA 정확 수치 노출)" : "OFF (티어만 표시)")}",
                EditorStyles.miniLabel
            );
            if (GUILayout.Button(balance.isDebugMode ? "OFF 로 전환" : "ON 으로 전환", GUILayout.Width(120)))
            {
                balance.isDebugMode = !balance.isDebugMode;
                EditorUtility.SetDirty(balance);
                Debug.Log($"[Debug] isDebugMode → {balance.isDebugMode}");
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "※ PlayerProfile 화면에서 CA/PA 수치 노출 여부가 달라집니다.",
                EditorStyles.miniLabel
            );
        }

        private static void DrawStatus(GameState state)
        {
            EditorGUILayout.LabelField("── 현재 상태 ──", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"날짜: {state.currentDate:yyyy-MM-dd} ({state.currentDate.DayOfWeek})"
            );
            var userClub = state.GetClub(state.userClubId);
            EditorGUILayout.LabelField(
                $"유저 구단: {userClub?.name ?? "-"} (id={state.userClubId} / rep={userClub?.reputation ?? 0})"
            );
            EditorGUILayout.LabelField($"리롤 토큰: {state.rerollTokens}");
            EditorGUILayout.LabelField(
                $"선수 수: {state.allPlayers.Count} / 구단 수: {state.allClubs.Count} / 활성 오퍼: {state.activeOffers?.Count ?? 0}"
            );
            EditorGUILayout.LabelField(
                $"nextPlayerId: {state.nextPlayerId} / nextIntakeId: {state.nextIntakeId} / nextOfferId: {state.nextOfferId}"
            );

            if (userClub?.finance != null)
            {
                var f = userClub.finance;
                EditorGUILayout.LabelField(
                    $"Finance — money: {f.money:N0} / debt: {f.debt:N0} / 이적예산: {f.transferBudget:N0} / 임금예산: {f.wageBudget:N0}",
                    EditorStyles.miniLabel
                );
            }
        }

        private static void DrawUpcomingMatchesSection(GameState state)
        {
            EditorGUILayout.LabelField("── 다가오는 매치 (5경기) ──", EditorStyles.miniBoldLabel);
            var league = state.leagues.Count > 0 ? state.leagues[0] : null;
            if (league?.schedule == null || league.schedule.Count == 0)
            {
                EditorGUILayout.LabelField("(일정 없음)", EditorStyles.miniLabel);
                return;
            }
            int shown = 0;
            foreach (var m in league.schedule)
            {
                if (m == null)
                    continue;
                if (m.result != null)
                    continue;
                if (m.date.Date < state.currentDate.Date)
                    continue;
                var home = state.GetClub(m.homeClubId)?.name ?? "?";
                var away = state.GetClub(m.awayClubId)?.name ?? "?";
                EditorGUILayout.LabelField(
                    $"#{m.id, 3}  {m.date:MM-dd}  {home} vs {away}",
                    EditorStyles.miniLabel
                );
                if (++shown >= 5)
                    break;
            }
            if (shown == 0)
                EditorGUILayout.LabelField(
                    "(예정 매치 없음 — 시즌 종료 직후일 수 있음)",
                    EditorStyles.miniLabel
                );
        }

        private static void DrawInjuredListSection(GameState state)
        {
            EditorGUILayout.LabelField("── 부상자 명단 ──", EditorStyles.miniBoldLabel);
            int count = 0;
            foreach (var p in state.allPlayers)
            {
                var inj = p?.state?.injury;
                if (inj == null || inj.injuryTypeId == -1)
                    continue;
                var clubName = state.GetClub(p.currentClubId)?.name ?? "(FA)";
                int daysLeft = Math.Max(0, (inj.expectedReturn.Date - state.currentDate.Date).Days);
                EditorGUILayout.LabelField(
                    $"#{p.id, 3} {p.info?.lastName ?? "-"} [{clubName}] "
                        + $"복귀 {inj.expectedReturn:MM-dd} ({daysLeft}일 남음)",
                    EditorStyles.miniLabel
                );
                if (++count >= 12)
                {
                    EditorGUILayout.LabelField("...", EditorStyles.miniLabel);
                    break;
                }
            }
            if (count == 0)
                EditorGUILayout.LabelField("(부상자 없음)", EditorStyles.miniLabel);
        }

        private static void DrawActiveOffersSection(GameState state)
        {
            EditorGUILayout.LabelField("── 활성 이적 오퍼 ──", EditorStyles.miniBoldLabel);
            if (state.activeOffers == null || state.activeOffers.Count == 0)
            {
                EditorGUILayout.LabelField("(오퍼 없음)", EditorStyles.miniLabel);
                return;
            }
            int shown = 0;
            foreach (var o in state.activeOffers)
            {
                if (o == null)
                    continue;
                var playerName = state.GetPlayer(o.playerId)?.info?.lastName ?? $"id={o.playerId}";
                var from = state.GetClub(o.fromClubId)?.name ?? "?";
                var to = state.GetClub(o.toClubId)?.name ?? "?";
                EditorGUILayout.LabelField(
                    $"#{o.id, 3} [{o.status}] {playerName} : {from} → {to} ({o.amount:N0})",
                    EditorStyles.miniLabel
                );
                if (++shown >= 10)
                {
                    EditorGUILayout.LabelField("...", EditorStyles.miniLabel);
                    break;
                }
            }
        }

        private void DrawTimeSection(GameState state, GameBalanceSO balance)
        {
            EditorGUILayout.LabelField("── 시간 진행 ──", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 일"))
                AdvanceDays(state, balance, 1);
            if (GUILayout.Button("+1 주"))
                AdvanceDays(state, balance, 7);
            if (GUILayout.Button("+1 개월"))
                AdvanceDays(state, balance, 30);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMoneySection(GameState state)
        {
            EditorGUILayout.LabelField("── 자금 추가 ──", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _moneyAdd = EditorGUILayout.IntField("금액", _moneyAdd);
            if (GUILayout.Button("유저 구단에 추가", GUILayout.Width(150)))
            {
                var userClub = state.GetClub(state.userClubId);
                if (userClub?.finance == null)
                {
                    Debug.LogWarning("[Debug] userClub 또는 finance 없음 — 구단 선택 후 사용.");
                    return;
                }
                userClub.finance.money += _moneyAdd;
                Debug.Log(
                    $"[Debug] {userClub.name} money += {_moneyAdd:N0} → {userClub.finance.money:N0}"
                );
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTokenSection(GameState state, GameBalanceSO balance)
        {
            EditorGUILayout.LabelField(
                $"── 리롤 토큰 추가 (max {balance.maxRerollStockpile}) ──",
                EditorStyles.miniBoldLabel
            );
            EditorGUILayout.BeginHorizontal();
            _tokenAdd = EditorGUILayout.IntField("토큰 수", _tokenAdd);
            if (GUILayout.Button("추가", GUILayout.Width(150)))
            {
                int before = state.rerollTokens;
                state.rerollTokens = Math.Min(
                    balance.maxRerollStockpile,
                    state.rerollTokens + _tokenAdd
                );
                Debug.Log(
                    $"[Debug] tokens {before} → {state.rerollTokens} (cap {balance.maxRerollStockpile})"
                );
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInjurySection(GameState state)
        {
            EditorGUILayout.LabelField("── 부상 강제 / 회복 ──", EditorStyles.miniBoldLabel);
            _injuryPlayerId = EditorGUILayout.IntField("선수 ID", _injuryPlayerId);
            _injuryDays = EditorGUILayout.IntField("회복 일수", _injuryDays);

            // 선택 선수 정보 자동 조회
            var p = state.GetPlayer(_injuryPlayerId);
            if (p == null)
            {
                EditorGUILayout.LabelField(
                    $"(id={_injuryPlayerId} 선수 없음)",
                    EditorStyles.miniLabel
                );
            }
            else
            {
                int age =
                    p.info != null
                        ? (int)((state.currentDate - p.info.birthDate).TotalDays / 365.25)
                        : 0;
                var clubName = state.GetClub(p.currentClubId)?.name ?? "(FA)";
                EditorGUILayout.LabelField(
                    $"{p.info?.firstName} {p.info?.lastName} / {p.info?.primaryPosition} / age {age} / {clubName}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"CA {p.currentAbility} (PA {p.potentialAbility}) / fatigue {p.state?.fatigue ?? 0} / form {p.state?.form ?? 0}",
                    EditorStyles.miniLabel
                );
                var inj = p.state?.injury;
                string injStr =
                    (inj == null || inj.injuryTypeId == -1)
                        ? "건강"
                        : $"부상 (복귀 {inj.expectedReturn:MM-dd})";
                EditorGUILayout.LabelField($"상태: {injStr}", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("부상 부여"))
                ForceInjury(state, _injuryPlayerId, _injuryDays);
            if (GUILayout.Button("회복"))
                ForceRecover(state, _injuryPlayerId);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSubmitOfferSection(GameState state, GameBalanceSO balance)
        {
            EditorGUILayout.LabelField(
                "── 이적 오퍼 제출 (유저 구단이 영입) ──",
                EditorStyles.miniBoldLabel
            );
            _offerPlayerId = EditorGUILayout.IntField("대상 선수 ID", _offerPlayerId);
            _offerAmount = EditorGUILayout.IntField("이적료", _offerAmount);
            _offerWeeklyWage = EditorGUILayout.IntField("제안 주급", _offerWeeklyWage);
            _offerYears = EditorGUILayout.IntField("계약 기간(년)", _offerYears);

            // 시장가 자동 표시 + 자동 채우기 버튼
            var target = state.GetPlayer(_offerPlayerId);
            if (target != null)
            {
                int mv = TransferSystem.CalculateMarketValue(target, state, balance);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"시장가: {mv:N0} / 소속: {state.GetClub(target.currentClubId)?.name ?? "(FA)"}",
                    EditorStyles.miniLabel
                );
                if (GUILayout.Button("시장가 ×1.2 채우기", GUILayout.Width(140)))
                {
                    _offerAmount = (int)(mv * 1.2f);
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"(id={_offerPlayerId} 선수 없음)",
                    EditorStyles.miniLabel
                );
            }

            if (GUILayout.Button("오퍼 제출"))
            {
                SubmitOffer(state, balance);
            }
        }

        // ── 액션 헬퍼 ───────────────────────────────────────────────────

        private void SubmitOffer(GameState state, GameBalanceSO balance)
        {
            var target = state.GetPlayer(_offerPlayerId);
            if (target == null)
            {
                Debug.LogWarning($"[Debug] player id={_offerPlayerId} 없음");
                return;
            }
            if (state.userClubId <= 0)
            {
                Debug.LogWarning("[Debug] userClubId 미설정 — Bootstrap 의 userClubId 확인");
                return;
            }
            if (target.currentClubId == state.userClubId)
            {
                Debug.LogWarning($"[Debug] player id={_offerPlayerId} 는 이미 유저 구단 소속");
                return;
            }
            if (target.currentClubId < 0)
            {
                Debug.LogWarning(
                    $"[Debug] player id={_offerPlayerId} 는 FA — SubmitOffer 대상 아님 (자유계약 흐름 V0.1 미구현)"
                );
                return;
            }

            var proposed = new Contract
            {
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(Math.Max(1, _offerYears)),
                weeklyWage = _offerWeeklyWage,
            };
            try
            {
                var offer = TransferSystem.SubmitOffer(
                    _offerPlayerId,
                    target.currentClubId,
                    state.userClubId,
                    _offerAmount,
                    proposed,
                    state,
                    balance
                );
                Debug.Log(
                    $"[Debug] Offer #{offer.id} 제출 — player {_offerPlayerId} ({target.info?.lastName}) "
                        + $"from {state.GetClub(target.currentClubId)?.name} to {state.GetClub(state.userClubId)?.name} "
                        + $"amount {_offerAmount:N0}"
                );
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Debug] SubmitOffer 실패: {ex.Message}");
            }
        }

        private void AdvanceDays(GameState state, GameBalanceSO balance, int days)
        {
            // stopRequested 무시 — 디버그 강제 진행
            for (int i = 0; i < days; i++)
                GameLoop.AdvanceDay(state, balance);
            Debug.Log($"[Debug] +{days} 일 진행 → {state.currentDate:yyyy-MM-dd}");
            Repaint();
        }

        private void ForceInjury(GameState state, int playerId, int days)
        {
            var p = state.GetPlayer(playerId);
            if (p == null)
            {
                Debug.LogWarning($"[Debug] player id={playerId} 없음");
                return;
            }
            if (p.state == null)
                p.state = new PlayerState();
            p.state.injury = new InjuryInfo
            {
                injuryTypeId = 1, // V0.1 단순 sentinel (≠ -1 이면 부상 중)
                startDate = state.currentDate,
                expectedReturn = state.currentDate.AddDays(days),
                isCareerThreatening = false,
            };
            Debug.Log(
                $"[Debug] player {p.id} 부상 부여 ({days} 일 — 복귀 예정 {p.state.injury.expectedReturn:yyyy-MM-dd})"
            );
            Repaint();
        }

        private void ForceRecover(GameState state, int playerId)
        {
            var p = state.GetPlayer(playerId);
            if (p == null)
            {
                Debug.LogWarning($"[Debug] player id={playerId} 없음");
                return;
            }
            if (p.state?.injury == null || p.state.injury.injuryTypeId == -1)
            {
                Debug.LogWarning($"[Debug] player {p.id} 부상 없음");
                return;
            }
            p.state.injury.injuryTypeId = -1;
            p.state.injury.isCareerThreatening = false;
            Debug.Log($"[Debug] player {p.id} 회복");
            Repaint();
        }
    }
}
