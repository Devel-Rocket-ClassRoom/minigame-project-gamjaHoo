// GameState.cs
// 게임 전체 상태의 단일 진실의 원천 (design-decisions.md #2). 세이브 파일은 결국 이것 하나의 직렬화 결과.
// 런타임 인덱스(_playerById, _clubById)는 직렬화 제외 — 로드 후 BuildIndexes 호출 필요.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FMLite.Domain
{
    [Serializable]
    public class GameState
    {
        // 세이브 버전 (design-decisions.md #52 / algorithms.md V0.5-8).
        // 0 = V0.1 구형 세이브 (필드 없음 → JSON 역직렬화 시 0으로 수신).
        // 2 = V0.5. SaveSystem.Save 가 직렬화 직전 CurrentVersion 으로 스탬프.
        public int saveVersion;

        // 메타 필드
        public DateTime currentDate;
        public int userClubId = -1; // -1 = "선택 안 됨" sentinel. UI 구단 선택 후 설정.
        public int rerollTokens;
        public int randomSeed;

        // 모든 player id 발급의 단일 진실의 원천 (design-decisions.md #31).
        // ClubGen / Reroll / YouthIntake 등 어떤 호출자든 이 카운터로 id 부여 후 갱신.
        // Stateless 원칙 유지: 호출자가 nextPlayerId 를 읽고 갱신. 시스템(ClubGen 등)
        // 자체는 startPlayerId 파라미터로 받음.
        public int nextPlayerId = 1;

        // 모든 YouthIntake id 발급의 단일 진실의 원천 (design-decisions.md #36).
        // 시즌별 메인(6/15) / 보조(1/15) 인스펙션이 누적 단조증가.
        // algorithms.md #4 1단계 시드 공식에 들어가 풀 결정성 기반.
        public int nextIntakeId = 1;

        // 모든 TransferOffer id 발급의 단일 진실의 원천 (algorithms.md #3.1).
        // SubmitOffer 호출 시 이 카운터로 id 부여 후 +1.
        // AI 응답 시드에 들어가 결정성 기반.
        public int nextOfferId = 1;

        // 마스터 리스트 (직렬화 대상)
        public List<Player> allPlayers = new List<Player>();
        public List<Club> allClubs = new List<Club>();
        public List<League> leagues = new List<League>();
        public List<TransferOffer> activeOffers = new List<TransferOffer>();

        // V0.5 신규 (design-decisions.md #43, #51)
        public List<Promise> activePromises = new List<Promise>();
        public List<SeasonAward> activeAwards = new List<SeasonAward>();
        public int managerReputation = 50; // 0-100
        public int nextPromiseId = 1;
        public int nextAwardId = 1;
        public int nextMentoringGroupId = 1;

        // V1.0 신규 (design-decisions.md #66)
        public List<InboxItem> inbox = new List<InboxItem>();
        public int nextInboxId = 1;

        // 런타임 인덱스 (직렬화 제외, 로드 후 BuildIndexes 필요)
        [JsonIgnore]
        private Dictionary<int, Player> _playerById;

        [JsonIgnore]
        private Dictionary<int, Club> _clubById;

        public void BuildIndexes()
        {
            _playerById = new Dictionary<int, Player>(allPlayers.Count);
            foreach (var p in allPlayers)
                _playerById[p.id] = p;

            _clubById = new Dictionary<int, Club>(allClubs.Count);
            foreach (var c in allClubs)
                _clubById[c.id] = c;
        }

        public Player GetPlayer(int id)
        {
            if (_playerById == null)
                BuildIndexes();
            return _playerById.TryGetValue(id, out var p) ? p : null;
        }

        public Club GetClub(int id)
        {
            if (_clubById == null)
                BuildIndexes();
            return _clubById.TryGetValue(id, out var c) ? c : null;
        }

        public void AddPlayer(Player p)
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));
            allPlayers.Add(p);
            if (_playerById == null)
                BuildIndexes();
            else
                _playerById[p.id] = p;
        }

        public bool RemovePlayer(int id)
        {
            if (_playerById == null)
                BuildIndexes();
            if (!_playerById.TryGetValue(id, out var p))
                return false;
            allPlayers.Remove(p);
            _playerById.Remove(id);
            return true;
        }

        public void AddClub(Club c)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c));
            allClubs.Add(c);
            if (_clubById == null)
                BuildIndexes();
            else
                _clubById[c.id] = c;
        }

        public bool RemoveClub(int id)
        {
            if (_clubById == null)
                BuildIndexes();
            if (!_clubById.TryGetValue(id, out var c))
                return false;
            allClubs.Remove(c);
            _clubById.Remove(id);
            return true;
        }
    }
}
