// Stats.cs
// 선수 능력치. 4개 카테고리(Technical / Mental / Physical / Goalkeeping).
// 각 카테고리에 ApplyToAll(Func<int,int>) 헬퍼로 일괄 변환 가능.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class Stats
    {
        public TechnicalStats technical = new TechnicalStats();
        public MentalStats mental = new MentalStats();
        public PhysicalStats physical = new PhysicalStats();
        public GoalkeepingStats gk = new GoalkeepingStats();
    }

    [Serializable]
    public class TechnicalStats
    {
        public int passing;
        public int shooting;
        public int tackling;
        public int dribbling;
        public int heading;
        public int crossing;
        public int firstTouch;
        public int finishing;
        public int longShots;
        public int freeKickAccuracy;
        public int penaltyTaking;
        public int corners;

        public void ApplyToAll(Func<int, int> modifier)
        {
            passing = modifier(passing);
            shooting = modifier(shooting);
            tackling = modifier(tackling);
            dribbling = modifier(dribbling);
            heading = modifier(heading);
            crossing = modifier(crossing);
            firstTouch = modifier(firstTouch);
            finishing = modifier(finishing);
            longShots = modifier(longShots);
            freeKickAccuracy = modifier(freeKickAccuracy);
            penaltyTaking = modifier(penaltyTaking);
            corners = modifier(corners);
        }
    }

    [Serializable]
    public class MentalStats
    {
        public int vision;
        public int anticipation;
        public int composure;
        public int concentration;
        public int decisions;
        public int determination;
        public int leadership;
        public int offTheBall;
        public int positioning;
        public int teamwork;
        public int workRate;
        public int aggression;

        public void ApplyToAll(Func<int, int> modifier)
        {
            vision = modifier(vision);
            anticipation = modifier(anticipation);
            composure = modifier(composure);
            concentration = modifier(concentration);
            decisions = modifier(decisions);
            determination = modifier(determination);
            leadership = modifier(leadership);
            offTheBall = modifier(offTheBall);
            positioning = modifier(positioning);
            teamwork = modifier(teamwork);
            workRate = modifier(workRate);
            aggression = modifier(aggression);
        }
    }

    [Serializable]
    public class PhysicalStats
    {
        public int acceleration;
        public int agility;
        public int balance;
        public int jumping;
        public int naturalFitness;
        public int pace;
        public int stamina;
        public int strength;

        public void ApplyToAll(Func<int, int> modifier)
        {
            acceleration = modifier(acceleration);
            agility = modifier(agility);
            balance = modifier(balance);
            jumping = modifier(jumping);
            naturalFitness = modifier(naturalFitness);
            pace = modifier(pace);
            stamina = modifier(stamina);
            strength = modifier(strength);
        }
    }

    [Serializable]
    public class GoalkeepingStats
    {
        public int aerialReach;
        public int commandOfArea;
        public int communication;
        public int eccentricity;
        public int handling;
        public int kicking;
        public int oneOnOnes;
        public int reflexes;
        public int rushingOut;
        public int throwing;

        public void ApplyToAll(Func<int, int> modifier)
        {
            aerialReach = modifier(aerialReach);
            commandOfArea = modifier(commandOfArea);
            communication = modifier(communication);
            eccentricity = modifier(eccentricity);
            handling = modifier(handling);
            kicking = modifier(kicking);
            oneOnOnes = modifier(oneOnOnes);
            reflexes = modifier(reflexes);
            rushingOut = modifier(rushingOut);
            throwing = modifier(throwing);
        }
    }
}
