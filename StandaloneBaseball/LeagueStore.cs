#nullable enable annotations

using System.IO;
using System.Linq;
using System.Text.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace StandaloneBaseball
{
    public sealed class LeagueBackupInfo
    {
        public string Path { get; set; } = "";
        public DateTime SavedAt { get; set; }
        public long SizeBytes { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public static class LeagueStore
    {
        public const string Extension = ".dbaseball.json";
        public const int CurrentSaveSchemaVersion = 1;
        private const int MaxBackupFiles = 12;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static void Save(string path, LeagueFile league)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A dynasty save path is required.", nameof(path));
            if (league == null)
                throw new ArgumentNullException(nameof(league));

            league.SaveSchemaVersion = CurrentSaveSchemaVersion;
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            string json = JsonSerializer.Serialize(league, Options);

            try
            {
                WriteAllTextDurable(tempPath, json);
                if (File.Exists(fullPath))
                    CreateBackup(fullPath);

                if (File.Exists(fullPath))
                    File.Replace(tempPath, fullPath, null, ignoreMetadataErrors: true);
                else
                {
                    File.Move(tempPath, fullPath);
                    CreateBackup(fullPath);
                }

                PruneBackups(fullPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static void WriteAllTextDurable(string path, string text)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream);
            writer.Write(text);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private static void CreateBackup(string sourcePath)
        {
            if (!File.Exists(sourcePath))
                return;

            string backupDirectory = BackupDirectoryFor(sourcePath);
            Directory.CreateDirectory(backupDirectory);
            string backupPath = Path.Combine(
                backupDirectory,
                Path.GetFileNameWithoutExtension(sourcePath) + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + Path.GetExtension(sourcePath) + ".bak");
            File.Copy(sourcePath, backupPath, overwrite: false);
        }

        private static void PruneBackups(string savePath)
        {
            string backupDirectory = BackupDirectoryFor(savePath);
            if (!Directory.Exists(backupDirectory))
                return;

            string prefix = Path.GetFileNameWithoutExtension(savePath) + ".";
            string suffix = Path.GetExtension(savePath) + ".bak";
            var backups = Directory.EnumerateFiles(backupDirectory, prefix + "*" + suffix)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.CreationTimeUtc)
                .ToList();

            foreach (var backup in backups.Skip(MaxBackupFiles))
            {
                try
                {
                    backup.Delete();
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static string BackupDirectoryFor(string savePath)
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(savePath));
            if (string.IsNullOrWhiteSpace(directory))
                directory = Directory.GetCurrentDirectory();
            return Path.Combine(directory, "Backups");
        }

        public static LeagueFile Load(string path)
        {
            var league = JsonSerializer.Deserialize<LeagueFile>(File.ReadAllText(path)) ?? new LeagueFile();
            var rng = new Random();
            if (league.SaveSchemaVersion <= 0)
                league.SaveSchemaVersion = 1;
            if (string.IsNullOrWhiteSpace(league.AssetLibraryPath))
                league.AssetLibraryPath = LeagueFile.DefaultAssetLibraryPath;
            league.Rules ??= new LeagueRules();
            if (!Enum.IsDefined(typeof(NationalAnthemCutsceneDefault), league.NationalAnthemCutsceneDefault))
                league.NationalAnthemCutsceneDefault = NationalAnthemCutsceneDefault.CurrentGameSettings;
            league.Rules.Innings = Math.Clamp(league.Rules.Innings, 5, 9);
            league.Rules.Schedule ??= new SeasonScheduleRules();
            league.Rules.Schedule.SeriesLength = Math.Clamp(league.Rules.Schedule.SeriesLength <= 0 ? 3 : league.Rules.Schedule.SeriesLength, 1, 6);
            league.Structure ??= LeagueStructure.CreateDefault();
            league.Structure.Conferences ??= new System.Collections.Generic.List<Conference>();
            NormalizeStructure(league.Structure);
            league.Teams ??= new System.Collections.Generic.List<Team>();
            league.UserControlledTeamIds ??= new System.Collections.Generic.List<System.Guid>();
            league.Seasons ??= new System.Collections.Generic.List<Season>();
            league.InProgressGames ??= new System.Collections.Generic.List<InProgressGameSave>();
            league.HallOfFameEntries ??= new System.Collections.Generic.List<HallOfFameEntry>();
            league.CustomFields ??= new System.Collections.Generic.List<CustomBaseballField>();
            EnsureBuiltInCustomFieldTemplates(league);
            league.Cutscenes ??= new System.Collections.Generic.List<CutsceneDefinition>();
            league.InboxMessages ??= new System.Collections.Generic.List<CoachInboxMessage>();
            foreach (var field in league.CustomFields)
                NormalizeCustomField(field);
            foreach (var cutscene in league.Cutscenes)
                NormalizeCutscene(cutscene);
            foreach (var message in league.InboxMessages)
                NormalizeInboxMessage(message);
            foreach (var entry in league.HallOfFameEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.EntryType))
                    entry.EntryType = "Player";
                if (entry.EntryType.Equals("Coach", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.CoachId == System.Guid.Empty)
                        entry.CoachId = entry.PlayerId;
                    if (string.IsNullOrWhiteSpace(entry.CoachName))
                        entry.CoachName = entry.PlayerName;
                }
            }
            foreach (var team in league.Teams)
            {
                team.NormalizeText();
                team.Roster ??= new System.Collections.Generic.List<Player>();
                team.JvPool ??= new System.Collections.Generic.List<Player>();
                team.InjuredReserve ??= new System.Collections.Generic.List<Player>();
                team.Cutscenes ??= new System.Collections.Generic.List<CutsceneDefinition>();
                foreach (var cutscene in team.Cutscenes)
                    NormalizeCutscene(cutscene);
                team.UniformSets ??= new System.Collections.Generic.List<TeamUniformSet>();
                foreach (var uniform in team.UniformSets)
                    uniform.Normalize(team);
                team.EnsureDefaultUniformSets();
                NormalizeActiveUniforms(team);
                team.BaseLineup ??= new TeamBaseLineup();
                team.BaseLineup.BattingOrder ??= new System.Collections.Generic.List<TeamBaseLineupSlot>();
                team.BaseLineup.DefensiveAssignments = team.BaseLineup.DefensiveAssignments == null
                    ? new System.Collections.Generic.Dictionary<string, System.Guid>(System.StringComparer.OrdinalIgnoreCase)
                    : new System.Collections.Generic.Dictionary<string, System.Guid>(team.BaseLineup.DefensiveAssignments, System.StringComparer.OrdinalIgnoreCase);
                team.PitchingPlan ??= new TeamPitchingPlan();
                NormalizePitchingPlan(team.PitchingPlan);
                foreach (var player in team.Roster)
                    NormalizePlayer(player, rng);
                foreach (var player in team.JvPool)
                    NormalizePlayer(player, rng);
                foreach (var player in team.InjuredReserve)
                    NormalizePlayer(player, rng);
            }
            foreach (var season in league.Seasons)
            {
                season.Schedule ??= new System.Collections.Generic.List<ScheduledGame>();
                for (int i = 0; i < season.Schedule.Count; i++)
                {
                    var scheduled = season.Schedule[i];
                    if (scheduled.GameNumber <= 0)
                        scheduled.GameNumber = i + 1;
                    if (scheduled.Week <= 0)
                        scheduled.Week = scheduled.GameNumber;
                    if (scheduled.WeekGameNumber <= 0)
                        scheduled.WeekGameNumber = 1;
                    if (scheduled.DayGameNumber <= 0)
                        scheduled.DayGameNumber = 1;
                    if (string.IsNullOrWhiteSpace(scheduled.DayLabel))
                        scheduled.DayLabel = "Game Day";
                }
                season.Games ??= new System.Collections.Generic.List<GameResult>();
                season.Playoffs ??= new System.Collections.Generic.List<PlayoffSeries>();
                foreach (var series in season.Playoffs.Where(series => series != null))
                {
                    series.RoundName ??= "";
                    series.BracketGroup ??= "";
                    series.Notes ??= "";
                    series.DistrictIds ??= new System.Collections.Generic.List<System.Guid>();
                    series.FeederSeriesIds ??= new System.Collections.Generic.List<System.Guid>();
                    series.DistrictIds = series.DistrictIds.Where(id => id != System.Guid.Empty).Distinct().ToList();
                    series.FeederSeriesIds = series.FeederSeriesIds.Where(id => id != System.Guid.Empty).Distinct().ToList();
                }
                season.RankingPolls ??= new System.Collections.Generic.List<SeasonRankingPoll>();
                foreach (var poll in season.RankingPolls)
                {
                    poll.Rankings ??= new System.Collections.Generic.List<SeasonRankingEntry>();
                    if (string.IsNullOrWhiteSpace(poll.Name))
                        poll.Name = RankingEngine.PollName(poll.Type, poll.Week);
                }
                season.AllStarSelections ??= new System.Collections.Generic.List<SeasonAllStarSelection>();
                season.Awards ??= new System.Collections.Generic.List<SeasonAwardSelection>();
                season.PitcherUsage ??= new System.Collections.Generic.Dictionary<System.Guid, PitcherUsageState>();
                if (season.AllStarGame != null)
                {
                    season.AllStarGame.Lines ??= new System.Collections.Generic.List<PlayerGameLine>();
                    season.AllStarGame.AwayBaseLineup ??= new TeamBaseLineup();
                    season.AllStarGame.HomeBaseLineup ??= new TeamBaseLineup();
                    NormalizeBaseLineup(season.AllStarGame.AwayBaseLineup);
                    NormalizeBaseLineup(season.AllStarGame.HomeBaseLineup);
                }
                foreach (var game in season.Games)
                    NormalizeGameResult(game);
            }
            foreach (var save in league.InProgressGames)
                NormalizeInProgressGameSave(save);
            PlayoffEngine.EnsureDefaultStructure(league);
            return league;
        }

        public static bool TryLoad(string path, [NotNullWhen(true)] out LeagueFile? league, out string errorMessage)
        {
            try
            {
                league = Load(path);
                errorMessage = "";
                return true;
            }
            catch (Exception ex)
            {
                league = null;
                errorMessage = ex.Message;
                return false;
            }
        }

        public static System.Collections.Generic.IReadOnlyList<LeagueBackupInfo> GetBackups(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
                return Array.Empty<LeagueBackupInfo>();

            string fullPath = Path.GetFullPath(savePath);
            string backupDirectory = BackupDirectoryFor(fullPath);
            if (!Directory.Exists(backupDirectory))
                return Array.Empty<LeagueBackupInfo>();

            string prefix = Path.GetFileNameWithoutExtension(fullPath) + ".";
            string suffix = Path.GetExtension(fullPath) + ".bak";
            var backups = new System.Collections.Generic.List<LeagueBackupInfo>();
            foreach (string backupPath in Directory.EnumerateFiles(backupDirectory, prefix + "*" + suffix))
            {
                var file = new FileInfo(backupPath);
                bool valid = TryLoad(backupPath, out _, out string error);
                backups.Add(new LeagueBackupInfo
                {
                    Path = backupPath,
                    SavedAt = file.LastWriteTime,
                    SizeBytes = file.Length,
                    IsValid = valid,
                    ErrorMessage = valid ? "" : error
                });
            }

            return backups
                .OrderByDescending(backup => backup.SavedAt)
                .ThenByDescending(backup => backup.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void EnsureBuiltInCustomFieldTemplates(LeagueFile league)
        {
            if (league?.CustomFields == null)
                return;

            if (!league.CustomFields.Any(field => string.Equals(field.Id, "custom-buda-johnson-field", StringComparison.OrdinalIgnoreCase)))
            {
                league.CustomFields.Add(new CustomBaseballField
                {
                    Id = "custom-buda-johnson-field",
                    Name = "Buda Johnson Field",
                    TeamLabel = "Johnson Jaguars",
                    OpenedYear = 2026,
                    GrassArgb = unchecked((int)0xFF4F9D3D),
                    DarkGrassArgb = unchecked((int)0xFF245F2B),
                    InfieldArgb = unchecked((int)0xFF352018),
                    ClayArgb = unchecked((int)0xFF1F1713),
                    WallArgb = unchecked((int)0xFF111111),
                    SeatArgb = unchecked((int)0xFFD7D7D7),
                    StructureArgb = unchecked((int)0xFFC9C4B9),
                    AccentArgb = unchecked((int)0xFFC6A348),
                    BackgroundAssetPath = "Assets\\Stadiums\\Custom\\buda-johnson-field.jpg"
                });
            }

            if (!league.CustomFields.Any(field => string.Equals(field.Id, "custom-aledo-field", StringComparison.OrdinalIgnoreCase)))
            {
                league.CustomFields.Add(new CustomBaseballField
                {
                    Id = "custom-aledo-field",
                    Name = "Aledo Field",
                    TeamLabel = "Aledo Bearcats",
                    OpenedYear = 2026,
                    GrassArgb = unchecked((int)0xFF58A53D),
                    DarkGrassArgb = unchecked((int)0xFF2A6A2D),
                    InfieldArgb = unchecked((int)0xFF332018),
                    ClayArgb = unchecked((int)0xFF211610),
                    WallArgb = unchecked((int)0xFF143D2B),
                    SeatArgb = unchecked((int)0xFFC6B8A6),
                    StructureArgb = unchecked((int)0xFFB9B0A2),
                    AccentArgb = unchecked((int)0xFFF05A24),
                    BackgroundAssetPath = "Assets\\Stadiums\\Custom\\aledo-field.jpg"
                });
            }
        }

        private static void NormalizeInProgressGameSave(InProgressGameSave save)
        {
            if (save == null)
                return;
            if (save.Id == System.Guid.Empty)
                save.Id = System.Guid.NewGuid();
            if (save.SavedAt == default)
                save.SavedAt = System.DateTime.Now;
            save.Label ??= "";
            if (save.State != null)
            {
                save.State.Count ??= new CountState();
                save.State.Bases ??= new BaseState();
                save.State.PinchUseCounts ??= new System.Collections.Generic.Dictionary<System.Guid, int>();
                save.State.RemovedPlayerIds ??= new System.Collections.Generic.List<System.Guid>();
                save.State.AwayLineupPlayerIds ??= new System.Collections.Generic.List<System.Guid>();
                save.State.HomeLineupPlayerIds ??= new System.Collections.Generic.List<System.Guid>();
                save.State.LiveLines ??= new System.Collections.Generic.List<PlayerGameLine>();
                save.State.AwayRunsByInning ??= new System.Collections.Generic.List<int>();
                save.State.HomeRunsByInning ??= new System.Collections.Generic.List<int>();
                save.State.PlayByPlay ??= new System.Collections.Generic.List<GamePlayByPlayEntry>();
                save.State.CompletedHalfInnings ??= new System.Collections.Generic.List<HalfInningSnapshot>();
                save.State.LiveRules ??= new GameplayLiveRulesState();
                save.State.AwayLeftOnBase = System.Math.Max(0, save.State.AwayLeftOnBase);
                save.State.HomeLeftOnBase = System.Math.Max(0, save.State.HomeLeftOnBase);
                save.State.AwayRunsByInning = save.State.AwayRunsByInning.Select(r => System.Math.Max(0, r)).ToList();
                save.State.HomeRunsByInning = save.State.HomeRunsByInning.Select(r => System.Math.Max(0, r)).ToList();
                NormalizeGameplayControlAssignments(save);
                NormalizeGameplayLiveRules(save.State.LiveRules);
                foreach (var line in save.State.LiveLines)
                    NormalizePlayerGameLine(line);
                foreach (var entry in save.State.PlayByPlay.Where(p => p != null))
                {
                    entry.Bases ??= "";
                    entry.Description ??= "";
                }
            }
        }

        private static void NormalizeGameplayControlAssignments(InProgressGameSave save)
        {
            var state = save.State;
            System.Guid awayId = state.AwayTeam?.Id ?? save.AwayTeamId;
            System.Guid homeId = state.HomeTeam?.Id ?? save.HomeTeamId;

            if (!IsGameplayTeamId(state.UserControlledTeamId, awayId, homeId))
                state.UserControlledTeamId = awayId;
            if (!IsGameplayTeamId(state.KeyboardControlledTeamId, awayId, homeId))
                state.KeyboardControlledTeamId = awayId;
            if (!IsGameplayTeamId(state.ControllerControlledTeamId, awayId, homeId))
                state.ControllerControlledTeamId = homeId;
            if (state.ControllerControlledTeamId == state.KeyboardControlledTeamId)
                state.ControllerControlledTeamId = state.KeyboardControlledTeamId == awayId ? homeId : awayId;
        }

        private static bool IsGameplayTeamId(System.Guid teamId, System.Guid awayId, System.Guid homeId)
            => teamId != System.Guid.Empty && (teamId == awayId || teamId == homeId);

        private static void NormalizeGameplayLiveRules(GameplayLiveRulesState liveRules)
        {
            if (liveRules == null)
                return;

            liveRules.PitchersRemovedByRunRule ??= new System.Collections.Generic.List<System.Guid>();
            liveRules.ReliefPitcherFatigue ??= new System.Collections.Generic.Dictionary<System.Guid, GameplayReliefPitcherState>();
            liveRules.PitcherRunRules ??= new System.Collections.Generic.Dictionary<System.Guid, GameplayPitcherRunRuleState>();

            foreach (var item in liveRules.ReliefPitcherFatigue.ToList())
            {
                if (item.Value == null)
                    liveRules.ReliefPitcherFatigue[item.Key] = new GameplayReliefPitcherState();
            }

            foreach (var item in liveRules.PitcherRunRules.ToList())
            {
                if (item.Value == null)
                {
                    liveRules.PitcherRunRules[item.Key] = new GameplayPitcherRunRuleState();
                    continue;
                }

                item.Value.RunsAllowedByInning ??= new System.Collections.Generic.Dictionary<int, int>();
                item.Value.EarnedRunsAllowedByInning ??= new System.Collections.Generic.Dictionary<int, int>();
                item.Value.FinalizedInnings ??= new System.Collections.Generic.HashSet<int>();
            }
        }

        private static void NormalizeGameResult(GameResult game)
        {
            if (game == null)
                return;
            game.Lines ??= new System.Collections.Generic.List<PlayerGameLine>();
            game.AwayRunsByInning ??= new System.Collections.Generic.List<int>();
            game.HomeRunsByInning ??= new System.Collections.Generic.List<int>();
            game.PlayByPlay ??= new System.Collections.Generic.List<GamePlayByPlayEntry>();
            game.GameType ??= "";
            game.GameMode ??= "";
            game.StadiumId ??= "";
            game.StadiumName ??= "";
            game.PlayoffRoundName ??= "";
            game.WinningPitcherName ??= "";
            game.LosingPitcherName ??= "";
            game.SavePitcherName ??= "";
            if (game.RegulationInnings <= 0)
                game.RegulationInnings = 9;
            if (game.GameLengthInnings <= 0)
                game.GameLengthInnings = System.Math.Max(1, System.Math.Max(game.AwayRunsByInning.Count, game.HomeRunsByInning.Count));
            foreach (var line in game.Lines)
                NormalizePlayerGameLine(line);
            foreach (var play in game.PlayByPlay)
            {
                if (play != null)
                {
                    play.Bases ??= "";
                    play.Description ??= "";
                }
            }
        }

        private static void NormalizePlayerGameLine(PlayerGameLine line)
        {
            if (line == null)
                return;
            line.PlayerName ??= "";
        }

        private static void NormalizeCustomField(CustomBaseballField field)
        {
            if (field == null)
                return;
            if (string.IsNullOrWhiteSpace(field.Id))
                field.Id = "custom-" + System.Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(field.Name))
                field.Name = "Custom Field";
            if (string.IsNullOrWhiteSpace(field.TeamLabel))
                field.TeamLabel = "Custom Home Field";
            if (field.OpenedYear <= 0)
                field.OpenedYear = System.DateTime.Now.Year;
            field.Overlays ??= new System.Collections.Generic.List<FieldImageOverlay>();
            foreach (var overlay in field.Overlays)
                NormalizeOverlay(overlay);
        }

        private static void NormalizeCutscene(CutsceneDefinition cutscene)
        {
            if (cutscene == null)
                return;
            if (cutscene.Id == System.Guid.Empty)
                cutscene.Id = System.Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(cutscene.Name))
                cutscene.Name = cutscene.Trigger.ToString();
            if (!System.Enum.IsDefined(typeof(CutsceneTrigger), cutscene.Trigger))
                cutscene.Trigger = CutsceneTrigger.GameStart;
            if (!System.Enum.IsDefined(typeof(TeamCutsceneUniformFolder), cutscene.UniformFolder))
                cutscene.UniformFolder = TeamCutsceneUniformFolder.Any;
            cutscene.MediaPath ??= "";
            cutscene.DurationSeconds = System.Math.Clamp(cutscene.DurationSeconds <= 0 ? 5 : cutscene.DurationSeconds, 1, 120);
        }

        private static void NormalizeActiveUniforms(Team team)
        {
            if (team?.UniformSets == null)
                return;

            foreach (TeamUniformCategory category in System.Enum.GetValues(typeof(TeamUniformCategory)))
            {
                var uniforms = team.UniformSets.Where(u => u.Category == category).ToList();
                if (uniforms.Count == 0)
                    continue;

                var active = uniforms.FirstOrDefault(u => u.Active) ?? uniforms[0];
                foreach (var uniform in uniforms)
                    uniform.Active = uniform.Id == active.Id;
            }
        }

        private static void NormalizeInboxMessage(CoachInboxMessage message)
        {
            if (message == null)
                return;
            if (message.Id == System.Guid.Empty)
                message.Id = System.Guid.NewGuid();
            if (message.CreatedAt == default)
                message.CreatedAt = System.DateTime.Now;
            if (string.IsNullOrWhiteSpace(message.From))
                message.From = "League Office";
            if (string.IsNullOrWhiteSpace(message.Category))
                message.Category = "Game Report";
            if (string.IsNullOrWhiteSpace(message.Subject))
                message.Subject = "League message";
            message.Body ??= "";
            message.To ??= "";
            message.ReferenceKey ??= "";
        }

        private static void NormalizeBaseLineup(TeamBaseLineup lineup)
        {
            if (lineup == null)
                return;
            lineup.BattingOrder ??= new System.Collections.Generic.List<TeamBaseLineupSlot>();
            lineup.DefensiveAssignments = lineup.DefensiveAssignments == null
                ? new System.Collections.Generic.Dictionary<string, System.Guid>(System.StringComparer.OrdinalIgnoreCase)
                : new System.Collections.Generic.Dictionary<string, System.Guid>(lineup.DefensiveAssignments, System.StringComparer.OrdinalIgnoreCase);
        }

        private static void NormalizePitchingPlan(TeamPitchingPlan plan)
        {
            if (plan == null)
                return;
            plan.RotationSize = System.Math.Clamp(plan.RotationSize, 3, 5);
            plan.NextStarterSlot = System.Math.Max(0, plan.NextStarterSlot);
            plan.AllStarPitchingScheduleIds ??= new System.Collections.Generic.List<System.Guid>();
            plan.StarterRotationIds ??= new System.Collections.Generic.List<System.Guid>();
            plan.BullpenRoles ??= new System.Collections.Generic.List<BullpenRoleAssignment>();
            foreach (var role in plan.BullpenRoles)
            {
                if (role == null)
                    continue;
                if (string.IsNullOrWhiteSpace(role.PlayerName))
                    role.PlayerName = "Pitcher";
                if (!System.Enum.IsDefined(typeof(BullpenRole), role.Role))
                    role.Role = BullpenRole.MiddleRelief;
            }
        }

        private static void NormalizeOverlay(FieldImageOverlay overlay)
        {
            if (overlay == null)
                return;
            if (string.IsNullOrWhiteSpace(overlay.Name))
                overlay.Name = "Image";
            overlay.X = Math.Clamp(overlay.X, 0f, 1f);
            overlay.Y = Math.Clamp(overlay.Y, 0f, 1f);
            overlay.Width = Math.Clamp(overlay.Width, 0.02f, 1f);
            overlay.Height = Math.Clamp(overlay.Height, 0.02f, 1f);
            overlay.Opacity = Math.Clamp(overlay.Opacity, 0, 255);
        }

        private static void NormalizePlayer(Player player, Random rng)
        {
            if (player == null)
                return;
            player.AllStarSeasonIds ??= new System.Collections.Generic.List<System.Guid>();
            player.AvatarPath ??= "";
            player.SpriteSheetPath ??= "";
            if (player.Classification == PlayerClassification.Unassigned)
                player.Classification = Simulator.RandomClassification(rng);
            if (player.InitialClassification == PlayerClassification.Unassigned)
                player.InitialClassification = player.Classification;
            PitchProfileEngine.NormalizePlayerPitchProfiles(player, rng);
            if (string.IsNullOrWhiteSpace(player.Positions))
                player.Positions = Simulator.RandomPositions(rng, player.Role);
            if (player.Potential <= 0) player.Potential = Simulator.RandomDevelopmentRating(rng, 40, 95);
            if (player.WorkEthic <= 0) player.WorkEthic = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.Durability <= 0) player.Durability = Simulator.RandomDevelopmentRating(rng, 35, 95);
            if (player.RegressionRisk <= 0) player.RegressionRisk = Simulator.RandomDevelopmentRating(rng, 5, 55);
            if (player.Fielding <= 0) player.Fielding = Simulator.RandomDevelopmentRating(rng, 35, 95);
            if (player.StealAggression <= 0) player.StealAggression = Simulator.RandomDevelopmentRating(rng, 20, 90);
            if (player.BaseRunning <= 0) player.BaseRunning = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.HoldRunner <= 0) player.HoldRunner = Simulator.RandomDevelopmentRating(rng, player.Role == PlayerRole.Pitcher ? 30 : 10, player.Role == PlayerRole.Pitcher ? 95 : 55);
            if (player.Pickoff <= 0) player.Pickoff = Simulator.RandomDevelopmentRating(rng, player.Role == PlayerRole.Pitcher ? 25 : 10, player.Role == PlayerRole.Pitcher ? 90 : 45);
            if (player.DeliveryTime <= 0) player.DeliveryTime = Simulator.RandomDevelopmentRating(rng, player.Role == PlayerRole.Pitcher ? 30 : 10, player.Role == PlayerRole.Pitcher ? 95 : 50);
            if (player.ArmStrength <= 0) player.ArmStrength = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.PopTime <= 0) player.PopTime = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.Accuracy <= 0) player.Accuracy = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.TagRating <= 0) player.TagRating = Simulator.RandomDevelopmentRating(rng, 30, 95);
            player.FieldingErrorPenaltyDebt = Math.Max(0, player.FieldingErrorPenaltyDebt);
            player.ErrorFreeFieldingChanceStreak = Math.Clamp(player.ErrorFreeFieldingChanceStreak, 0, 9);
            player.InjuredReserveSeasonNumber = Math.Max(0, player.InjuredReserveSeasonNumber);
            player.VarsityCallUpSeasonNumber = Math.Max(0, player.VarsityCallUpSeasonNumber);
            player.VarsitySeasonsPlayed = Math.Max(0, player.VarsitySeasonsPlayed);
            player.LastVarsitySeasonNumber = Math.Max(0, player.LastVarsitySeasonNumber);
            if (player.MedicalTag)
                player.MedicalTagEligible = false;
            if (string.IsNullOrWhiteSpace(player.Bats))
                player.Bats = Simulator.RandomBatSide(rng);
            if (string.IsNullOrWhiteSpace(player.Throws))
                player.Throws = Simulator.RandomThrowSide(rng, player.Role);
            if (player.Role == PlayerRole.Pitcher && player.CareerPitchCount <= 0)
                player.CareerPitchCount = Simulator.RandomCareerPitchCount(rng);
            if (player.InjuryStatus == PlayerInjuryStatus.Healthy)
            {
                player.InjuryName = "";
                player.InjuryGamesRemaining = 0;
                player.InjurySeverity = 0;
            }
            player.UnqualifiedPositionGameStreaks = player.UnqualifiedPositionGameStreaks == null
                ? new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
                : new System.Collections.Generic.Dictionary<string, int>(player.UnqualifiedPositionGameStreaks, System.StringComparer.OrdinalIgnoreCase);
        }

        private static void NormalizeStructure(LeagueStructure structure)
        {
            foreach (var conference in structure.Conferences)
            {
                conference.Regions ??= new System.Collections.Generic.List<Region>();
                foreach (var region in conference.Regions)
                {
                    region.Districts ??= new System.Collections.Generic.List<District>();
                    foreach (var district in region.Districts)
                        district.TeamIds ??= new System.Collections.Generic.List<System.Guid>();
                }
            }
        }
    }
}
