// Task 13.6 (Issue #51) — 선수 프로필 화면.
// PlayerPrefs("SelectedPlayerId")로 선수 ID 수신.
// 능력치는 5단계 티어로 표시 (design-decisions #14).
// GameBalanceSO.isDebugMode 활성 시 정확한 수치 추가 노출 (Task 14.2 연동).

using System.Text;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class PlayerProfileController : MonoBehaviour
    {
        private const string SquadScene = "SquadScene";

        // 개별 스탯 티어 컷오프 (스탯 스케일 1-20)
        private const int StatElite = 17;
        private const int StatStrong = 13;
        private const int StatAverage = 9;
        private const int StatWeak = 5;

        [Header("헤더")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text positionAgeText;
        [SerializeField] private TMP_Text nationalityText;
        [SerializeField] private TMP_Text footText;

        [Header("능력치")]
        [SerializeField] private TMP_Text technicalText;
        [SerializeField] private TMP_Text mentalText;
        [SerializeField] private TMP_Text physicalText;
        [SerializeField] private TMP_Text gkText;

        [Header("트레잇 / 계약 / 상태")]
        [SerializeField] private TMP_Text traitsText;
        [SerializeField] private TMP_Text contractText;
        [SerializeField] private TMP_Text stateText;

        [Header("커리어")]
        [SerializeField] private TMP_Text careerText;

        private void Start()
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            int playerId = PlayerPrefs.GetInt(SquadController.SelectedPlayerIdKey, -1);
            var player = state.GetPlayer(playerId);
            if (player == null)
            {
                if (nameText != null) nameText.text = $"선수 없음 (id={playerId})";
                return;
            }

            bool debugMode = GameDatabase.GameBalance != null && GameDatabase.GameBalance.isDebugMode;
            int age = player.info != null
                ? (int)((state.currentDate - player.info.birthDate).TotalDays / 365.25)
                : 0;

            if (nameText != null)
                nameText.text = player.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={player.id}";

            if (positionAgeText != null)
                positionAgeText.text = $"{player.info?.primaryPosition} · {age}세";

            if (nationalityText != null)
                nationalityText.text = player.info?.nationalityCode ?? "-";

            if (footText != null)
                footText.text = player.info?.preferredFoot.ToString() ?? "-";

            if (technicalText != null)
                technicalText.text = BuildTechnicalText(player, debugMode);

            if (mentalText != null)
                mentalText.text = BuildMentalText(player, debugMode);

            if (physicalText != null)
                physicalText.text = BuildPhysicalText(player, debugMode);

            if (gkText != null)
                gkText.text = BuildGkText(player, debugMode);

            if (traitsText != null)
                traitsText.text = BuildTraitsText(player);

            if (contractText != null)
                contractText.text = BuildContractText(player, debugMode);

            if (stateText != null)
                stateText.text = BuildStateText(player, state);

            if (careerText != null)
                careerText.text = BuildCareerText(player, state);
        }

        public void OnBackClicked()
        {
            Debug.Log("[PlayerProfileController] Back button clicked. Loading SquadScene...");
            SceneManager.LoadScene(SquadScene);
        }

        // ── 능력치 ──────────────────────────────────────────────────────────

        private static string StatTier(int value)
        {
            if (value >= StatElite)   return "Elite";
            if (value >= StatStrong)  return "Strong";
            if (value >= StatAverage) return "Average";
            if (value >= StatWeak)    return "Weak";
            return "Poor";
        }

        private static string StatLine(string label, int value, bool debug)
            => debug ? $"{label}: {StatTier(value)} ({value})" : $"{label}: {StatTier(value)}";

        private string BuildTechnicalText(Player p, bool debug)
        {
            if (p.stats == null) return "기술: -";
            var t = p.stats.technical;
            var sb = new StringBuilder("[기술]\n");
            sb.AppendLine(StatLine("패스", t.passing, debug));
            sb.AppendLine(StatLine("슈팅", t.shooting, debug));
            sb.AppendLine(StatLine("태클", t.tackling, debug));
            sb.AppendLine(StatLine("드리블", t.dribbling, debug));
            sb.AppendLine(StatLine("헤딩", t.heading, debug));
            sb.AppendLine(StatLine("크로스", t.crossing, debug));
            sb.AppendLine(StatLine("퍼스트터치", t.firstTouch, debug));
            sb.AppendLine(StatLine("마무리", t.finishing, debug));
            sb.AppendLine(StatLine("중거리", t.longShots, debug));
            sb.AppendLine(StatLine("프리킥", t.freeKickAccuracy, debug));
            sb.AppendLine(StatLine("패널티", t.penaltyTaking, debug));
            sb.Append(StatLine("코너", t.corners, debug));
            return sb.ToString();
        }

        private string BuildMentalText(Player p, bool debug)
        {
            if (p.stats == null) return "정신: -";
            var m = p.stats.mental;
            var sb = new StringBuilder("[정신]\n");
            sb.AppendLine(StatLine("시야", m.vision, debug));
            sb.AppendLine(StatLine("예측", m.anticipation, debug));
            sb.AppendLine(StatLine("침착", m.composure, debug));
            sb.AppendLine(StatLine("집중", m.concentration, debug));
            sb.AppendLine(StatLine("판단", m.decisions, debug));
            sb.AppendLine(StatLine("투지", m.determination, debug));
            sb.AppendLine(StatLine("리더십", m.leadership, debug));
            sb.AppendLine(StatLine("오프더볼", m.offTheBall, debug));
            sb.AppendLine(StatLine("포지셔닝", m.positioning, debug));
            sb.AppendLine(StatLine("팀워크", m.teamwork, debug));
            sb.AppendLine(StatLine("활동량", m.workRate, debug));
            sb.Append(StatLine("공격성", m.aggression, debug));
            return sb.ToString();
        }

        private string BuildPhysicalText(Player p, bool debug)
        {
            if (p.stats == null) return "신체: -";
            var ph = p.stats.physical;
            var sb = new StringBuilder("[신체]\n");
            sb.AppendLine(StatLine("가속", ph.acceleration, debug));
            sb.AppendLine(StatLine("민첩", ph.agility, debug));
            sb.AppendLine(StatLine("밸런스", ph.balance, debug));
            sb.AppendLine(StatLine("점프", ph.jumping, debug));
            sb.AppendLine(StatLine("피지컬", ph.naturalFitness, debug));
            sb.AppendLine(StatLine("스피드", ph.pace, debug));
            sb.AppendLine(StatLine("스태미나", ph.stamina, debug));
            sb.Append(StatLine("체력", ph.strength, debug));
            return sb.ToString();
        }

        private string BuildGkText(Player p, bool debug)
        {
            if (p.stats == null || p.info?.primaryPosition != Position.GK)
                return string.Empty;
            var g = p.stats.gk;
            var sb = new StringBuilder("[골키퍼]\n");
            sb.AppendLine(StatLine("공중장악", g.aerialReach, debug));
            sb.AppendLine(StatLine("박스장악", g.commandOfArea, debug));
            sb.AppendLine(StatLine("지시", g.communication, debug));
            sb.AppendLine(StatLine("돌발", g.eccentricity, debug));
            sb.AppendLine(StatLine("핸들링", g.handling, debug));
            sb.AppendLine(StatLine("킥", g.kicking, debug));
            sb.AppendLine(StatLine("1대1", g.oneOnOnes, debug));
            sb.AppendLine(StatLine("반응", g.reflexes, debug));
            sb.AppendLine(StatLine("돌진", g.rushingOut, debug));
            sb.Append(StatLine("스로인", g.throwing, debug));
            return sb.ToString();
        }

        // ── 트레잇 / 계약 / 상태 / 커리어 ─────────────────────────────────

        private static string BuildTraitsText(Player p)
        {
            if (p.traitIds == null || p.traitIds.Count == 0)
                return "[트레잇]\n없음";
            var sb = new StringBuilder("[트레잇]\n");
            foreach (var id in p.traitIds)
            {
                var trait = GameDatabase.GetTrait(id);
                sb.AppendLine(trait != null ? trait.displayName : $"id={id}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildContractText(Player p, bool debug)
        {
            if (p.contract == null)
                return "[계약]\n정보 없음";
            var c = p.contract;
            var sb = new StringBuilder("[계약]\n");
            if (debug)
                sb.AppendLine($"주급: £{c.weeklyWage:N0}");
            sb.AppendLine($"계약 만료: {c.endDate:yyyy-MM-dd}");
            if (c.releaseClause > 0)
                sb.Append(debug ? $"바이아웃: £{c.releaseClause:N0}" : "바이아웃: 있음");
            return sb.ToString().TrimEnd();
        }

        private static string BuildStateText(Player p, GameState state)
        {
            if (p.state == null)
                return "[상태]\n정보 없음";
            var s = p.state;
            var sb = new StringBuilder("[상태]\n");
            sb.AppendLine($"피로: {s.fatigue}");
            sb.AppendLine($"사기: {s.morale}");
            sb.AppendLine($"폼: {s.form}");
            sb.AppendLine($"출전: {s.seasonAppearances}경기");
            if (s.injury != null)
                sb.AppendLine($"부상: 복귀 예정 {s.injury.expectedReturn:MM-dd}");
            else
                sb.AppendLine("부상: 없음");
            sb.Append(s.transferListed ? "이적 리스트 등재" : "");
            return sb.ToString().TrimEnd();
        }

        private static string BuildCareerText(Player p, GameState state)
        {
            if (p.career == null || p.career.Count == 0)
                return "[커리어]\n기록 없음";
            var sb = new StringBuilder("[커리어]\n");
            foreach (var s in p.career)
            {
                var club = state.GetClub(s.clubId);
                string clubName = club?.name ?? $"id={s.clubId}";
                sb.AppendLine($"{s.seasonYear}-{s.seasonYear + 1}  {clubName}  {s.appearances}경기 {s.goals}골 {s.assists}도움");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
