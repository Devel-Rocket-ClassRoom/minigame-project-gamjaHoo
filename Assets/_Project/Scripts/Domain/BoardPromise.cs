// BoardPromise.cs
// 보드와의 약속 (시즌 시작 영입 약속 등). SeasonState.boardPromises 에서 관리.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class BoardPromise
    {
        public int id;
        public PromiseType type;
        public DateTime madeAt;
        public PromiseStatus status;
        public Dictionary<string, int> targets = new Dictionary<string, int>();
    }
}
