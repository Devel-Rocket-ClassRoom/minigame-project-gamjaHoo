// Task 13.3 (Issue #48) — 스타팅 가챠 화면.
// EvaluateSquad 결과(4라인 티어 + ACE) 표시 + 리롤/확정 버튼.
// 확정 → DashboardScene 전환.

using System;
using FMLite.Application;
using Random = System.Random;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class GachaController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("4라인 티어 텍스트")]
        [SerializeField]
        private TMP_Text gkTierText;

        [SerializeField]
        private TMP_Text dfTierText;

        [SerializeField]
        private TMP_Text mfTierText;

        [SerializeField]
        private TMP_Text atTierText;

        [Header("ACE")]
        [SerializeField]
        private TMP_Text aceLineText;

        [Header("리롤")]
        [SerializeField]
        private TMP_Text rerollTokenText;

        [SerializeField]
        private Button rerollButton;

        [Header("데이터")]
        [SerializeField]
        private LeagueConfigSO leagueConfig;

        [SerializeField]
        private GameBalanceSO balance;

        private int rerollCount;

        private void Start()
        {
            var eval = EvaluateCurrent();
            RefreshUI(eval);
        }

        public void OnRerollClicked()
        {
            var state = GameManager.Instance.State;
            var club = GameManager.Instance.UserClub;
            if (club == null || state.rerollTokens <= 0)
                return;

            var rng = new Random(state.randomSeed ^ club.id ^ rerollCount);
            rerollCount++;

            var eval = StartingSquadGacha.RerollSquad(
                club,
                state,
                leagueConfig,
                balance,
                state.currentDate,
                rng
            );
            RefreshUI(eval);
        }

        public void OnConfirmClicked()
        {
            SceneManager.LoadScene(DashboardScene);
        }

        private SquadEvaluation EvaluateCurrent()
        {
            var state = GameManager.Instance.State;
            var club = GameManager.Instance.UserClub;
            return StartingSquadGacha.EvaluateSquad(club, state, balance);
        }

        private void RefreshUI(SquadEvaluation eval)
        {
            gkTierText.text = $"GK  {TierLabel(eval.gk)}";
            dfTierText.text = $"DF  {TierLabel(eval.df)}";
            mfTierText.text = $"MF  {TierLabel(eval.mf)}";
            atTierText.text = $"AT  {TierLabel(eval.at)}";
            aceLineText.text = $"ACE  {eval.acePosition}";

            var tokens = GameManager.Instance.State.rerollTokens;
            rerollTokenText.text = Localization.Get("reroll_fmt", tokens);
            rerollButton.interactable = tokens > 0;
        }

        private static string TierLabel(TierGrade grade) =>
            grade switch
            {
                TierGrade.Elite => "Elite ★★★★★",
                TierGrade.Strong => "Strong ★★★★",
                TierGrade.Average => "Average ★★★",
                TierGrade.Weak => "Weak ★★",
                TierGrade.Poor => "Poor ★",
                _ => grade.ToString(),
            };
    }
}
