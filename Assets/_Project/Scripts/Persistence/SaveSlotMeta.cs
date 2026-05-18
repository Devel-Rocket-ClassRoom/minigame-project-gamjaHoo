// SaveSlotMeta.cs
// 슬롯 목록 화면용 메타데이터. state.json 과 별도로 meta.json 에 저장되어
// 무거운 GameState 역직렬화 없이 슬롯 표시 정보를 빠르게 조회 가능.

using System;

namespace FMLite.Persistence
{
    [Serializable]
    public class SaveSlotMeta
    {
        public string slotName;
        public DateTime savedAt;
        public DateTime currentDate;
        public int userClubId;
        public string userClubName;
    }
}
