#nullable enable annotations

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class LeagueFile
    {
        public const string DefaultAssetLibraryPath = "";

        public int SaveSchemaVersion { get; set; } = 1;
        public string Name { get; set; } = "New Baseball Universe";
        public string OwnerFullName { get; set; } = "";
        public string AssetLibraryPath { get; set; } = DefaultAssetLibraryPath;
        public LeagueRules Rules { get; set; } = new LeagueRules();
        public LeagueStructure Structure { get; set; } = LeagueStructure.CreateDefault();
        public List<Team> Teams { get; set; } = new List<Team>();
        public List<Guid> UserControlledTeamIds { get; set; } = new List<Guid>();
        public List<Season> Seasons { get; set; } = new List<Season>();
        public List<InProgressGameSave> InProgressGames { get; set; } = new List<InProgressGameSave>();
        public List<HallOfFameEntry> HallOfFameEntries { get; set; } = new List<HallOfFameEntry>();
        public List<CustomBaseballField> CustomFields { get; set; } = new List<CustomBaseballField>();
        public List<CutsceneDefinition> Cutscenes { get; set; } = new List<CutsceneDefinition>();
        public List<CoachInboxMessage> InboxMessages { get; set; } = new List<CoachInboxMessage>();
        public NationalAnthemCutsceneDefault NationalAnthemCutsceneDefault { get; set; } = NationalAnthemCutsceneDefault.CurrentGameSettings;
    }

    public sealed class InProgressGameSave
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime SavedAt { get; set; } = DateTime.Now;
        public Guid? SeasonId { get; set; }
        public Guid? ScheduledGameId { get; set; }
        public Guid AwayTeamId { get; set; }
        public Guid HomeTeamId { get; set; }
        public string Label { get; set; } = "";
        public GameplayState State { get; set; }
    }

    public sealed class CoachInboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public Guid? SeasonId { get; set; }
        public int SeasonNumber { get; set; }
        public Guid? GameResultId { get; set; }
        public Guid TeamId { get; set; }
        public Guid CoachId { get; set; }
        public string ReferenceKey { get; set; } = "";
        public string From { get; set; } = "League Office";
        public string To { get; set; } = "";
        public string Category { get; set; } = "Game Report";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsRead { get; set; }
        public bool Important { get; set; }
    }

    public enum CutsceneTrigger
    {
        GameStart,
        PlayoffGameStart,
        AllStarGameStart,
        NationalAnthem,
        HomeRun,
        GrandSlam,
        RunScored,
        Strikeout,
        PitcherChange,
        SeventhInningStretch,
        FinalOut,
        ChampionshipWon
    }

    public enum NationalAnthemCutsceneDefault
    {
        CurrentGameSettings,
        LeagueCutscene,
        TeamCutscene
    }

    public sealed class CutsceneDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Cutscene";
        public CutsceneTrigger Trigger { get; set; }
        public TeamCutsceneUniformFolder UniformFolder { get; set; } = TeamCutsceneUniformFolder.Any;
        public string MediaPath { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public int DurationSeconds { get; set; } = 5;

        public override string ToString()
            => (Enabled ? "" : "[Off] ") + Trigger + " - " + Name;
    }

    public enum TeamCutsceneUniformFolder
    {
        Any,
        Home,
        HomeAlternate,
        Visitor,
        VisitorAlternate
    }

    public sealed class CustomBaseballField
    {
        public string Id { get; set; } = "custom-" + Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Custom Field";
        public string TeamLabel { get; set; } = "Custom Home Field";
        public int OpenedYear { get; set; } = DateTime.Now.Year;
        public int GrassArgb { get; set; } = unchecked((int)0xFF2E8B4D);
        public int DarkGrassArgb { get; set; } = unchecked((int)0xFF1E5D3B);
        public int InfieldArgb { get; set; } = unchecked((int)0xFFBF7B45);
        public int ClayArgb { get; set; } = unchecked((int)0xFF8C5736);
        public int WallArgb { get; set; } = unchecked((int)0xFF355E45);
        public int SeatArgb { get; set; } = unchecked((int)0xFFC13F32);
        public int StructureArgb { get; set; } = unchecked((int)0xFF86613C);
        public int AccentArgb { get; set; } = unchecked((int)0xFF244E83);
        public string BackgroundAssetPath { get; set; } = "";
        public List<FieldImageOverlay> Overlays { get; set; } = new List<FieldImageOverlay>();

        public override string ToString() => Name;
    }

    public sealed class FieldImageOverlay
    {
        public string Name { get; set; } = "Image";
        public string AssetPath { get; set; } = "";
        public float X { get; set; } = 0.5f;
        public float Y { get; set; } = 0.5f;
        public float Width { get; set; } = 0.18f;
        public float Height { get; set; } = 0.12f;
        public int Opacity { get; set; } = 255;
    }

    public sealed class TeamScoreboardTemplate
    {
        public bool Enabled { get; set; }
        public string TemplateName { get; set; } = "East View";
        public string BackgroundAssetPath { get; set; } = "Assets\\Scoreboards\\east-view-scoreboard-template.jpg";
        public string SchoolNameText { get; set; } = "";
        public string PreferredAbbreviation { get; set; } = "";
        public string MascotText { get; set; } = "";
        public ScoreboardBoardColorLayout BoardColorLayout { get; set; } = ScoreboardBoardColorLayout.Solid;
        public int BoardArgb { get; set; } = unchecked((int)0xFF102A1D);
        public int BoardSecondArgb { get; set; } = unchecked((int)0xFF102A1D);
        public int BoardThirdArgb { get; set; } = unchecked((int)0xFF102A1D);
        public int BoardFourthArgb { get; set; } = unchecked((int)0xFF102A1D);
        public int AccentArgb { get; set; } = unchecked((int)0xFFE8E2C5);
        public int TextArgb { get; set; } = unchecked((int)0xFFFFFFFF);
        public int AdStripArgb { get; set; } = unchecked((int)0xFF161616);
        public List<string> Ads { get; set; } = new List<string> { "BOOSTER CLUB", "EAST VIEW BASEBALL", "VISIT THE CONCESSION STAND" };

        public void Normalize(Team team)
        {
            TemplateName ??= "East View";
            BackgroundAssetPath ??= "Assets\\Scoreboards\\east-view-scoreboard-template.jpg";
            if (string.IsNullOrWhiteSpace(SchoolNameText))
                SchoolNameText = team?.City ?? "";
            if (string.IsNullOrWhiteSpace(PreferredAbbreviation))
                PreferredAbbreviation = team?.ScoreboardName ?? "";
            if (string.IsNullOrWhiteSpace(MascotText))
                MascotText = team?.Nickname ?? "";
            Ads ??= new List<string>();
            Ads = Ads.Where(ad => !string.IsNullOrWhiteSpace(ad)).Select(ad => ad.Trim()).Take(8).ToList();
            if (Ads.Count == 0)
                Ads.Add("BOOSTER CLUB");
        }
    }

    public enum ScoreboardBoardColorLayout
    {
        Solid,
        VerticalHalves,
        HorizontalHalves,
        Quarters
    }

    public sealed class HallOfFameEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EntryType { get; set; } = "Player";
        public Guid PlayerId { get; set; }
        public Guid CoachId { get; set; }
        public Guid TeamId { get; set; }
        public string PlayerName { get; set; } = "";
        public string CoachName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public PlayerRole Role { get; set; }
        public PlayerClassification Classification { get; set; }
        public int InductedSeasonNumber { get; set; }
        public DateTime InductedAt { get; set; } = DateTime.Now;
        public string Reason { get; set; } = "";
        public int HallScore { get; set; }
        public int Games { get; set; }
        public int PlateAppearances { get; set; }
        public int Hits { get; set; }
        public int ExtraBaseHits { get; set; }
        public int HomeRuns { get; set; }
        public int RBI { get; set; }
        public int StolenBases { get; set; }
        public int PitchingWins { get; set; }
        public int Saves { get; set; }
        public int Holds { get; set; }
        public int BlownSaves { get; set; }
        public int CompleteGames { get; set; }
        public int Shutouts { get; set; }
        public int RunsAllowed { get; set; }
        public int DoublesAllowed { get; set; }
        public int TriplesAllowed { get; set; }
        public int ReachedOnError { get; set; }
        public int DefensiveOuts { get; set; }
        public int TotalChances { get; set; }
        public int CatcherCaughtStealing { get; set; }
        public int CatcherStealAttempts { get; set; }
        public double CatcherCaughtStealingPercentage { get; set; }
        public int Strikeouts { get; set; }
        public double Average { get; set; }
        public double OPS { get; set; }
        public double ERA { get; set; }
        public double WHIP { get; set; }
        public int Championships { get; set; }
    }

    public sealed class LeagueRules
    {
        public int Innings { get; set; } = 9;
        public int LineupSize { get; set; } = 9;
        public bool ExtraInnings { get; set; } = true;
        public bool MercyRuleEnabled { get; set; } = true;
        public int MercyRuleRuns { get; set; } = 10;
        public int MercyRuleMinimumInning { get; set; } = 5;
        public bool ExtraInningRunnerOnSecond { get; set; } = true;
        public bool CourtesyRunnerForPitchersCatchers { get; set; } = true;
        public bool RotateSavedUniforms { get; set; } = true;
        public SeasonScheduleRules Schedule { get; set; } = new SeasonScheduleRules();
    }

    public sealed class SeasonScheduleRules
    {
        public int SeriesLength { get; set; } = 3;
        public int DistrictHomeGames { get; set; }
        public int DistrictAwayGames { get; set; }
        public int RegionHomeGames { get; set; }
        public int RegionAwayGames { get; set; }
        public int ConferenceHomeGames { get; set; }
        public int ConferenceAwayGames { get; set; }
        public int NonConferenceHomeGames { get; set; }
        public int NonConferenceAwayGames { get; set; }

        public bool HasAnyGames =>
            DistrictHomeGames + DistrictAwayGames +
            RegionHomeGames + RegionAwayGames +
            ConferenceHomeGames + ConferenceAwayGames +
            NonConferenceHomeGames + NonConferenceAwayGames > 0;
    }

    public sealed class LeagueStructure
    {
        public List<Conference> Conferences { get; set; } = new List<Conference>();

        public static LeagueStructure CreateDefault()
        {
            var structure = new LeagueStructure();
            for (int c = 1; c <= 2; c++)
            {
                var conf = new Conference { Name = "Conference " + c };
                for (int r = 1; r <= 2; r++)
                {
                    var region = new Region { Name = "Region " + r };
                    for (int d = 1; d <= 2; d++)
                        region.Districts.Add(new District { Name = "District " + d });
                    conf.Regions.Add(region);
                }
                structure.Conferences.Add(conf);
            }
            return structure;
        }
    }

    public sealed class Conference
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Conference";
        public List<Region> Regions { get; set; } = new List<Region>();
    }

    public sealed class Region
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Region";
        public List<District> Districts { get; set; } = new List<District>();
    }

    public sealed class District
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "District";
        public List<Guid> TeamIds { get; set; } = new List<Guid>();
    }

    public sealed class Team
    {
        public const int MaxScoreboardAbbreviationLength = 6;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CoachId { get; set; } = Guid.NewGuid();
        public string CoachName { get; set; } = "Head Coach";
        public string City { get; set; } = "";
        public string Nickname { get; set; } = "New Team";
        public string CatalogSchoolName { get; set; } = "";
        public string CatalogMascot { get; set; } = "";
        public string ScoreboardAbbreviation { get; set; } = "";
        public int PrimaryArgb { get; set; } = unchecked((int)0xFF1F6FEB);
        public int SecondaryArgb { get; set; } = unchecked((int)0xFFFFC857);
        public string SpriteSheetPath { get; set; } = "";
        public string HomeFieldPresetId { get; set; } = "";
        public string TeamMusicPath { get; set; } = "";
        public List<string> TeamMusicPlaylist { get; set; } = new List<string>();
        public List<Coach> Coaches { get; set; } = new List<Coach>();
        public List<Player> Roster { get; set; } = new List<Player>();
        public List<Player> JvPool { get; set; } = new List<Player>();
        public List<Player> InjuredReserve { get; set; } = new List<Player>();
        public List<CutsceneDefinition> Cutscenes { get; set; } = new List<CutsceneDefinition>();
        public List<TeamUniformSet> UniformSets { get; set; } = new List<TeamUniformSet>();
        public TeamScoreboardTemplate ScoreboardTemplate { get; set; } = new TeamScoreboardTemplate();
        public TeamBaseLineup BaseLineup { get; set; } = new TeamBaseLineup();
        public TeamPitchingPlan PitchingPlan { get; set; } = new TeamPitchingPlan();

        public string DisplayName => string.IsNullOrWhiteSpace(City) ? Nickname : (City + " " + Nickname).Trim();

        public string ScoreboardName
        {
            get
            {
                string value = string.IsNullOrWhiteSpace(ScoreboardAbbreviation)
                    ? (!string.IsNullOrWhiteSpace(City) ? City : Nickname)
                    : ScoreboardAbbreviation;
                return Limit(value.ToUpperInvariant(), MaxScoreboardAbbreviationLength);
            }
        }

        public void NormalizeText()
        {
            if (CoachId == Guid.Empty)
                CoachId = Guid.NewGuid();
            CoachName = string.IsNullOrWhiteSpace(CoachName) ? "Head Coach" : Limit(CoachName, 40);
            Coaches ??= new List<Coach>();
            if (Coaches.Count == 0)
                Coaches.Add(new Coach { Id = CoachId, Name = CoachName, Role = "Head Coach", Active = true });
            foreach (var coach in Coaches)
            {
                if (coach.Id == Guid.Empty)
                    coach.Id = Guid.NewGuid();
                coach.Name = string.IsNullOrWhiteSpace(coach.Name) ? "Coach" : Limit(coach.Name, 40);
                coach.Role = string.IsNullOrWhiteSpace(coach.Role) ? "Assistant Coach" : Limit(coach.Role, 32);
                if (!Enum.IsDefined(typeof(CoachStyle), coach.Style))
                    coach.Style = CoachStyle.Average;
                if (!Enum.IsDefined(typeof(CoachStrategy), coach.Strategy))
                    coach.Strategy = CoachStrategy.Conservative;
            }
            var head = Coaches.FirstOrDefault(c => c.Id == CoachId)
                ?? Coaches.FirstOrDefault(c => string.Equals(c.Role, "Head Coach", StringComparison.OrdinalIgnoreCase))
                ?? Coaches.FirstOrDefault();
            if (head != null)
            {
                CoachId = head.Id;
                CoachName = head.Name;
                head.Role = "Head Coach";
                head.Active = true;
            }
            City = (City ?? "").Trim();
            Nickname = string.IsNullOrWhiteSpace(Nickname) ? "Team" : Nickname.Trim();
            CatalogSchoolName ??= "";
            CatalogMascot ??= "";
            ScoreboardAbbreviation = Limit(ScoreboardAbbreviation, MaxScoreboardAbbreviationLength).ToUpperInvariant();
            TeamMusicPath ??= "";
            TeamMusicPlaylist ??= new List<string>();
            if (TeamMusicPlaylist.Count == 0 && !string.IsNullOrWhiteSpace(TeamMusicPath))
                TeamMusicPlaylist.Add(TeamMusicPath);
            UniformSets ??= new List<TeamUniformSet>();
            foreach (var uniform in UniformSets)
                uniform.Normalize(this);
            EnsureDefaultUniformSets();
            ScoreboardTemplate ??= new TeamScoreboardTemplate();
            ScoreboardTemplate.Normalize(this);
            JvPool ??= new List<Player>();
            InjuredReserve ??= new List<Player>();
            BaseLineup ??= new TeamBaseLineup();
            PitchingPlan ??= new TeamPitchingPlan();
        }

        public static string Limit(string? value, int maxLength)
        {
            value = (value ?? "").Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public void EnsureDefaultUniformSets()
        {
            UniformSets ??= new List<TeamUniformSet>();
            EnsureDefaultUniformSet(TeamUniformCategory.Home, "Home", PrimaryArgb, Color.White.ToArgb(), SecondaryArgb);
            EnsureDefaultUniformSet(TeamUniformCategory.HomeAlternate, "Home Alternate", SecondaryArgb, Color.White.ToArgb(), PrimaryArgb);
            EnsureDefaultUniformSet(TeamUniformCategory.Visitor, "Visitor", PrimaryArgb, Color.LightGray.ToArgb(), SecondaryArgb);
            EnsureDefaultUniformSet(TeamUniformCategory.VisitorAlternate, "Visitor Alternate", SecondaryArgb, Color.LightGray.ToArgb(), PrimaryArgb);
        }

        private void EnsureDefaultUniformSet(TeamUniformCategory category, string name, int jersey, int pants, int capHelmet)
        {
            if (UniformSets.Any(u => u.Category == category))
                return;

            UniformSets.Add(new TeamUniformSet
            {
                Category = category,
                Name = name,
                JerseyArgb = jersey,
                PantsArgb = pants,
                CapHelmetArgb = capHelmet,
                Active = true
            });
        }

        public TeamUniformSet ActiveUniform(TeamUniformCategory category)
        {
            UniformSets ??= new List<TeamUniformSet>();
            return UniformSets.FirstOrDefault(u => u.Category == category && u.Active)
                ?? UniformSets.FirstOrDefault(u => u.Category == category);
        }

        public TeamUniformSet UniformById(Guid? uniformId)
        {
            if (!uniformId.HasValue || uniformId.Value == Guid.Empty)
                return null;
            UniformSets ??= new List<TeamUniformSet>();
            return UniformSets.FirstOrDefault(u => u.Id == uniformId.Value);
        }

        public TeamUniformSet DefaultUniform()
            => ActiveUniform(TeamUniformCategory.Home)
                ?? ActiveUniform(TeamUniformCategory.Visitor)
                ?? UniformSets?.FirstOrDefault();
    }

    public enum TeamUniformCategory
    {
        Home,
        HomeAlternate,
        Visitor,
        VisitorAlternate
    }

    public sealed class TeamUniformSet
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TeamUniformCategory Category { get; set; } = TeamUniformCategory.Home;
        public string Name { get; set; } = "Uniform";
        public int JerseyArgb { get; set; } = unchecked((int)0xFF1F6FEB);
        public int PantsArgb { get; set; } = Color.White.ToArgb();
        public int CapHelmetArgb { get; set; } = unchecked((int)0xFFFFC857);
        public string ImagePath { get; set; } = "";
        public bool Active { get; set; }

        public void Normalize(Team team)
        {
            if (Id == Guid.Empty)
                Id = Guid.NewGuid();
            if (!Enum.IsDefined(typeof(TeamUniformCategory), Category))
                Category = TeamUniformCategory.Home;
            Name = string.IsNullOrWhiteSpace(Name) ? CategoryLabel(Category) : Team.Limit(Name, 60);
            ImagePath ??= "";
            if (JerseyArgb == 0)
                JerseyArgb = team?.PrimaryArgb ?? unchecked((int)0xFF1F6FEB);
            if (PantsArgb == 0)
                PantsArgb = Color.White.ToArgb();
            if (CapHelmetArgb == 0)
                CapHelmetArgb = team?.SecondaryArgb ?? unchecked((int)0xFFFFC857);
        }

        public static string CategoryLabel(TeamUniformCategory category)
        {
            return category switch
            {
                TeamUniformCategory.Home => "Home",
                TeamUniformCategory.HomeAlternate => "Home Alternate",
                TeamUniformCategory.Visitor => "Visitor",
                TeamUniformCategory.VisitorAlternate => "Visitor Alternate",
                _ => "Uniform"
            };
        }
    }

    public sealed class TeamBaseLineup
    {
        public DateTime LastCalculatedAt { get; set; } = DateTime.Now;
        public bool HasDesignatedHitter { get; set; }
        public Guid? StartingPitcherId { get; set; }
        public Guid? DesignatedHitterId { get; set; }
        public Dictionary<string, Guid> DefensiveAssignments { get; set; } = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        public List<TeamBaseLineupSlot> BattingOrder { get; set; } = new List<TeamBaseLineupSlot>();
        public string Status { get; set; } = "";
    }

    public sealed class TeamBaseLineupSlot
    {
        public int BattingOrder { get; set; }
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public string DefensivePosition { get; set; } = "";
        public bool DesignatedHitter { get; set; }
    }

    public sealed class TeamPitchingPlan
    {
        public DateTime LastCalculatedAt { get; set; } = DateTime.Now;
        public int RotationSize { get; set; } = 5;
        public int NextStarterSlot { get; set; }
        public bool UseAllStarPitchingRules { get; set; }
        public List<Guid> AllStarPitchingScheduleIds { get; set; } = new List<Guid>();
        public List<Guid> StarterRotationIds { get; set; } = new List<Guid>();
        public List<BullpenRoleAssignment> BullpenRoles { get; set; } = new List<BullpenRoleAssignment>();
        public string Status { get; set; } = "";
    }

    public sealed class BullpenRoleAssignment
    {
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public BullpenRole Role { get; set; } = BullpenRole.MiddleRelief;
    }

    public sealed class Coach
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Coach";
        public string Role { get; set; } = "Assistant Coach";
        public CoachStyle Style { get; set; } = CoachStyle.Average;
        public CoachStrategy Strategy { get; set; } = CoachStrategy.Conservative;
        public bool Active { get; set; } = true;
    }

    public sealed class Player
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Player";
        public PlayerRole Role { get; set; } = PlayerRole.Batter;
        public PlayerClassification Classification { get; set; } = PlayerClassification.Unassigned;
        public PlayerClassification InitialClassification { get; set; } = PlayerClassification.Unassigned;
        public string Positions { get; set; } = "";
        public string Bats { get; set; } = "";
        public string Throws { get; set; } = "";
        public int Potential { get; set; } = 50;
        public int WorkEthic { get; set; } = 50;
        public int Durability { get; set; } = 50;
        public int RegressionRisk { get; set; } = 25;
        public PlayerInjuryStatus InjuryStatus { get; set; } = PlayerInjuryStatus.Healthy;
        public string InjuryName { get; set; } = "";
        public int InjuryGamesRemaining { get; set; }
        public int InjurySeverity { get; set; }
        public int InjuryMissedGamesThisSeason { get; set; }
        public bool MedicalTag { get; set; }
        public bool MedicalTagEligible { get; set; }
        public int InjuredReserveSeasonNumber { get; set; }
        public Dictionary<string, int> UnqualifiedPositionGameStreaks { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public bool RedshirtActive { get; set; }
        public bool RedshirtUsed { get; set; }
        public int VarsityCallUpSeasonNumber { get; set; }
        public int VarsitySeasonsPlayed { get; set; }
        public int LastVarsitySeasonNumber { get; set; }
        public List<Guid> AllStarSeasonIds { get; set; } = new List<Guid>();
        public int? JerseyArgbOverride { get; set; }
        public int? PantsArgbOverride { get; set; }
        public int? CapHelmetArgbOverride { get; set; }
        public string AvatarPath { get; set; } = "";
        public string SpriteSheetPath { get; set; } = "";
        public List<PlayerPitchProfile> PitchArsenal { get; set; } = new List<PlayerPitchProfile>();
        public List<GameplayPitchType> PitchStrengths { get; set; } = new List<GameplayPitchType>();
        public List<GameplayPitchType> PitchWeaknesses { get; set; } = new List<GameplayPitchType>();
        public int Contact { get; set; } = 50;
        public int Power { get; set; } = 50;
        public int Speed { get; set; } = 50;
        public int StealAggression { get; set; } = 50;
        public int BaseRunning { get; set; } = 50;
        public int Fielding { get; set; } = 50;
        public int HoldRunner { get; set; } = 50;
        public int Pickoff { get; set; } = 50;
        public int DeliveryTime { get; set; } = 50;
        public int ArmStrength { get; set; } = 50;
        public int PopTime { get; set; } = 50;
        public int Accuracy { get; set; } = 50;
        public int TagRating { get; set; } = 50;
        public int FieldingErrorPenaltyDebt { get; set; }
        public int ErrorFreeFieldingChanceStreak { get; set; }
        public int Pitching { get; set; } = 50;
        public int Stamina { get; set; } = 50;
        public int CareerPitchCount { get; set; }
        public int StarterReliefOutsSinceLastStart { get; set; }
        public int NextStartPitchCountPenaltyPercent { get; set; }
        public int ConsecutiveReliefGames { get; set; }

        public int Overall => Role == PlayerRole.Pitcher
            ? Clamp((Pitching * 2 + Stamina + Speed + Fielding) / 5)
            : Clamp((Contact + Power + Speed + Fielding) / 4);

        public Color JerseyColor(Team? team)
            => JerseyColor(team, TeamUniformCategory.Home);

        public Color JerseyColor(Team? team, TeamUniformCategory category)
            => Color.FromArgb(JerseyArgbOverride ?? team?.ActiveUniform(category)?.JerseyArgb ?? team?.PrimaryArgb ?? unchecked((int)0xFF1F6FEB));

        public Color JerseyColor(Team? team, TeamUniformSet? uniform)
            => Color.FromArgb(JerseyArgbOverride ?? uniform?.JerseyArgb ?? team?.PrimaryArgb ?? unchecked((int)0xFF1F6FEB));

        public Color PantsColor(Team? team)
            => PantsColor(team, TeamUniformCategory.Home);

        public Color PantsColor(Team? team, TeamUniformCategory category)
            => Color.FromArgb(PantsArgbOverride ?? team?.ActiveUniform(category)?.PantsArgb ?? Color.White.ToArgb());

        public Color PantsColor(Team? team, TeamUniformSet? uniform)
            => Color.FromArgb(PantsArgbOverride ?? uniform?.PantsArgb ?? Color.White.ToArgb());

        public Color CapHelmetColor(Team? team)
            => CapHelmetColor(team, TeamUniformCategory.Home);

        public Color CapHelmetColor(Team? team, TeamUniformCategory category)
            => Color.FromArgb(CapHelmetArgbOverride ?? team?.ActiveUniform(category)?.CapHelmetArgb ?? team?.SecondaryArgb ?? unchecked((int)0xFFFFC857));

        public Color CapHelmetColor(Team? team, TeamUniformSet? uniform)
            => Color.FromArgb(CapHelmetArgbOverride ?? uniform?.CapHelmetArgb ?? team?.SecondaryArgb ?? unchecked((int)0xFFFFC857));

        private static int Clamp(int n) => n < 0 ? 0 : n > 99 ? 99 : n;
    }

    public sealed class PlayerPitchProfile
    {
        public GameplayPitchType PitchType { get; set; } = GameplayPitchType.Fastball;
        public bool Enabled { get; set; }
        public int Effectiveness { get; set; } = 50;
    }

    public enum PlayerRole
    {
        Batter,
        Pitcher
    }

    public enum PlayerClassification
    {
        Unassigned,
        Freshman,
        Sophomore,
        Junior,
        Senior
    }

    public enum PlayerInjuryStatus
    {
        Healthy,
        DayToDay,
        Out
    }

    public enum BullpenRole
    {
        Closer,
        Setup,
        LongRelief,
        MiddleRelief
    }

    public enum CoachStyle
    {
        BelowAverage,
        Average,
        AboveAverage,
        Championship
    }

    public enum CoachStrategy
    {
        Safe,
        Conservative,
        Aggressive
    }

    public sealed class Season
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Year { get; set; } = DateTime.Now.Year;
        public string Name { get; set; } = "Season";
        public List<ScheduledGame> Schedule { get; set; } = new List<ScheduledGame>();
        public List<GameResult> Games { get; set; } = new List<GameResult>();
        public List<PlayoffSeries> Playoffs { get; set; } = new List<PlayoffSeries>();
        public List<SeasonRankingPoll> RankingPolls { get; set; } = new List<SeasonRankingPoll>();
        public List<SeasonAllStarSelection> AllStarSelections { get; set; } = new List<SeasonAllStarSelection>();
        public SeasonAllStarGame AllStarGame { get; set; }
        public List<SeasonAwardSelection> Awards { get; set; } = new List<SeasonAwardSelection>();
        public Dictionary<Guid, PitcherUsageState> PitcherUsage { get; set; } = new Dictionary<Guid, PitcherUsageState>();
        public Guid? ChampionTeamId { get; set; }
        public bool OffseasonProcessed { get; set; }
    }

    public enum RankingPollType
    {
        PreSeason,
        Weekly,
        Final
    }

    public sealed class SeasonRankingPoll
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public RankingPollType Type { get; set; }
        public int Week { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Name { get; set; } = "";
        public List<SeasonRankingEntry> Rankings { get; set; } = new List<SeasonRankingEntry>();
    }

    public sealed class SeasonRankingEntry
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public int Rank { get; set; }
        public int PreviousRank { get; set; }
        public double Score { get; set; }
        public double PollScore { get; set; }
        public double ComputerScore { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Ties { get; set; }
        public int RankedWins { get; set; }
        public double StrengthOfSchedule { get; set; }
        public int RunDifferential { get; set; }
        public string Notes { get; set; } = "";
    }

    public sealed class PitcherUsageState
    {
        public Guid PlayerId { get; set; }
        public Guid TeamId { get; set; }
        public int LastTeamGameNumber { get; set; }
        public int LastStartTeamGameNumber { get; set; }
        public int LastReliefTeamGameNumber { get; set; }
        public int ConsecutiveReliefGames { get; set; }
        public int OutsSinceLastStart { get; set; }
        public int NextStartPitchCountPenaltyPercent { get; set; }
        public string Notes { get; set; } = "";
    }

    public sealed class SeasonAwardSelection
    {
        public Guid PlayerId { get; set; }
        public Guid TeamId { get; set; }
        public string PlayerName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public string AwardName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Position { get; set; } = "";
        public int Rank { get; set; }
        public bool Winner { get; set; }
        public double Score { get; set; }
        public string KeyStats { get; set; } = "";
        public DateTime FinalizedAt { get; set; } = DateTime.Now;
    }

    public sealed class SeasonAllStarSelection
    {
        public Guid PlayerId { get; set; }
        public Guid TeamId { get; set; }
        public string PlayerName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public PlayerRole Role { get; set; }
        public string Positions { get; set; } = "";
        public string AllStarTeam { get; set; } = "";
        public bool Starter { get; set; }
        public int SelectionScore { get; set; }
    }

    public sealed class SeasonAllStarGame
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime PlayedAt { get; set; } = DateTime.Now;
        public string AwayName { get; set; } = "Blue All-Stars";
        public string HomeName { get; set; } = "Red All-Stars";
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public TeamBaseLineup AwayBaseLineup { get; set; } = new TeamBaseLineup();
        public TeamBaseLineup HomeBaseLineup { get; set; } = new TeamBaseLineup();
        public List<PlayerGameLine> Lines { get; set; } = new List<PlayerGameLine>();
    }

    public enum ScheduledGameType
    {
        District,
        Region,
        Conference,
        NonConference
    }

    public sealed class ScheduledGame
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Week { get; set; }
        public string DayLabel { get; set; } = "";
        public int DayGameNumber { get; set; } = 1;
        public int GameNumber { get; set; }
        public int WeekGameNumber { get; set; }
        public ScheduledGameType Type { get; set; }
        public Guid AwayTeamId { get; set; }
        public Guid HomeTeamId { get; set; }
        public Guid? AwayUniformSetId { get; set; }
        public Guid? HomeUniformSetId { get; set; }
        public TeamUniformCategory? AwayUniformAutoCategory { get; set; }
        public TeamUniformCategory? HomeUniformAutoCategory { get; set; }
        public Guid? PlayedGameId { get; set; }
    }

    public sealed class PlayoffSeries
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Round { get; set; }
        public string RoundName { get; set; } = "";
        public int BestOf { get; set; }
        public Guid? ConferenceId { get; set; }
        public Guid? RegionId { get; set; }
        public List<Guid> DistrictIds { get; set; } = new List<Guid>();
        public List<Guid> FeederSeriesIds { get; set; } = new List<Guid>();
        public string BracketGroup { get; set; } = "";
        public Guid TeamAId { get; set; }
        public Guid TeamBId { get; set; }
        public Guid? HomeAdvantageTeamId { get; set; }
        public Guid TeamACoachId { get; set; }
        public Guid TeamBCoachId { get; set; }
        public int TeamAWins { get; set; }
        public int TeamBWins { get; set; }
        public Guid? WinnerTeamId { get; set; }
        public Guid? WinnerCoachId { get; set; }
        public string Notes { get; set; } = "";
    }

    public sealed class GameResult
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime PlayedAt { get; set; } = DateTime.Now;
        public Guid? ScheduledGameId { get; set; }
        public Guid AwayTeamId { get; set; }
        public Guid HomeTeamId { get; set; }
        public Guid AwayCoachId { get; set; }
        public Guid HomeCoachId { get; set; }
        public bool IsPlayoff { get; set; }
        public Guid? PlayoffSeriesId { get; set; }
        public int PlayoffRound { get; set; }
        public string PlayoffRoundName { get; set; } = "";
        public string GameType { get; set; } = "";
        public string GameMode { get; set; } = "";
        public string StadiumId { get; set; } = "";
        public string StadiumName { get; set; } = "";
        public Guid? AwayUniformSetId { get; set; }
        public Guid? HomeUniformSetId { get; set; }
        public string AwayUniformName { get; set; } = "";
        public string HomeUniformName { get; set; } = "";
        public int RegulationInnings { get; set; } = 9;
        public bool ExtraInningsEnabled { get; set; } = true;
        public bool ExtraInningRunnerOnSecond { get; set; } = true;
        public bool MercyRuleEnabled { get; set; }
        public int MercyRuleRuns { get; set; }
        public int MercyRuleMinimumInning { get; set; }
        public bool EndedByMercyRule { get; set; }
        public int GameLengthInnings { get; set; }
        public int GameLengthOuts { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public int AwayHits { get; set; }
        public int HomeHits { get; set; }
        public int AwayErrors { get; set; }
        public int HomeErrors { get; set; }
        public int AwayLeftOnBase { get; set; }
        public int HomeLeftOnBase { get; set; }
        public List<int> AwayRunsByInning { get; set; } = new List<int>();
        public List<int> HomeRunsByInning { get; set; } = new List<int>();
        public Guid? WinningPitcherId { get; set; }
        public string WinningPitcherName { get; set; } = "";
        public Guid? LosingPitcherId { get; set; }
        public string LosingPitcherName { get; set; } = "";
        public Guid? SavePitcherId { get; set; }
        public string SavePitcherName { get; set; } = "";
        public List<GamePlayByPlayEntry> PlayByPlay { get; set; } = new List<GamePlayByPlayEntry>();
        public List<PlayerGameLine> Lines { get; set; } = new List<PlayerGameLine>();
    }

    public sealed class GamePlayByPlayEntry
    {
        public int Sequence { get; set; }
        public int Inning { get; set; }
        public HalfInning Half { get; set; } = HalfInning.Top;
        public int Outs { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public string Bases { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class PlayerGameLine
    {
        public Guid TeamId { get; set; }
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public bool Pitcher { get; set; }
        public bool StartingPitcher { get; set; }
        public PlayerClassification Classification { get; set; } = PlayerClassification.Unassigned;
        public PlayerClassification InitialClassification { get; set; } = PlayerClassification.Unassigned;
        public int R { get; set; }
        public int AB { get; set; }
        public int H { get; set; }
        public int Doubles { get; set; }
        public int Triples { get; set; }
        public int HR { get; set; }
        public int RBI { get; set; }
        public int BB { get; set; }
        public int IBB { get; set; }
        public int SO { get; set; }
        public int SB { get; set; }
        public int CS { get; set; }
        public int HBP { get; set; }
        public int SH { get; set; }
        public int SF { get; set; }
        public int FlyOuts { get; set; }
        public int GroundOuts { get; set; }
        public int PopOuts { get; set; }
        public int GroundedIntoDoublePlays { get; set; }
        public int ReachedOnError { get; set; }
        public int IPOuts { get; set; }
        public int ER { get; set; }
        public int RunsAllowed { get; set; }
        public int K { get; set; }
        public int HitsAllowed { get; set; }
        public int DoublesAllowed { get; set; }
        public int TriplesAllowed { get; set; }
        public int WalksAllowed { get; set; }
        public int IntentionalWalksAllowed { get; set; }
        public int HomeRunsAllowed { get; set; }
        public int HitBatters { get; set; }
        public int WildPitches { get; set; }
        public int Balks { get; set; }
        public int BattersFaced { get; set; }
        public int PitchCount { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Saves { get; set; }
        public int Holds { get; set; }
        public int BlownSaves { get; set; }
        public int CompleteGames { get; set; }
        public int Shutouts { get; set; }
        public int Putouts { get; set; }
        public int Assists { get; set; }
        public int Errors { get; set; }
        public int DefensiveOuts { get; set; }
        public int DefensiveDoublePlays { get; set; }
        public int TeamDoublePlaysTurned { get; set; }
        public int PassedBalls { get; set; }
        public int StolenBasesAllowed { get; set; }
        public int CatcherCaughtStealing { get; set; }
        public int GamesMissedInjury { get; set; }

        public int PlateAppearances => AB + BB + HBP + SH + SF;
        public int ExtraBaseHits => Doubles + Triples + HR;
        public int TotalChances => Putouts + Assists + Errors;
        public int CatcherStealAttempts => StolenBasesAllowed + CatcherCaughtStealing;
        public double CatcherCaughtStealingPercentage => CatcherStealAttempts <= 0
            ? 0.0
            : CatcherCaughtStealing / (double)CatcherStealAttempts;
    }
}
