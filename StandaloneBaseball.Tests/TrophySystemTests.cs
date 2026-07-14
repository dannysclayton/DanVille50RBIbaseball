using System.Drawing;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class TrophySystemTests
{
    [Fact]
    public void Catalog_ReturnsEmptyWhenNoDynastyIsLoaded()
    {
        Assert.Empty(TrophyCatalog.Build(null));
    }

    [Fact]
    public void Catalog_CreatesTrophiesForWinnersOnlyAndUsesDynastySeasonNumber()
    {
        Guid firstWinnerId = Guid.NewGuid();
        Guid secondWinnerId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        var league = new LeagueFile
        {
            Seasons = new List<Season>
            {
                new Season
                {
                    Name = "Opening Season",
                    Awards = new List<SeasonAwardSelection>
                    {
                        new SeasonAwardSelection
                        {
                            PlayerId = firstWinnerId,
                            TeamId = teamId,
                            PlayerName = "First Winner",
                            TeamName = "Danville Stars",
                            AwardName = "Babe Ruth Award",
                            Rank = 1,
                            Winner = true
                        },
                        new SeasonAwardSelection
                        {
                            PlayerId = Guid.NewGuid(),
                            TeamId = teamId,
                            PlayerName = "Finalist Only",
                            TeamName = "Danville Stars",
                            AwardName = "Babe Ruth Award",
                            Rank = 2,
                            Winner = false
                        }
                    }
                },
                new Season
                {
                    Name = "Second Season",
                    Awards = new List<SeasonAwardSelection>
                    {
                        new SeasonAwardSelection
                        {
                            PlayerId = secondWinnerId,
                            TeamId = teamId,
                            PlayerName = "Second Winner",
                            TeamName = "Danville Stars",
                            AwardName = "Nolan Ryan Award",
                            Rank = 1,
                            Winner = true
                        }
                    }
                }
            }
        };

        List<AwardTrophyRecord> trophies = TrophyCatalog.Build(league);

        Assert.Equal(2, trophies.Count);
        AwardTrophyRecord latest = Assert.Single(trophies.Where(trophy => trophy.RecipientId == secondWinnerId));
        Assert.Equal(2, latest.SeasonNumber);
        Assert.Equal("Nolan Ryan Award", latest.AwardName);
        AwardTrophyRecord first = Assert.Single(trophies.Where(trophy => trophy.RecipientId == firstWinnerId));
        Assert.Equal(1, first.SeasonNumber);
        Assert.DoesNotContain(trophies, trophy => trophy.RecipientName == "Finalist Only");
    }

    [Fact]
    public void Catalog_UsesCoachIdentifierForJohnnyOatesTrophyRecipient()
    {
        Guid coachId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        var league = new LeagueFile
        {
            Seasons = new List<Season>
            {
                new Season
                {
                    Awards = new List<SeasonAwardSelection>
                    {
                        new SeasonAwardSelection
                        {
                            PlayerId = coachId,
                            TeamId = teamId,
                            PlayerName = "Championship Coach",
                            TeamName = "Danville Stars",
                            AwardName = "Johnny Oates Award",
                            Winner = true
                        }
                    }
                }
            }
        };

        AwardTrophyRecord trophy = Assert.Single(TrophyCatalog.Build(league));

        Assert.Equal(coachId, trophy.RecipientId);
        Assert.Equal(teamId, trophy.TeamId);
        Assert.Equal("Championship Coach", trophy.RecipientName);
        Assert.Equal("Johnny Oates Award", trophy.AwardName);
    }

    [Fact]
    public void Renderer_AllowsOriginalOrAwardWinnerPlaqueContent()
    {
        Assert.True(File.Exists(TrophyRenderer.TemplatePath), TrophyRenderer.TemplatePath);
        using Image template = Image.FromFile(TrophyRenderer.TemplatePath);
        var trophy = new AwardTrophyRecord
        {
            SeasonNumber = 12,
            AwardName = "Ivan Rodriguez Award",
            RecipientName = "Alexandra Long Player Name",
            TeamName = "Danville Stars"
        };
        using Bitmap original = TrophyRenderer.Render(trophy, TrophyPlaqueStyle.OriginalPlaque);
        using Bitmap rendered = TrophyRenderer.Render(trophy, TrophyPlaqueStyle.AwardWinnerPlaque);

        Assert.Equal(template.Width, rendered.Width);
        Assert.Equal(template.Height, rendered.Height);
        using var sourceBitmap = new Bitmap(template);
        Color originalPlaque = sourceBitmap.GetPixel(sourceBitmap.Width / 2, (int)(sourceBitmap.Height * 0.70));
        Assert.Equal(originalPlaque.ToArgb(),
            original.GetPixel(original.Width / 2, (int)(original.Height * 0.70)).ToArgb());
        Color renderedPlaque = rendered.GetPixel(rendered.Width / 2, (int)(rendered.Height * 0.70));
        Assert.NotEqual(originalPlaque.ToArgb(), renderedPlaque.ToArgb());
    }
}
