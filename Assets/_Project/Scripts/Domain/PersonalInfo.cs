// PersonalInfo.cs
// 선수 개인 정보 (이름 / 생년월일 / 국적 / 포지션 / 선호 발).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class PersonalInfo
    {
        public string firstName;
        public string lastName;
        public DateTime birthDate;
        public string nationalityCode;
        public Position primaryPosition;
        public List<Position> secondaryPositions = new List<Position>();
        public Foot preferredFoot;
    }
}
