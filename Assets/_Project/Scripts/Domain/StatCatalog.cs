// StatCatalog.cs
// 49 stat 의 단일 카탈로그 — fieldPath / 로컬라이즈 키 / 카테고리 / 값 접근자.
// Stage F (#472): Transfer 세부 stat 임계 필터 (SearchPlayers) + 필터 모달 stat 드롭다운이 공유.
//   - fieldPath = GrowthSystem.GetStatChange / StatSnapshot 와 동일 표기 ("technical.passing").
//   - labelKey  = PlayerProfileController 의 stat_* 로컬라이즈 키와 동일.
//   - accessor  = 리플렉션 없이 값 조회 (3000+ 선수 검색 시 성능).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    public enum StatCategory
    {
        Technical,
        Mental,
        Physical,
        Goalkeeping,
    }

    public sealed class StatDescriptor
    {
        public readonly string fieldPath; // "technical.passing"
        public readonly string labelKey; // "stat_passing"
        public readonly StatCategory category;
        private readonly Func<Stats, int> _accessor;

        public StatDescriptor(
            string fieldPath,
            string labelKey,
            StatCategory category,
            Func<Stats, int> accessor
        )
        {
            this.fieldPath = fieldPath;
            this.labelKey = labelKey;
            this.category = category;
            _accessor = accessor;
        }

        public int Read(Stats stats) => stats == null ? 0 : _accessor(stats);
    }

    public static class StatCatalog
    {
        // 49 stat (Technical 14 / Mental 14 / Physical 8 / Goalkeeping 13).
        public static readonly IReadOnlyList<StatDescriptor> All = new List<StatDescriptor>
        {
            // Technical
            new(
                "technical.passing",
                "stat_passing",
                StatCategory.Technical,
                s => s.technical.passing
            ),
            new(
                "technical.tackling",
                "stat_tackling",
                StatCategory.Technical,
                s => s.technical.tackling
            ),
            new(
                "technical.dribbling",
                "stat_dribbling",
                StatCategory.Technical,
                s => s.technical.dribbling
            ),
            new(
                "technical.heading",
                "stat_heading",
                StatCategory.Technical,
                s => s.technical.heading
            ),
            new(
                "technical.crossing",
                "stat_crossing",
                StatCategory.Technical,
                s => s.technical.crossing
            ),
            new(
                "technical.firstTouch",
                "stat_first_touch",
                StatCategory.Technical,
                s => s.technical.firstTouch
            ),
            new(
                "technical.finishing",
                "stat_finishing",
                StatCategory.Technical,
                s => s.technical.finishing
            ),
            new(
                "technical.longShots",
                "stat_long_shots",
                StatCategory.Technical,
                s => s.technical.longShots
            ),
            new(
                "technical.freeKickTaking",
                "stat_free_kick",
                StatCategory.Technical,
                s => s.technical.freeKickTaking
            ),
            new(
                "technical.penaltyTaking",
                "stat_penalty",
                StatCategory.Technical,
                s => s.technical.penaltyTaking
            ),
            new(
                "technical.corners",
                "stat_corners",
                StatCategory.Technical,
                s => s.technical.corners
            ),
            new(
                "technical.marking",
                "stat_marking",
                StatCategory.Technical,
                s => s.technical.marking
            ),
            new(
                "technical.technique",
                "stat_technique",
                StatCategory.Technical,
                s => s.technical.technique
            ),
            new(
                "technical.longThrows",
                "stat_long_throws",
                StatCategory.Technical,
                s => s.technical.longThrows
            ),
            // Mental
            new("mental.vision", "stat_vision", StatCategory.Mental, s => s.mental.vision),
            new(
                "mental.anticipation",
                "stat_anticipation",
                StatCategory.Mental,
                s => s.mental.anticipation
            ),
            new("mental.composure", "stat_composure", StatCategory.Mental, s => s.mental.composure),
            new(
                "mental.concentration",
                "stat_concentration",
                StatCategory.Mental,
                s => s.mental.concentration
            ),
            new("mental.decisions", "stat_decisions", StatCategory.Mental, s => s.mental.decisions),
            new(
                "mental.determination",
                "stat_determination",
                StatCategory.Mental,
                s => s.mental.determination
            ),
            new(
                "mental.leadership",
                "stat_leadership",
                StatCategory.Mental,
                s => s.mental.leadership
            ),
            new(
                "mental.offTheBall",
                "stat_off_the_ball",
                StatCategory.Mental,
                s => s.mental.offTheBall
            ),
            new(
                "mental.positioning",
                "stat_positioning",
                StatCategory.Mental,
                s => s.mental.positioning
            ),
            new("mental.teamwork", "stat_teamwork", StatCategory.Mental, s => s.mental.teamwork),
            new("mental.workRate", "stat_work_rate", StatCategory.Mental, s => s.mental.workRate),
            new(
                "mental.aggression",
                "stat_aggression",
                StatCategory.Mental,
                s => s.mental.aggression
            ),
            new("mental.bravery", "stat_bravery", StatCategory.Mental, s => s.mental.bravery),
            new("mental.flair", "stat_flair", StatCategory.Mental, s => s.mental.flair),
            // Physical
            new(
                "physical.acceleration",
                "stat_acceleration",
                StatCategory.Physical,
                s => s.physical.acceleration
            ),
            new("physical.agility", "stat_agility", StatCategory.Physical, s => s.physical.agility),
            new("physical.balance", "stat_balance", StatCategory.Physical, s => s.physical.balance),
            new(
                "physical.jumpingReach",
                "stat_jumping",
                StatCategory.Physical,
                s => s.physical.jumpingReach
            ),
            new(
                "physical.naturalFitness",
                "stat_natural_fitness",
                StatCategory.Physical,
                s => s.physical.naturalFitness
            ),
            new("physical.pace", "stat_pace", StatCategory.Physical, s => s.physical.pace),
            new("physical.stamina", "stat_stamina", StatCategory.Physical, s => s.physical.stamina),
            new(
                "physical.strength",
                "stat_strength",
                StatCategory.Physical,
                s => s.physical.strength
            ),
            // Goalkeeping
            new(
                "gk.aerialReach",
                "stat_aerial_reach",
                StatCategory.Goalkeeping,
                s => s.gk.aerialReach
            ),
            new(
                "gk.commandOfArea",
                "stat_command_of_area",
                StatCategory.Goalkeeping,
                s => s.gk.commandOfArea
            ),
            new(
                "gk.communication",
                "stat_communication",
                StatCategory.Goalkeeping,
                s => s.gk.communication
            ),
            new(
                "gk.eccentricity",
                "stat_eccentricity",
                StatCategory.Goalkeeping,
                s => s.gk.eccentricity
            ),
            new("gk.handling", "stat_handling", StatCategory.Goalkeeping, s => s.gk.handling),
            new("gk.kicking", "stat_kicking", StatCategory.Goalkeeping, s => s.gk.kicking),
            new("gk.oneOnOnes", "stat_one_on_ones", StatCategory.Goalkeeping, s => s.gk.oneOnOnes),
            new("gk.reflexes", "stat_reflexes", StatCategory.Goalkeeping, s => s.gk.reflexes),
            new(
                "gk.rushingOut",
                "stat_rushing_out",
                StatCategory.Goalkeeping,
                s => s.gk.rushingOut
            ),
            new("gk.throwing", "stat_throwing", StatCategory.Goalkeeping, s => s.gk.throwing),
            new(
                "gk.firstTouchGk",
                "stat_first_touch_gk",
                StatCategory.Goalkeeping,
                s => s.gk.firstTouchGk
            ),
            new("gk.passingGk", "stat_passing_gk", StatCategory.Goalkeeping, s => s.gk.passingGk),
            new(
                "gk.punchingTendency",
                "stat_punching_tendency",
                StatCategory.Goalkeeping,
                s => s.gk.punchingTendency
            ),
        };

        private static readonly Dictionary<string, StatDescriptor> ByFieldPath = BuildIndex();

        private static Dictionary<string, StatDescriptor> BuildIndex()
        {
            var dict = new Dictionary<string, StatDescriptor>(All.Count);
            foreach (var d in All)
                dict[d.fieldPath] = d;
            return dict;
        }

        public static StatDescriptor Get(string fieldPath)
        {
            if (string.IsNullOrEmpty(fieldPath))
                return null;
            return ByFieldPath.TryGetValue(fieldPath, out var d) ? d : null;
        }

        // fieldPath 로 stat 값 조회. 미지정/미존재 시 0.
        public static int Read(Stats stats, string fieldPath) => Get(fieldPath)?.Read(stats) ?? 0;
    }
}
