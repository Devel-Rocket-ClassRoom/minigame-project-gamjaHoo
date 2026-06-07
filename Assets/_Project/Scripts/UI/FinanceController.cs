// FinanceController.cs
// 재정 씬 — 유저 구단의 현금흐름을 한눈에 (수입 분산 / 주급 유출 / 순흐름 / 예산).
// V1.0 R.3 (#74). 표시 수치는 FinanceSystem public 헬퍼로 계산 → ProcessMonthly 와 단일 진실 소스.
// 도메인은 GBP base, 표시만 OptionsManager.Currency 변환 (#61).

using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Utils;
using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class FinanceController : MonoBehaviour
    {
        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text moneyText; // 현재 잔고 (대형)

        [Header("수입 (Income)")]
        [SerializeField]
        private TMP_Text tvText; // TV/중계 (월 / 연)

        [SerializeField]
        private TMP_Text matchdayText; // 입장 수입 (직전 월 / 시즌 추정)

        [SerializeField]
        private TMP_Text prizeText; // 상금 (안내)

        [Header("지출 (Outflow)")]
        [SerializeField]
        private TMP_Text wageText; // 주급 (월 / 연)

        [Header("요약")]
        [SerializeField]
        private TMP_Text netText; // 최근 월 순현금흐름 (색상 코딩)

        [SerializeField]
        private TMP_Text transferBudgetText;

        [SerializeField]
        private TMP_Text wageBudgetText;

        [SerializeField]
        private TMP_Text squadText; // 스쿼드 인원

        // 색상 (muip-reference §18)
        private static readonly Color Positive = new Color(0.30f, 0.69f, 0.31f); // #4CAF50
        private static readonly Color Negative = new Color(0.91f, 0.30f, 0.24f); // #E74C3C
        private static readonly Color Neutral = new Color(0.80f, 0.80f, 0.80f); // #CCCCCC

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            var state = GameManager.Instance?.State;
            var balance = GameDatabase.GameBalance;
            if (state == null || balance == null)
                return;
            var club = GameManager.Instance.UserClub;
            if (club == null)
                return;

            var cur = OptionsManager.Currency;

            int monthlyWage = FinanceSystem.MonthlyWage(club, state);
            long annualWage = FinanceSystem.AnnualWage(club, state);
            int monthlyTv = FinanceSystem.MonthlyTvIncome(club, balance);
            long annualTv = FinanceSystem.AnnualTvIncome(club, balance);
            int lastMatchday = FinanceSystem.LastMonthMatchday(club, state, balance);
            int seasonMatchday = FinanceSystem.ProjectedSeasonMatchday(club, state, balance);
            int netMonthly = monthlyTv + lastMatchday - monthlyWage;

            if (titleText != null)
                titleText.text = "재정";

            if (moneyText != null)
                moneyText.text = CurrencyFormatter.Format(club.finance?.money ?? 0, cur);

            if (tvText != null)
                tvText.text =
                    $"TV/중계   월 {CurrencyFormatter.Format(monthlyTv, cur)}  ·  연 {CurrencyFormatter.Format(annualTv, cur)}";

            if (matchdayText != null)
                matchdayText.text =
                    $"입장 수입   직전 월 {CurrencyFormatter.Format(lastMatchday, cur)}  ·  시즌 추정 {CurrencyFormatter.Format(seasonMatchday, cur)}";

            if (prizeText != null)
                prizeText.text = "상금   시즌 종료 시 리그 순위별 지급";

            if (wageText != null)
                wageText.text =
                    $"주급   월 -{CurrencyFormatter.Format(monthlyWage, cur)}  ·  연 -{CurrencyFormatter.Format(annualWage, cur)}";

            if (netText != null)
            {
                string sign = netMonthly >= 0 ? "+" : "-";
                netText.text =
                    $"최근 월 순현금흐름   {sign}{CurrencyFormatter.Format(System.Math.Abs(netMonthly), cur)}";
                netText.color = netMonthly > 0 ? Positive
                    : netMonthly < 0 ? Negative
                    : Neutral;
            }

            if (transferBudgetText != null)
                transferBudgetText.text =
                    $"이적 예산   {CurrencyFormatter.Format(club.finance?.transferBudget ?? 0, cur)}";

            if (wageBudgetText != null)
                wageBudgetText.text =
                    $"임금 예산   {CurrencyFormatter.Format(club.finance?.wageBudget ?? 0, cur)}";

            if (squadText != null)
                squadText.text = $"스쿼드 {club.seniorSquadIds?.Count ?? 0}명";
        }
    }
}
