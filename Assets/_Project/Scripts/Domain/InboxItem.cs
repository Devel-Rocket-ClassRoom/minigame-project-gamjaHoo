// InboxItem.cs
// V1.0 인박스 도메인 단위. GameState.inbox 에 영구 저장 (design-decisions.md #66).
// FMLite.UI.InboxItem (MonoBehaviour, V0.5 Dashboard 행) 과 네임스페이스가 다름 — 컴파일 충돌 없음.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class InboxItem
    {
        public int id;
        public InboxCategory category;
        public InboxPriority priority;
        public DateTime createdAt;
        public DateTime? deadline;
        public bool isRead;
        public string titleKey;
        public Dictionary<string, string> titleArgs;
        public string bodyKey;
        public Dictionary<string, string> bodyArgs;
        public InboxAction action;
        public string actionTargetSceneOrDialogId;
    }

    public enum InboxCategory
    {
        Match,
        Transfer,
        Morale,
        Board,
        Youth,
        Cup,
        Award,
    }

    public enum InboxPriority
    {
        Low,
        Medium,
        High,
        RequiresAction,
    }

    public enum InboxAction
    {
        None,
        OpenScene,
        OpenDialog,
    }
}
