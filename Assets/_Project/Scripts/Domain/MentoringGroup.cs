// MentoringGroup.cs
// 멘토링 그룹 — 베테랑 1명 + 멘티 1-3명 (design-decisions.md #50).
// 시즌당 professionalism / determination Hidden Attrs 수렴 처리.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class MentoringGroup
    {
        public int id;
        public int mentorPlayerId;
        public List<int> menteePlayerIds = new List<int>();
        public DateTime startedAt;
    }
}
