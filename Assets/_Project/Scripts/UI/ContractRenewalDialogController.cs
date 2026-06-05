// ContractRenewalDialogController.cs
// R.7 (#77-2) — PlayerProfile [재계약] 버튼 → 재계약 모달.
// 로직(TransferSystem.RenewContract)은 기구현 — 본 컨트롤러는 UI 배선만.
//
// 사용:
//   ContractRenewalDialogController.Show(playerId)
//   - 패널 활성화 + 주급 입력 기본값 = SuggestFairWage + 계약기간 선택(1/2/3년)
//   - [확정] → Contract 빌드 → RenewContract → 수락/거절 결과 표시
//
// InterviewDialogController 패턴 미러링 (패널 + Show/Hide + 버튼).

using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class ContractRenewalDialogController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text titleText;

        [Header("주급")]
        [SerializeField]
        private TMP_InputField wageInput;

        [SerializeField]
        private TMP_Text suggestedWageText;

        [Header("계약 기간 (1/2/3년)")]
        [SerializeField]
        private TMP_Text lengthText;

        [SerializeField]
        private Button length1Button;

        [SerializeField]
        private Button length2Button;

        [SerializeField]
        private Button length3Button;

        [Header("액션")]
        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_Text resultText;

        [Header("Data")]
        [SerializeField]
        private GameBalanceSO balance;

        private int _playerId = -1;
        private int _years = 2;

        private void Awake()
        {
            if (length1Button != null)
                length1Button.onClick.AddListener(() => SetYears(1));
            if (length2Button != null)
                length2Button.onClick.AddListener(() => SetYears(2));
            if (length3Button != null)
                length3Button.onClick.AddListener(() => SetYears(3));
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            Hide();
        }

        public void Show(int playerId)
        {
            _playerId = playerId;
            _years = 2;

            var state = GameManager.Instance?.State;
            var player = state?.GetPlayer(playerId);
            if (player == null || balance == null)
            {
                Hide();
                return;
            }

            if (titleText != null)
                titleText.text = Localization.Get("renew_title");

            int fair = TransferSystem.SuggestFairWage(player, balance);
            if (wageInput != null)
                wageInput.text = fair.ToString();
            if (suggestedWageText != null)
                suggestedWageText.text = Localization.Get(
                    "renew_suggested_fmt",
                    FMLite.Utils.CurrencyFormatter.Format(fair, OptionsManager.Currency)
                );
            if (resultText != null)
                resultText.text = string.Empty;
            UpdateLengthLabel();

            if (panel != null)
                panel.SetActive(true);
        }

        public void Hide()
        {
            _playerId = -1;
            if (panel != null)
                panel.SetActive(false);
        }

        private void SetYears(int years)
        {
            _years = Mathf.Clamp(years, 1, 5);
            UpdateLengthLabel();
        }

        private void UpdateLengthLabel()
        {
            if (lengthText != null)
                lengthText.text = Localization.Get("renew_length_years_fmt", _years);
        }

        private void OnConfirm()
        {
            if (_playerId == -1)
            {
                Hide();
                return;
            }
            var state = GameManager.Instance?.State;
            var player = state?.GetPlayer(_playerId);
            if (state == null || player == null || balance == null)
            {
                Hide();
                return;
            }

            if (wageInput == null || !int.TryParse(wageInput.text, out int wage) || wage <= 0)
            {
                if (resultText != null)
                    resultText.text = Localization.Get("renew_invalid_wage");
                return;
            }

            var newContract = new Contract
            {
                weeklyWage = wage,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(_years),
                releaseClause = player.contract?.releaseClause ?? 0,
            };

            TransferSystem.RenewContract(_playerId, newContract, state, balance);
            // 수락 시 RenewContract 가 player.contract = newContract 로 교체 → 참조 동일성으로 판정.
            bool accepted = ReferenceEquals(player.contract, newContract);

            if (resultText != null)
                resultText.text = Localization.Get(accepted ? "renew_accepted" : "renew_rejected");
        }
    }
}
