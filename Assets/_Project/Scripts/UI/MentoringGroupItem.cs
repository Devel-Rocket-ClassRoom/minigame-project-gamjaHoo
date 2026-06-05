// MentoringGroupItem.cs
// MentoringScene — 그룹 목록의 단일 아이템.
// 멘토 헤더 + 멘티별 진행률 행(MenteeProgressRow) + 해체 버튼.
// V1.0 I.2 — 멘티 Hidden Attr 수렴 진행률 시각화 추가.

using System;
using FMLite.Application;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class MentoringGroupItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text mentorLabel;

        [Header("멘티 진행률 행 (동적 생성)")]
        [SerializeField]
        private Transform menteeRowParent;

        [SerializeField]
        private MenteeProgressRow menteeRowPrefab;

        [SerializeField]
        private Button dissolveButton;

        public void Setup(
            MentoringGroup group,
            GameState state,
            int rateCap,
            float fraction,
            Action<int> onDissolve
        )
        {
            var mentor = state.GetPlayer(group.mentorPlayerId);
            if (mentorLabel != null)
                mentorLabel.text =
                    mentor != null
                        ? Localization.Get("mentoring_mentor_fmt", PlayerName(mentor))
                        : $"ID {group.mentorPlayerId}";

            BuildMenteeRows(group, state, mentor?.hiddenAttrs, rateCap, fraction);

            if (dissolveButton != null)
            {
                int groupId = group.id;
                dissolveButton.onClick.RemoveAllListeners();
                dissolveButton.onClick.AddListener(() => onDissolve(groupId));
            }
        }

        private void BuildMenteeRows(
            MentoringGroup group,
            GameState state,
            HiddenAttributes mentorAttrs,
            int rateCap,
            float fraction
        )
        {
            if (menteeRowParent == null || menteeRowPrefab == null)
                return;

            foreach (Transform child in menteeRowParent)
                Destroy(child.gameObject);

            foreach (var id in group.menteePlayerIds)
            {
                var mentee = state.GetPlayer(id);
                var row = Instantiate(menteeRowPrefab, menteeRowParent);
                row.Setup(
                    mentee != null ? PlayerName(mentee) : $"ID {id}",
                    mentorAttrs,
                    mentee?.hiddenAttrs,
                    rateCap,
                    fraction
                );
            }
        }

        private static string PlayerName(Player p) => p.info?.lastName ?? $"P{p.id}";
    }
}
