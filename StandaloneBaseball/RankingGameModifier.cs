#nullable enable annotations

using System;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class RankingGameModifier
    {
        public static readonly RankingGameModifier None = new RankingGameModifier();

        public Guid AwayTeamId { get; private set; }
        public Guid HomeTeamId { get; private set; }
        public int AwayBoostPercent { get; private set; }
        public int HomeBoostPercent { get; private set; }

        public int BoostForTeam(Team? team)
            => team == null ? 0 : BoostForTeam(team.Id);

        public int BoostForTeam(Guid teamId)
        {
            if (teamId == Guid.Empty)
                return 0;
            if (teamId == AwayTeamId)
                return AwayBoostPercent;
            if (teamId == HomeTeamId)
                return HomeBoostPercent;
            return 0;
        }

        public int Apply(Team? team, int rating)
            => Apply(rating, BoostForTeam(team));

        public static int Apply(int rating, int boostPercent)
        {
            rating = Math.Clamp(rating, 0, 99);
            if (boostPercent <= 0)
                return rating;
            return Math.Clamp((int)Math.Round(rating * (1.0 + boostPercent / 100.0)), 0, 99);
        }

        public static RankingGameModifier FromSeason(Season? season, Team? away, Team? home)
        {
            if (season == null || away == null || home == null)
                return None;

            var poll = RankingEngine.LatestRegularSeasonPoll(season);
            if (poll?.Rankings == null || poll.Rankings.Count == 0)
                return None;

            int officialCount = Math.Min(RankingEngine.OfficialPollSize, poll.Rankings.Count);
            int awayRank = poll.Rankings.FirstOrDefault(r => r.TeamId == away.Id)?.Rank ?? int.MaxValue;
            int homeRank = poll.Rankings.FirstOrDefault(r => r.TeamId == home.Id)?.Rank ?? int.MaxValue;
            return new RankingGameModifier
            {
                AwayTeamId = away.Id,
                HomeTeamId = home.Id,
                AwayBoostPercent = BoostForRank(awayRank, homeRank, officialCount),
                HomeBoostPercent = BoostForRank(homeRank, awayRank, officialCount)
            };
        }

        private static int BoostForRank(int rank, int opponentRank, int officialCount)
        {
            if (rank <= 0 || rank == int.MaxValue)
                return 0;

            int top25Cutoff = Math.Min(25, officialCount);
            if (rank <= 10 && opponentRank > 10)
                return 5;
            if (rank <= top25Cutoff && rank > 10 && opponentRank > top25Cutoff)
                return 1;
            return 0;
        }
    }
}
