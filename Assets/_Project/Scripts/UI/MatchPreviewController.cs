// MatchPreviewController.cs
// Stage H.4 — 경기 직전 점검 화면. DashboardController 가 유저 경기일에 라우팅(미플레이 매치).
// 라인업(SelectStartingEleven 프리뷰 — 시뮬과 동일 시드) / 전술 / Mentality / 상대 라인업 + 활성 시너지 수.
// [매치 시작] = BackgroundSimulator.SimulateUserMatch → MatchTextScene / [라인업 수정] = TacticLineupScene.
// 게이트: 출전 가능 11명 미만이면 시작 차단 (H.6 HasValidLineup 취지 배선).

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class MatchPreviewController : MonoBehaviour
    {
        private const string MatchTextScene = "MatchTextScene";
        private const string TacticLineupScene = "TacticLineupScene";
        private const string DashboardScene = "DashboardScene";

        [Header("Header")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text matchupText;

        [Header("User")]
        [SerializeField]
        private TMP_Text userInfoText;

        [SerializeField]
        private RectTransform userLineupContent;

        [Header("Opponent")]
        [SerializeField]
        private TMP_Text opponentInfoText;

        [SerializeField]
        private RectTransform opponentLineupContent;

        [Header("Info / Gate")]
        [SerializeField]
        private TMP_Text synergyText;

        [SerializeField]
        private TMP_Text warningText;

        [SerializeField]
        private TMP_Text lineupRowTemplate; // 비활성 행 템플릿

        [Header("Buttons (MUIP)")]
        [SerializeField]
        private ButtonManagerBasic startButton;

        [SerializeField]
        private ButtonManagerBasic editButton;

        private GameState _state;
        private GameBalanceSO _balance;
        private Match _match;
        private Club _userClub;
        private Club _opponent;
        private List<int> _userXI;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            _balance = GameDatabase.GameBalance;
            if (_state == null)
                return;

            int matchId = PlayerPrefs.GetInt(DashboardController.SelectedMatchIdKey, -1);
            _match = FindMatch(matchId);
            _userClub = _state.GetClub(_state.userClubId);
            if (_match == null || _userClub == null)
            {
                SceneManager.LoadScene(DashboardScene);
                return;
            }

            int oppId =
                _match.homeClubId == _state.userClubId ? _match.awayClubId : _match.homeClubId;
            _opponent = _state.GetClub(oppId);

            WireButtons();
            BuildView();
        }

        private void WireButtons()
        {
            if (startButton != null)
            {
                startButton.buttonText = "매치 시작";
                startButton.UpdateUI();
                startButton.clickEvent.AddListener(OnStart);
            }
            if (editButton != null)
            {
                editButton.buttonText = "라인업 수정";
                editButton.UpdateUI();
                editButton.clickEvent.AddListener(OnEdit);
            }
        }

        private void BuildView()
        {
            if (titleText != null)
                titleText.text = "경기 미리보기";
            string home = _state.GetClub(_match.homeClubId)?.name ?? "?";
            string away = _state.GetClub(_match.awayClubId)?.name ?? "?";
            if (matchupText != null)
                matchupText.text = $"{home}  vs  {away}";

            // 유저 XI — MatchSimulator 와 동일 시드라 프리뷰 = 실제 출전.
            int userSeed = _match.id ^ _state.randomSeed ^ _userClub.id;
            _userXI = LineupSelector.SelectStartingEleven(_userClub, _state, userSeed, _balance);
            var userFormation = GameDatabase.GetFormation(_userClub.tactic?.formationId ?? 1);
            if (userInfoText != null)
                userInfoText.text =
                    $"{_userClub.name}\n포메이션 {userFormation?.displayName ?? "-"} · {MentalityKor(_userClub.tactic?.mentality ?? Mentality.Balanced)}";
            FillLineup(userLineupContent, _userXI);

            // 상대 XI
            if (_opponent != null)
            {
                int oppSeed = _match.id ^ _state.randomSeed ^ _opponent.id;
                var oppXI = LineupSelector.SelectStartingEleven(_opponent, _state, oppSeed, _balance);
                var oppFormation = GameDatabase.GetFormation(_opponent.tactic?.formationId ?? 1);
                if (opponentInfoText != null)
                    opponentInfoText.text = $"{_opponent.name}\n포메이션 {oppFormation?.displayName ?? "-"}";
                FillLineup(opponentLineupContent, oppXI);
            }

            // H.5: 매치업 보너스 + 활성 시너지 목록(이름 — 효과) 인라인
            if (synergyText != null)
                synergyText.text = BuildInfoText(_userXI);

            // 게이트 — 출전 가능 11명 미만이면 시작 차단.
            bool ok = _userXI.Count >= 11;
            if (warningText != null)
            {
                warningText.gameObject.SetActive(!ok);
                warningText.text = ok ? "" : "출전 가능 선수가 11명 미만입니다. 라인업을 수정하세요.";
            }
            SetInteractable(startButton, ok);
        }

        // H.5: 매치업 보너스 + 활성 시너지(이름 — 효과설명) 멀티라인 rich text.
        private string BuildInfoText(List<int> xi)
        {
            var sb = new System.Text.StringBuilder();

            // 포메이션 매치업 — 유저 기준 (홈/원정 무관 Get(userF, oppF), 명세 G.4)
            int userF = _userClub.tactic?.formationId ?? 1;
            int oppF = _opponent?.tactic?.formationId ?? 1;
            float bonus = GameDatabase.FormationMatchup != null
                ? GameDatabase.FormationMatchup.Get(userF, oppF)
                : 1f;
            string matchupVal =
                Mathf.Abs(bonus - 1f) < 0.001f
                    ? Localization.Get("matchup_even")
                    : $"{(bonus >= 1f ? "+" : "")}{(bonus - 1f) * 100f:0}%";
            sb.AppendLine($"<b>{Localization.Get("matchup_label")}</b>: {matchupVal}");

            // 활성 시너지 목록
            var syns = TacticImpact.ComputeSynergies(BuildTempTactic(xi), _state);
            sb.AppendLine($"<b>{Localization.Get("synergy_active_label")}</b> ({syns.Count})");
            if (syns.Count == 0)
                sb.AppendLine(Localization.Get("synergy_none_active"));
            else
                foreach (var s in syns)
                    sb.AppendLine(
                        $"· {Localization.Get(s.nameKey)} — {Localization.Get(s.descriptionKey)}"
                    );
            return sb.ToString();
        }

        private Tactic BuildTempTactic(List<int> xi)
        {
            var temp = new Tactic
            {
                formationId = _userClub.tactic?.formationId ?? 1,
                mentality = _userClub.tactic?.mentality ?? Mentality.Balanced,
                slots = new List<TacticSlot>(),
            };
            for (int i = 0; i < xi.Count; i++)
                temp.slots.Add(new TacticSlot { slotIndex = i, assignedPlayerId = xi[i] });
            return temp;
        }

        private void FillLineup(RectTransform content, List<int> xi)
        {
            if (content == null || lineupRowTemplate == null)
                return;
            foreach (Transform c in content)
                if (c.gameObject != lineupRowTemplate.gameObject)
                    Destroy(c.gameObject);
            foreach (var pid in xi)
            {
                var p = _state.GetPlayer(pid);
                if (p == null)
                    continue;
                var row = Instantiate(lineupRowTemplate, content);
                row.gameObject.SetActive(true);
                row.text = $"{p.info.primaryPosition,-3} {p.info.lastName}  {p.currentAbility}";
            }
        }

        private void OnStart()
        {
            if (_userXI == null || _userXI.Count < 11)
                return;
            BackgroundSimulator.SimulateUserMatch(_match, _state, _balance);
            PlayerPrefs.SetInt(DashboardController.SelectedMatchIdKey, _match.id);
            SceneManager.LoadScene(MatchTextScene);
        }

        private void OnEdit() => SceneManager.LoadScene(TacticLineupScene);

        private Match FindMatch(int id)
        {
            foreach (var league in _state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                    if (m.id == id)
                        return m;
            }
            return null;
        }

        private static void SetInteractable(ButtonManagerBasic b, bool on)
        {
            if (b == null)
                return;
            var btn = b.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
                btn.interactable = on;
        }

        private static string MentalityKor(Mentality m)
        {
            switch (m)
            {
                case Mentality.VeryDefensive:
                    return "매우 수비적";
                case Mentality.Defensive:
                    return "수비적";
                case Mentality.Cautious:
                    return "신중";
                case Mentality.Positive:
                    return "적극적";
                case Mentality.Attacking:
                    return "공격적";
                case Mentality.VeryAttacking:
                    return "매우 공격적";
                default:
                    return "균형";
            }
        }
    }
}
