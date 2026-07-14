#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class RankingEngine
    {
        public const int OfficialPollSize = 25;

        public static SeasonRankingPoll GeneratePoll(LeagueFile league, Season season, RankingPollType type, int week = 0)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (season == null) throw new ArgumentNullException(nameof(season));

            season.RankingPolls ??= new List<SeasonRankingPoll>();
            var teams = league.Teams ?? new List<Team>();
            var previous = PreviousPoll(season, type, week);
            var previousRank = previous?.Rankings?.ToDictionary(r => r.TeamId, r => r.Rank) ?? new Dictionary<Guid, int>();
            var priorSeason = PriorSeason(league, season);
            var preseasonRank = PreseasonBaselineRanks(league, season, priorSeason);
            var previousTop25 = new HashSet<Guid>((previous?.Rankings ?? Enumerable.Empty<SeasonRankingEntry>())
                .Where(r => r.Rank <= Math.Min(OfficialPollSize, teams.Count))
                .Select(r => r.TeamId));
            if (previousTop25.Count == 0)
                previousTop25 = new HashSet<Guid>(preseasonRank.OrderBy(kv => kv.Value).Take(Math.Min(OfficialPollSize, teams.Count)).Select(kv => kv.Key));

            var rows = new List<SeasonRankingEntry>();
            foreach (var team in teams)
            {
                var record = BuildRecord(season, team.Id, type, week, previousTop25);
                double roster = TeamRosterScore(team);
                double coach = TeamCoachScore(team);
                double preScore = PreSeasonScore(team, priorSeason, team.Id);
                double pollScore = previousRank.TryGetValue(team.Id, out int prev)
                    ? Math.Max(0, teams.Count - prev + 1)
                    : Math.Max(0, teams.Count - preseasonRank.GetValueOrDefault(team.Id, teams.Count) + 1);
                double computer = ComputerScore(record, roster, coach);
                double finalBoost = type == RankingPollType.Final ? PlayoffFinishScore(season, team.Id) : 0;
                double score = type switch
                {
                    RankingPollType.PreSeason => preScore,
                    RankingPollType.Final => computer * 0.40 + finalBoost * 0.40 + (record.StrengthOfSchedule * 20.0 + record.RankedWins * 6.0) * 0.20,
                    _ => pollScore * 0.50 + computer * 0.50
                };

                rows.Add(new SeasonRankingEntry
                {
                    TeamId = team.Id,
                    TeamName = team.DisplayName,
                    PreviousRank = previousRank.TryGetValue(team.Id, out int oldRank) ? oldRank : 0,
                    Score = Math.Round(score, 2),
                    PollScore = Math.Round(pollScore, 2),
                    ComputerScore = Math.Round(computer, 2),
                    Wins = record.Wins,
                    Losses = record.Losses,
                    Ties = record.Ties,
                    RankedWins = record.RankedWins,
                    StrengthOfSchedule = Math.Round(record.StrengthOfSchedule, 3),
                    RunDifferential = record.RunDifferential,
                    Notes = RankingNotes(type, record, finalBoost)
                });
            }

            var ordered = rows
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.Wins)
                .ThenBy(r => r.Losses)
                .ThenByDescending(r => r.RunDifferential)
                .ThenBy(r => r.TeamName)
                .ToList();
            if (type == RankingPollType.Final && season.ChampionTeamId.HasValue)
            {
                ordered = ordered
                    .OrderByDescending(r => r.TeamId == season.ChampionTeamId.Value)
                    .ThenByDescending(r => r.Score)
                    .ThenByDescending(r => r.Wins)
                    .ThenBy(r => r.Losses)
                    .ThenByDescending(r => r.RunDifferential)
                    .ThenBy(r => r.TeamName)
                    .ToList();
            }

            AssignRanksWithTies(ordered, type == RankingPollType.Final ? season.ChampionTeamId : null);

            return new SeasonRankingPoll
            {
                Type = type,
                Week = type == RankingPollType.Weekly ? Math.Max(1, week) : 0,
                CreatedAt = DateTime.Now,
                Name = PollName(type, week),
                Rankings = ordered
            };
        }

        public static void SavePoll(Season season, SeasonRankingPoll poll)
        {
            if (season == null || poll == null)
                return;

            season.RankingPolls ??= new List<SeasonRankingPoll>();
            season.RankingPolls.RemoveAll(p => p.Type == poll.Type && p.Week == poll.Week);
            season.RankingPolls.Add(poll);
            season.RankingPolls = season.RankingPolls
                .OrderBy(p => PollSortOrder(p.Type))
                .ThenBy(p => p.Week)
                .ThenBy(p => p.CreatedAt)
                .ToList();
        }

        public static SeasonRankingPoll LatestPoll(Season season)
        {
            return (season?.RankingPolls ?? Enumerable.Empty<SeasonRankingPoll>())
                .OrderByDescending(p => PollSortOrder(p.Type))
                .ThenByDescending(p => p.Week)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefault();
        }

        public static SeasonRankingPoll LatestRegularSeasonPoll(Season season)
        {
            return (season?.RankingPolls ?? Enumerable.Empty<SeasonRankingPoll>())
                .Where(p => p.Type == RankingPollType.Weekly || p.Type == RankingPollType.PreSeason)
                .OrderByDescending(p => p.Type == RankingPollType.Weekly)
                .ThenByDescending(p => p.Week)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefault();
        }

        public static int TeamRank(Season season, Guid teamId)
        {
            var poll = LatestPoll(season);
            var entry = poll?.Rankings?.FirstOrDefault(r => r.TeamId == teamId);
            return entry?.Rank ?? int.MaxValue;
        }

        public static int TeamPlayoffSeedRank(Season season, Guid teamId)
        {
            var poll = LatestRegularSeasonPoll(season);
            var entry = poll?.Rankings?.FirstOrDefault(r => r.TeamId == teamId);
            return entry?.Rank ?? int.MaxValue;
        }

        public static string PollName(RankingPollType type, int week)
        {
            return type switch
            {
                RankingPollType.PreSeason => "Pre-Season Poll",
                RankingPollType.Final => "Final Poll",
                _ => "Week " + Math.Max(1, week) + " Poll"
            };
        }

        public static int OfficialCount(LeagueFile league)
            => Math.Min(OfficialPollSize, league?.Teams?.Count ?? 0);

        private static void AssignRanksWithTies(List<SeasonRankingEntry> ordered, Guid? lockedFirstTeamId)
        {
            if (ordered == null)
                return;

            int index = 0;
            while (index < ordered.Count)
            {
                var current = ordered[index];
                int rank = index + 1;
                int end = index + 1;
                if (!lockedFirstTeamId.HasValue || current.TeamId != lockedFirstTeamId.Value)
                {
                    while (end < ordered.Count &&
                        (!lockedFirstTeamId.HasValue || ordered[end].TeamId != lockedFirstTeamId.Value) &&
                        Math.Abs(ordered[end].Score - current.Score) < 0.001)
                    {
                        end++;
                    }
                }

                bool tied = end - index > 1;
                for (int i = index; i < end; i++)
                {
                    ordered[i].Rank = rank;
                    if (tied && ordered[i].Notes.IndexOf("Tied for #", StringComparison.OrdinalIgnoreCase) < 0)
                        ordered[i].Notes = AppendNote(ordered[i].Notes, "Tied for #" + rank);
                }

                index = end;
            }
        }

        private static string AppendNote(string notes, string note)
            => string.IsNullOrWhiteSpace(notes) ? note : notes + "; " + note;

        private static SeasonRankingPoll PreviousPoll(Season season, RankingPollType type, int week)
        {
            var polls = season?.RankingPolls ?? new List<SeasonRankingPoll>();
            if (type == RankingPollType.PreSeason)
                return null;
            if (type == RankingPollType.Weekly)
            {
                return polls
                    .Where(p => p.Type == RankingPollType.Weekly && p.Week < week || p.Type == RankingPollType.PreSeason)
                    .OrderByDescending(p => PollSortOrder(p.Type))
                    .ThenByDescending(p => p.Week)
                    .ThenByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }
            return polls
                .Where(p => p.Type == RankingPollType.Weekly || p.Type == RankingPollType.PreSeason)
                .OrderByDescending(p => PollSortOrder(p.Type))
                .ThenByDescending(p => p.Week)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefault();
        }

        private static Dictionary<Guid, int> PreseasonBaselineRanks(LeagueFile league, Season season, Season priorSeason)
        {
            return (league.Teams ?? new List<Team>())
                .Select(t => new { t.Id, Score = PreSeasonScore(t, priorSeason, t.Id), Name = t.DisplayName })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Name)
                .Select((x, i) => new { x.Id, Rank = i + 1 })
                .ToDictionary(x => x.Id, x => x.Rank);
        }

        private static double PreSeasonScore(Team team, Season priorSeason, Guid teamId)
        {
            double roster = TeamRosterScore(team);
            if (priorSeason == null)
                return roster * 0.60 + SeniorClassScore(team) * 0.25 + TeamCoachScore(team) * 0.15;

            return roster * 0.60 + TeamCoachScore(team) * 0.18 + PriorSeasonScore(priorSeason, teamId) * 0.22;
        }

        private static Season PriorSeason(LeagueFile league, Season season)
        {
            var seasons = league?.Seasons ?? new List<Season>();
            int index = seasons.FindIndex(s => s.Id == season.Id);
            return index > 0 ? seasons[index - 1] : null;
        }

        private static double TeamRosterScore(Team team)
        {
            var players = (team?.Roster ?? new List<Player>()).Where(p => p != null).ToList();
            if (players.Count == 0)
                return 0;

            return players
                .OrderByDescending(p => p.Overall)
                .Take(Math.Min(30, players.Count))
                .Average(p => p.Overall);
        }

        private static double SeniorClassScore(Team team)
        {
            var seniors = (team?.Roster ?? new List<Player>())
                .Where(p => p != null && p.Classification == PlayerClassification.Senior)
                .ToList();
            if (seniors.Count == 0)
                return 0;

            double quality = seniors
                .OrderByDescending(p => p.Overall)
                .Take(Math.Min(10, seniors.Count))
                .Average(p => p.Overall);
            double depth = Math.Min(12, seniors.Count) / 12.0 * 15.0;
            return Math.Min(99.0, quality + depth);
        }

        private static double TeamCoachScore(Team team)
        {
            team?.NormalizeText();
            var coach = team?.Coaches?.FirstOrDefault(c => c.Id == team.CoachId) ?? team?.Coaches?.FirstOrDefault();
            return coach?.Style switch
            {
                CoachStyle.BelowAverage => 45,
                CoachStyle.AboveAverage => 75,
                CoachStyle.Championship => 95,
                _ => 60
            };
        }

        private static double PriorSeasonScore(Season season, Guid teamId)
        {
            if (season == null)
                return 50;

            int wins = 0;
            int losses = 0;
            int ties = 0;
            int runsFor = 0;
            int runsAgainst = 0;
            foreach (var game in season.Games ?? new List<GameResult>())
            {
                if (game.IsPlayoff)
                    continue;
                bool away = game.AwayTeamId == teamId;
                bool home = game.HomeTeamId == teamId;
                if (!away && !home)
                    continue;
                int teamRuns = away ? game.AwayScore : game.HomeScore;
                int oppRuns = away ? game.HomeScore : game.AwayScore;
                if (teamRuns > oppRuns) wins++;
                else if (oppRuns > teamRuns) losses++;
                else ties++;
                runsFor += teamRuns;
                runsAgainst += oppRuns;
            }

            int games = wins + losses + ties;
            double pct = games == 0 ? 0.500 : (wins + ties * 0.5) / games;
            double score = pct * 80 + Math.Clamp((runsFor - runsAgainst) / 10.0, -10, 10);
            if (season.ChampionTeamId == teamId)
                score += 25;
            var bestSeries = (season.Playoffs ?? new List<PlayoffSeries>())
                .Where(s => s.TeamAId == teamId || s.TeamBId == teamId)
                .OrderByDescending(s => s.Round)
                .FirstOrDefault();
            if (bestSeries != null)
                score += Math.Min(20, bestSeries.Round * 3);
            return score;
        }

        private static TeamRankRecord BuildRecord(Season season, Guid teamId, RankingPollType type, int week, HashSet<Guid> rankedTeams)
        {
            var record = new TeamRankRecord();
            var scheduleById = (season.Schedule ?? new List<ScheduledGame>()).ToDictionary(g => g.Id);
            var opponentRecords = PlayoffEngine.ComputeStandings(season, scheduleById.Values.SelectMany(g => new[] { g.AwayTeamId, g.HomeTeamId }))
                .ToDictionary(s => s.TeamId);

            foreach (var game in season.Games ?? new List<GameResult>())
            {
                if ((game.IsPlayoff && type != RankingPollType.Final) || !GameCountsForPoll(game, scheduleById, type, week))
                    continue;

                bool away = game.AwayTeamId == teamId;
                bool home = game.HomeTeamId == teamId;
                if (!away && !home)
                    continue;

                Guid opponentId = away ? game.HomeTeamId : game.AwayTeamId;
                int runsFor = away ? game.AwayScore : game.HomeScore;
                int runsAgainst = away ? game.HomeScore : game.AwayScore;
                bool won = runsFor > runsAgainst;
                bool tied = runsFor == runsAgainst;

                if (won) record.Wins++;
                else if (tied) record.Ties++;
                else record.Losses++;
                record.RunsFor += runsFor;
                record.RunsAgainst += runsAgainst;
                if (won && rankedTeams.Contains(opponentId))
                    record.RankedWins++;
                if (scheduleById.TryGetValue(game.ScheduledGameId.GetValueOrDefault(), out var scheduled))
                {
                    if (won && scheduled.Type == ScheduledGameType.District) record.DistrictWins++;
                    if (won && scheduled.Type == ScheduledGameType.Region) record.RegionWins++;
                    if (won && scheduled.Type == ScheduledGameType.Conference) record.ConferenceWins++;
                }
                if (opponentRecords.TryGetValue(opponentId, out var opp))
                    record.OpponentPcts.Add(opp.Pct);
            }

            return record;
        }

        private static bool GameCountsForPoll(GameResult game, Dictionary<Guid, ScheduledGame> scheduleById, RankingPollType type, int week)
        {
            if (type == RankingPollType.PreSeason)
                return false;
            if (type == RankingPollType.Final)
                return true;
            return game.ScheduledGameId.HasValue &&
                scheduleById.TryGetValue(game.ScheduledGameId.Value, out var scheduled) &&
                scheduled.Week <= week;
        }

        private static double ComputerScore(TeamRankRecord record, double roster, double coach)
        {
            double winPct = record.Games == 0 ? 0.500 : (record.Wins + record.Ties * 0.5) / record.Games;
            double runDiff = Math.Clamp(record.RunDifferential / 5.0, -15, 15);
            double categoryWins = record.DistrictWins + record.RegionWins * 0.8 + record.ConferenceWins * 0.6;
            return winPct * 35.0 +
                record.StrengthOfSchedule * 20.0 +
                runDiff +
                Math.Min(10.0, record.RankedWins * 4.0) +
                Math.Min(10.0, categoryWins) +
                roster * 0.05 +
                coach * 0.05;
        }

        private static double PlayoffFinishScore(Season season, Guid teamId)
        {
            double score = 0;
            if (season.ChampionTeamId == teamId)
                score += 100;

            var series = (season.Playoffs ?? new List<PlayoffSeries>())
                .Where(s => s.TeamAId == teamId || s.TeamBId == teamId)
                .OrderByDescending(s => s.Round)
                .FirstOrDefault();
            if (series != null)
            {
                score += series.Round * 10;
                if (series.WinnerTeamId == teamId)
                    score += 12;
            }

            return score;
        }

        private static string RankingNotes(RankingPollType type, TeamRankRecord record, double finalBoost)
        {
            if (type == RankingPollType.PreSeason)
                return "Varsity roster, senior class, coach level, and prior season when available";
            if (type == RankingPollType.Final)
                return "Final resume; playoff finish score " + finalBoost.ToString("0.0");
            return RecordText(record.Wins, record.Losses, record.Ties) + ", ranked wins " + record.RankedWins + ", SOS " + record.StrengthOfSchedule.ToString("0.000");
        }

        private static string RecordText(int wins, int losses, int ties)
            => ties > 0 ? wins + "-" + losses + "-" + ties : wins + "-" + losses;

        private static int PollSortOrder(RankingPollType type)
        {
            return type switch
            {
                RankingPollType.PreSeason => 0,
                RankingPollType.Weekly => 1,
                RankingPollType.Final => 2,
                _ => 0
            };
        }

        private sealed class TeamRankRecord
        {
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Ties { get; set; }
            public int RunsFor { get; set; }
            public int RunsAgainst { get; set; }
            public int RankedWins { get; set; }
            public int DistrictWins { get; set; }
            public int RegionWins { get; set; }
            public int ConferenceWins { get; set; }
            public List<double> OpponentPcts { get; } = new List<double>();
            public int Games => Wins + Losses + Ties;
            public int RunDifferential => RunsFor - RunsAgainst;
            public double StrengthOfSchedule => OpponentPcts.Count == 0 ? 0.500 : OpponentPcts.Average();
        }
    }
}
