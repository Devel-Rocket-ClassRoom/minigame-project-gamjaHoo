// SeasonRecord.cs
// 명예의 전당 — careers/{uid}/seasons/{pushKey} 한 시즌 기록 DTO.
// timestamp 는 ServerValue.Timestamp 로 채워진 Unix ms (long).

using System;

namespace FMLite.Persistence.Cloud
{
    [Serializable]
    public class SeasonRecord
    {
        public int year;
        public string clubName;
        public int position;
        public int points;
        public long timestamp; // Unix milliseconds (서버 기록)
    }
}
