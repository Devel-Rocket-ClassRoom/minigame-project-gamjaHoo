// MentoringGroupItem.cs
// MentoringScene — 그룹 목록의 단일 아이템 (멘토 이름 + 멘티 목록 + 해체 버튼).

using System;
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

        [SerializeField]
        private TMP_Text menteesLabel;

        [SerializeField]
        private Button dissolveButton;

        public void Setup(MentoringGroup group, GameState state, Action<int> onDissolve)
        {
            var mentor = state.GetPlayer(group.mentorPlayerId);
            if (mentorLabel != null)
                mentorLabel.text =
                    mentor != null
                        ? $"{mentor.info?.lastName ?? $"P{mentor.id}"} (멘토)"
                        : $"ID {group.mentorPlayerId}";

            if (menteesLabel != null)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var id in group.menteePlayerIds)
                {
                    var p = state.GetPlayer(id);
                    names.Add(p != null ? (p.info?.lastName ?? $"P{id}") : $"ID {id}");
                }
                menteesLabel.text = string.Join(", ", names);
            }

            if (dissolveButton != null)
            {
                int groupId = group.id;
                dissolveButton.onClick.RemoveAllListeners();
                dissolveButton.onClick.AddListener(() => onDissolve(groupId));
            }
        }
    }
}
