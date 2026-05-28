// CaptainSystem.cs
// J.6 — 캡틴/부캡틴 자동/수동 배정.
// 자동: score = leadership + min(age,35)*0.5 + contractYearsLeft*2.0
// 수동: Assign(club, playerId, isVice)
// 라커룸 효과는 MoraleSystem 에서 dressingRoomCaptainLeadershipBonus 로 처리.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class CaptainSystem
    {
        // 자동 배정 — 현재 captain/viceCaptain 이 squad 에 없을 때만 덮어씀.
        public static void AssignAuto(Club club, GameState state)
        {
            if (club?.season == null || state == null)
                return;

            var ranked = RankPlayers(club, state, state.currentDate);
            if (ranked.Count == 0)
                return;

            if (!IsInSquad(club.season.captainPlayerId, club))
                club.season.captainPlayerId = ranked[0].id;

            if (
                !IsInSquad(club.season.viceCaptainPlayerId, club)
                || club.season.viceCaptainPlayerId == club.season.captainPlayerId
            )
            {
                var vice = ranked.FirstOrDefault(p => p.id != club.season.captainPlayerId);
                club.season.viceCaptainPlayerId = vice?.id ?? -1;
            }
        }

        // 수동 배정. isVice=false: 캡틴, isVice=true: 부캡틴.
        // 동일 선수가 캡틴·부캡틴 동시 할당되면 나머지 → -1.
        public static void Assign(Club club, int playerId, bool isVice)
        {
            if (club?.season == null)
                return;

            if (!isVice)
            {
                club.season.captainPlayerId = playerId;
                if (club.season.viceCaptainPlayerId == playerId)
                    club.season.viceCaptainPlayerId = -1;
            }
            else
            {
                club.season.viceCaptainPlayerId = playerId;
                if (club.season.captainPlayerId == playerId)
                    club.season.captainPlayerId = -1;
            }
        }

        // score = leadership + min(age, 35) * 0.5 + max(contractYearsLeft, 0) * 2.0
        public static float Score(Player p, DateTime currentDate)
        {
            if (p?.info == null || p.contract == null || p.stats?.mental == null)
                return 0f;
            float age = (float)((currentDate - p.info.birthDate).TotalDays / 365.25);
            float yearsLeft = (float)((p.contract.endDate - currentDate).TotalDays / 365.25);
            return p.stats.mental.leadership
                + Math.Min(age, 35f) * 0.5f
                + Math.Max(yearsLeft, 0f) * 2.0f;
        }

        private static List<Player> RankPlayers(Club club, GameState state, DateTime currentDate) =>
            club
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .Where(p => p != null)
                .OrderByDescending(p => Score(p, currentDate))
                .ToList();

        private static bool IsInSquad(int playerId, Club club) =>
            playerId >= 0 && club.seniorSquadIds.Contains(playerId);
    }
}
