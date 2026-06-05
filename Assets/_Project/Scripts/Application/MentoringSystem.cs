// MentoringSystem.cs
// algorithms.md V0.5-4 Mentoring — Hidden Attributes 수렴 (월 1회).
// Stateless 시스템 (design-decisions.md #3).
// 대상 Hidden Attrs: professionalism / ambition / loyalty (design-decisions.md #50).
// 매월 1일 DailyProcessor.Run 이 RunMentoring 호출.

using System;
using System.Collections.Generic;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class MentoringSystem
    {
        // ── 월 1회 틱 ──────────────────────────────────────────────────

        public static void RunMentoring(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            foreach (var club in state.allClubs)
            {
                if (club.season?.mentoringGroups == null)
                    continue;

                foreach (var group in club.season.mentoringGroups)
                {
                    var mentor = state.GetPlayer(group.mentorPlayerId);
                    if (mentor?.hiddenAttrs == null)
                        continue;

                    foreach (var menteeId in group.menteePlayerIds)
                    {
                        var mentee = state.GetPlayer(menteeId);
                        if (mentee?.hiddenAttrs == null)
                            continue;

                        ConvergeAttrs(
                            mentor.hiddenAttrs,
                            mentee.hiddenAttrs,
                            balance.mentoringRateModifier,
                            balance.mentoringConvergenceFraction
                        );
                    }
                }
            }
        }

        // ── 그룹 관리 API ──────────────────────────────────────────────

        public static MentoringGroup AddGroup(
            Club club,
            int mentorId,
            IList<int> menteeIds,
            GameState state
        )
        {
            if (club == null)
                throw new ArgumentNullException(nameof(club));
            if (menteeIds == null || menteeIds.Count == 0)
                throw new ArgumentException("멘티가 1명 이상이어야 함", nameof(menteeIds));
            if (menteeIds.Count > 3)
                throw new ArgumentException("멘티는 최대 3명 (design-decisions.md #50)", nameof(menteeIds));
            if (menteeIds.Contains(mentorId))
                throw new ArgumentException("멘토가 자기 자신의 멘티가 될 수 없음");

            // 이미 멘토로 참여 중인지 검사
            foreach (var g in club.season.mentoringGroups)
                if (g.mentorPlayerId == mentorId)
                    throw new InvalidOperationException($"mentorId={mentorId} 는 이미 멘토 그룹 존재");

            // 이미 다른 그룹 멘티인지 검사
            var usedMentees = new HashSet<int>();
            foreach (var g in club.season.mentoringGroups)
                foreach (var id in g.menteePlayerIds)
                    usedMentees.Add(id);

            foreach (var id in menteeIds)
                if (usedMentees.Contains(id))
                    throw new InvalidOperationException($"menteeId={id} 는 이미 다른 그룹에 속함");

            var group = new MentoringGroup
            {
                id = state.nextMentoringGroupId++,
                mentorPlayerId = mentorId,
                menteePlayerIds = new System.Collections.Generic.List<int>(menteeIds),
                startedAt = state.currentDate,
            };
            club.season.mentoringGroups.Add(group);
            return group;
        }

        public static void RemoveGroup(Club club, int groupId)
        {
            if (club == null)
                throw new ArgumentNullException(nameof(club));

            int removed = club.season.mentoringGroups.RemoveAll(g => g.id == groupId);
            if (removed == 0)
                throw new InvalidOperationException($"groupId={groupId} 를 찾을 수 없음");
        }

        // ── I.2 진행률 헬퍼 (UI 용, 순수 함수) ─────────────────────────

        // 이번 달 수렴 스텝 (mentor 방향). 격차 비례 (차이 클수록 빠름) + rateCap 상한.
        // RunMentoring 의 ConvergeAttrs 와 동일 로직.
        public static int ProjectedMonthlyStep(
            int mentorVal,
            int menteeVal,
            int rateCap,
            float fraction
        )
        {
            int diff = mentorVal - menteeVal;
            if (diff == 0)
                return 0;
            int mag = Math.Abs(diff);
            // 격차 비례: |diff| × fraction, 반올림. 최소 1 (멘토 수치까지 결국 도달).
            int raw = (int)Math.Round(mag * (double)fraction, MidpointRounding.AwayFromZero);
            int step = Math.Clamp(raw, 1, Math.Min(Math.Max(rateCap, 1), mag));
            return (diff > 0 ? 1 : -1) * step;
        }

        // 수렴 진행률 % — mentee 가 mentor(목표=상한)에 얼마나 근접했나. mentee >= mentor 면 100.
        public static float ConvergencePercent(int mentorVal, int menteeVal)
        {
            if (mentorVal <= 0 || menteeVal >= mentorVal)
                return 100f;
            return Math.Clamp((float)menteeVal / mentorVal * 100f, 0f, 100f);
        }

        // ── 내부 ───────────────────────────────────────────────────────

        private static void ConvergeAttrs(
            HiddenAttributes mentor,
            HiddenAttributes mentee,
            int rateCap,
            float fraction
        )
        {
            mentee.professionalism = Math.Clamp(
                mentee.professionalism
                    + ProjectedMonthlyStep(
                        mentor.professionalism,
                        mentee.professionalism,
                        rateCap,
                        fraction
                    ),
                1,
                100
            );
            mentee.ambition = Math.Clamp(
                mentee.ambition
                    + ProjectedMonthlyStep(mentor.ambition, mentee.ambition, rateCap, fraction),
                1,
                100
            );
            mentee.loyalty = Math.Clamp(
                mentee.loyalty
                    + ProjectedMonthlyStep(mentor.loyalty, mentee.loyalty, rateCap, fraction),
                1,
                100
            );
        }
    }
}
