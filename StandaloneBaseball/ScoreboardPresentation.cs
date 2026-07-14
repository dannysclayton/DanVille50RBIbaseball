using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal static class GameplayScoreboardPresentation
    {
        public const int GenericHudHeight = 88;

        public static bool UsesCustomScoreboard(GameplayRenderingGameState? state)
            => state?.HomeTeam?.ScoreboardTemplate?.Enabled == true;

        public static int HudHeight(Rectangle bounds, GameplayRenderingGameState? state)
            => UsesCustomScoreboard(state)
                ? Math.Clamp(bounds.Height / 4, 140, 190)
                : GenericHudHeight;

        public static string ScoreText(GameplayRenderingGameState? state)
            => (state?.AwayName ?? "AWAY") + " " + Math.Max(0, state?.AwayScore ?? 0) +
                "   -   " + (state?.HomeName ?? "HOME") + " " + Math.Max(0, state?.HomeScore ?? 0);

        public static string InningText(GameplayRenderingGameState? state)
            => (state?.TopHalf == false ? "BOTTOM " : "TOP ") + Math.Max(1, state?.Inning ?? 1);

        public static string CountText(GameplayRenderingGameState? state)
            => "B " + Math.Clamp(state?.Balls ?? 0, 0, 4) +
                "  S " + Math.Clamp(state?.Strikes ?? 0, 0, 3) +
                "  O " + Math.Clamp(state?.Outs ?? 0, 0, 3);
    }

    internal static class ReplayScoreboardPresentation
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public static void Apply(
            ReplayFile? replay,
            ReplayTeam? replayTeam,
            Team target,
            Team? currentLeagueTeam,
            bool homeTeam)
        {
            if (target == null)
                return;

            TeamScoreboardTemplate? template = Clone(replayTeam?.ScoreboardTemplate)
                ?? Clone(currentLeagueTeam?.ScoreboardTemplate)
                ?? LoadLegacyTemplate(replay, homeTeam ? replay?.Assets?.ScoreboardTemplate : "", target);
            target.ScoreboardTemplate = template ?? new TeamScoreboardTemplate();
            target.ScoreboardTemplate.Normalize(target);
        }

        public static string ResolveLogo(ReplayFile replay, ReplayTeam? replayTeam, string? currentLeagueLogoPath)
        {
            string replayLogo = ReplayStore.ResolveReplayPath(replay, replayTeam?.LogoPath ?? "");
            if (!string.IsNullOrWhiteSpace(replayLogo))
                return replayLogo;

            string current = AssetPathResolver.ResolveExistingFile(currentLeagueLogoPath);
            return current ?? "";
        }

        public static TeamScoreboardTemplate? Clone(TeamScoreboardTemplate? source)
        {
            if (source == null)
                return null;

            return new TeamScoreboardTemplate
            {
                Enabled = source.Enabled,
                TemplateName = source.TemplateName ?? "",
                BackgroundAssetPath = source.BackgroundAssetPath ?? "",
                SchoolNameText = source.SchoolNameText ?? "",
                PreferredAbbreviation = source.PreferredAbbreviation ?? "",
                MascotText = source.MascotText ?? "",
                BoardColorLayout = source.BoardColorLayout,
                BoardArgb = source.BoardArgb,
                BoardSecondArgb = source.BoardSecondArgb,
                BoardThirdArgb = source.BoardThirdArgb,
                BoardFourthArgb = source.BoardFourthArgb,
                AccentArgb = source.AccentArgb,
                TextArgb = source.TextArgb,
                AdStripArgb = source.AdStripArgb,
                Ads = source.Ads == null ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(source.Ads)
            };
        }

        private static TeamScoreboardTemplate? LoadLegacyTemplate(ReplayFile? replay, string? templatePath, Team target)
        {
            string resolved = ReplayStore.ResolveReplayPath(replay, templatePath);
            if (string.IsNullOrWhiteSpace(resolved))
                return null;

            if (Path.GetExtension(resolved).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return JsonSerializer.Deserialize<TeamScoreboardTemplate>(File.ReadAllText(resolved), JsonOptions);
                }
                catch
                {
                    return null;
                }
            }

            return new TeamScoreboardTemplate
            {
                Enabled = true,
                TemplateName = "Replay Scoreboard",
                BackgroundAssetPath = resolved,
                SchoolNameText = target.City,
                PreferredAbbreviation = target.ScoreboardName,
                MascotText = target.Nickname,
                BoardArgb = target.PrimaryArgb,
                BoardSecondArgb = target.SecondaryArgb,
                AccentArgb = target.SecondaryArgb
            };
        }
    }
}
