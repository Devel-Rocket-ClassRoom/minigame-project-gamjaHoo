// RoleDutyPopup.cs
// Stage H.3 — 슬롯 Role/Duty 팝업 위젯. Canvas 최상위 오버레이(클리핑 없음) — 구 CustomDropdown 폐기.
// 슬롯 클릭 → 포지션 호환 Role 버튼 리스트 + Duty 3버튼. 선택 시 TacticSlot 직접 기록 + onChanged 콜백.
// 컴포넌트는 항상 활성인 루트에 두고(Awake 보장), 내부 'root'(backdrop+panel)만 토글.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class RoleDutyPopup : MonoBehaviour
    {
        [SerializeField]
        private GameObject root; // backdrop + panel 컨테이너 (토글)

        [SerializeField]
        private Button backdrop; // 바깥 클릭 시 닫기

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private RectTransform roleListContent;

        [SerializeField]
        private Button roleButtonTemplate; // 비활성 템플릿

        [SerializeField]
        private Button dutyAttackButton;

        [SerializeField]
        private Button dutySupportButton;

        [SerializeField]
        private Button dutyDefendButton;

        private static readonly Color Selected = new Color32(74, 144, 217, 255);
        private static readonly Color Normal = new Color32(42, 42, 62, 255);

        private TacticSlot _slot;
        private Position _pos;
        private Action _onChanged;
        private readonly List<Button> _roleButtons = new List<Button>();
        private readonly List<int> _roleIds = new List<int>();

        private void Awake()
        {
            if (backdrop != null)
                backdrop.onClick.AddListener(Close);
            WireDuty(dutyAttackButton, Duty.Attack);
            WireDuty(dutySupportButton, Duty.Support);
            WireDuty(dutyDefendButton, Duty.Defend);
            // H.5: duty 라벨 로컬라이즈 (H.3 한글 하드코딩 대체)
            SetButtonLabel(dutyAttackButton, "duty_attack");
            SetButtonLabel(dutySupportButton, "duty_support");
            SetButtonLabel(dutyDefendButton, "duty_defend");
            if (root != null)
                root.SetActive(false);
        }

        private static void SetButtonLabel(Button b, string key)
        {
            var t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null)
                t.text = Localization.Get(key);
        }

        private void WireDuty(Button b, Duty d)
        {
            if (b == null)
                return;
            b.onClick.AddListener(() =>
            {
                if (_slot == null)
                    return;
                _slot.duty = d;
                HighlightDuty();
                _onChanged?.Invoke();
            });
        }

        // 슬롯 클릭 시 호출. slot 참조에 직접 기록.
        public void Open(TacticSlot slot, Position pos, Action onChanged)
        {
            _slot = slot;
            _pos = pos;
            _onChanged = onChanged;
            if (titleText != null)
                titleText.text = pos.ToString();
            BuildRoleButtons();
            HighlightDuty();
            if (root != null)
                root.SetActive(true);
        }

        public void Close()
        {
            _slot = null;
            if (root != null)
                root.SetActive(false);
        }

        private void BuildRoleButtons()
        {
            var roles = GameDatabase
                .AllPlayerRoles.Where(r =>
                    r.compatiblePositions != null && r.compatiblePositions.Contains(_pos)
                )
                .OrderBy(r => r.id)
                .ToList();

            while (_roleButtons.Count < roles.Count && roleButtonTemplate != null)
            {
                var b = Instantiate(roleButtonTemplate, roleListContent);
                int captureIdx = _roleButtons.Count;
                b.onClick.AddListener(() => OnRolePicked(captureIdx));
                _roleButtons.Add(b);
            }

            _roleIds.Clear();
            for (int i = 0; i < _roleButtons.Count; i++)
            {
                bool active = i < roles.Count;
                _roleButtons[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                _roleIds.Add(roles[i].id);
                var t = _roleButtons[i].GetComponentInChildren<TMP_Text>();
                if (t != null)
                    t.text = roles[i].displayName;
            }
            HighlightRoles();
        }

        private void OnRolePicked(int idx)
        {
            if (_slot == null || idx < 0 || idx >= _roleIds.Count)
                return;
            _slot.roleId = _roleIds[idx];
            HighlightRoles();
            _onChanged?.Invoke();
        }

        private void HighlightRoles()
        {
            for (int i = 0; i < _roleIds.Count; i++)
                SetColor(_roleButtons[i], _slot != null && _roleIds[i] == _slot.roleId);
        }

        private void HighlightDuty()
        {
            if (_slot == null)
                return;
            SetColor(dutyAttackButton, _slot.duty == Duty.Attack);
            SetColor(dutySupportButton, _slot.duty == Duty.Support);
            SetColor(dutyDefendButton, _slot.duty == Duty.Defend);
        }

        private static void SetColor(Button b, bool selected)
        {
            if (b == null)
                return;
            var img = b.GetComponent<Image>();
            if (img != null)
                img.color = selected ? Selected : Normal;
        }
    }
}
