// MentoringController.cs
// L.4 — MentoringScene 메인 컨트롤러.
// 그룹 목록 표시 + 그룹 만들기 (멘토 드롭다운 + 멘티 토글) + 해체.
// Unity AI 씬/prefab 지시서: docs/unity-ai/mentoring-scene.md

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class MentoringController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("그룹 목록")]
        [SerializeField]
        private Transform groupListParent;

        [SerializeField]
        private GameObject groupItemPrefab;

        [Header("그룹 만들기 패널")]
        [SerializeField]
        private GameObject createPanel;

        [Header("Mentor (MUIP CustomDropdown)")]
        [SerializeField]
        private CustomDropdown mentorDropdown;

        [SerializeField]
        private Transform menteeToggleParent;

        [SerializeField]
        private GameObject menteeTogglePrefab;

        [Header("Panel Buttons (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager confirmCreateButton;

        [SerializeField]
        private ButtonManager cancelCreateButton;

        [Header("공통 (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager createGroupButton;

        [SerializeField]
        private ButtonManager backButton;

        private GameState _state;
        private Club _userClub;
        private readonly List<int> _mentorCandidateIds = new();
        private readonly List<int> _menteeCandidateIds = new();
        private readonly HashSet<int> _selectedMenteeIds = new();

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            _userClub = GameManager.Instance.UserClub;
            if (_userClub == null)
                return;

            if (createPanel != null)
                createPanel.SetActive(false);

            if (createGroupButton != null)
                createGroupButton.clickEvent.AddListener(OnCreateGroupButtonClicked);
            if (backButton != null)
                backButton.clickEvent.AddListener(OnBackClicked);
            if (confirmCreateButton != null)
                confirmCreateButton.clickEvent.AddListener(OnConfirmCreateClicked);
            if (cancelCreateButton != null)
                cancelCreateButton.clickEvent.AddListener(OnCancelCreateClicked);

            Refresh();
        }

        public void OnCreateGroupButtonClicked()
        {
            if (createPanel == null)
                return;

            BuildCreatePanel();
            createPanel.SetActive(true);
        }

        public void OnConfirmCreateClicked()
        {
            if (_state == null || _userClub == null)
                return;
            if (mentorDropdown == null || _mentorCandidateIds.Count == 0)
                return;
            if (_selectedMenteeIds.Count == 0)
                return;

            int idx = mentorDropdown.selectedItemIndex;
            if (idx < 0 || idx >= _mentorCandidateIds.Count)
                return;
            int mentorId = _mentorCandidateIds[idx];
            try
            {
                MentoringSystem.AddGroup(_userClub, mentorId, _selectedMenteeIds.ToList(), _state);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MentoringController] AddGroup 실패: {e.Message}");
            }

            if (createPanel != null)
                createPanel.SetActive(false);

            Refresh();
        }

        public void OnCancelCreateClicked()
        {
            if (createPanel != null)
                createPanel.SetActive(false);
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        // ── 내부 ───────────────────────────────────────────────────────

        private void Refresh()
        {
            PopulateGroupList();
        }

        private void PopulateGroupList()
        {
            if (groupListParent == null)
                return;

            foreach (Transform child in groupListParent)
                Destroy(child.gameObject);

            foreach (var group in _userClub.season.mentoringGroups)
            {
                var item = Instantiate(groupItemPrefab, groupListParent);
                var ctrl = item.GetComponent<MentoringGroupItem>();
                if (ctrl != null)
                    ctrl.Setup(group, _state, OnDissolveClicked);
            }
        }

        private void BuildCreatePanel()
        {
            _mentorCandidateIds.Clear();
            _menteeCandidateIds.Clear();
            _selectedMenteeIds.Clear();

            // 이미 그룹에 속한 ID 수집
            var usedMentors = new HashSet<int>(
                _userClub.season.mentoringGroups.Select(g => g.mentorPlayerId)
            );
            var usedMentees = new HashSet<int>(
                _userClub.season.mentoringGroups.SelectMany(g => g.menteePlayerIds)
            );

            var allIds = _userClub.seniorSquadIds.Concat(_userClub.youthSquadIds).ToList();

            // 멘토 후보: 그룹 미참여 + 나이 25세 이상 권장 (강제 아님)
            var mentorOptions = new List<string>();
            foreach (var id in allIds)
            {
                if (usedMentors.Contains(id))
                    continue;
                var p = _state.GetPlayer(id);
                if (p == null)
                    continue;
                _mentorCandidateIds.Add(id);
                mentorOptions.Add(PlayerLabel(p));
            }

            if (mentorDropdown != null)
            {
                mentorDropdown.dropdownItems.Clear();
                foreach (var opt in mentorOptions)
                    mentorDropdown.CreateNewItemFast(opt, null);
                mentorDropdown.selectedItemIndex = 0;
                mentorDropdown.SetupDropdown();
                Canvas.ForceUpdateCanvases();
            }

            // 멘티 후보: 그룹 미참여
            if (menteeToggleParent != null)
            {
                foreach (Transform child in menteeToggleParent)
                    Destroy(child.gameObject);
            }

            foreach (var id in allIds)
            {
                if (usedMentees.Contains(id))
                    continue;
                var p = _state.GetPlayer(id);
                if (p == null)
                    continue;
                _menteeCandidateIds.Add(id);

                if (menteeTogglePrefab != null && menteeToggleParent != null)
                {
                    var go = Instantiate(menteeTogglePrefab, menteeToggleParent);
                    var label = go.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                        label.text = PlayerLabel(p);

                    var toggle = go.GetComponent<Toggle>();
                    int capturedId = id;
                    if (toggle != null)
                        toggle.onValueChanged.AddListener(on =>
                        {
                            if (on)
                                _selectedMenteeIds.Add(capturedId);
                            else
                                _selectedMenteeIds.Remove(capturedId);
                        });
                }
            }
        }

        private void OnDissolveClicked(int groupId)
        {
            if (_state == null || _userClub == null)
                return;
            try
            {
                MentoringSystem.RemoveGroup(_userClub, groupId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MentoringController] RemoveGroup 실패: {e.Message}");
            }
            Refresh();
        }

        private static string PlayerLabel(Player p)
        {
            string name = p.info?.lastName ?? $"P{p.id}";
            int age = p.info != null ? System.DateTime.Now.Year - p.info.birthDate.Year : 0;
            return $"{name} ({age})";
        }
    }
}
