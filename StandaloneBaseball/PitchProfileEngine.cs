#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class PitchProfileEngine
    {
        public static readonly GameplayPitchType[] AllPitchTypes =
        {
            GameplayPitchType.Fastball,
            GameplayPitchType.Curveball,
            GameplayPitchType.Slider,
            GameplayPitchType.Changeup,
            GameplayPitchType.Splitter,
            GameplayPitchType.Forkball,
            GameplayPitchType.Knuckleball
        };

        public enum PitchScoutRating
        {
            Poor,
            Average,
            AboveAverage,
            Exceptional
        }

        public static void NormalizePlayerPitchProfiles(Player player, Random? rng = null)
        {
            if (player == null)
                return;

            player.PitchArsenal ??= new List<PlayerPitchProfile>();
            player.PitchStrengths ??= new List<GameplayPitchType>();
            player.PitchWeaknesses ??= new List<GameplayPitchType>();

            if (player.PitchArsenal.Count == 0)
                AssignDefaultPitchArsenal(player, rng ?? new Random());
            else
                NormalizeArsenalList(player);

            if (IsPitcherClassified(player))
                EnsurePitcherMinimumArsenal(player, rng ?? new Random());

            if (player.PitchStrengths.Count == 0 && player.PitchWeaknesses.Count == 0)
                AssignDefaultBatterPitchProfile(player, rng ?? new Random());
            else
                NormalizeBatterLists(player);
        }

        public static bool CanThrow(Player pitcher, GameplayPitchType pitchType)
        {
            EnsureProfilesReady(pitcher);
            return EnabledPitchProfiles(pitcher).Any(p => p.PitchType == pitchType);
        }

        public static GameplayPitchType BestPitch(Player pitcher)
        {
            EnsureProfilesReady(pitcher);
            return EnabledPitchProfiles(pitcher)
                .OrderByDescending(p => p.Effectiveness)
                .ThenBy(p => p.PitchType == GameplayPitchType.Fastball ? 0 : 1)
                .FirstOrDefault()?.PitchType ?? GameplayPitchType.Fastball;
        }

        public static int PitchEffectiveness(Player? pitcher, GameplayPitchType pitchType)
        {
            EnsureProfilesReady(pitcher);
            var profile = EnabledPitchProfiles(pitcher).FirstOrDefault(p => p.PitchType == pitchType);
            if (profile != null)
                return Math.Clamp(profile.Effectiveness, 0, 99);

            return pitcher?.Role == PlayerRole.Pitcher ? Math.Clamp(pitcher.Pitching - 20, 10, 65) : 20;
        }

        public static int BatterPitchAdjustment(Player? batter, GameplayPitchType pitchType)
        {
            EnsureProfilesReady(batter);
            if (batter == null)
                return 0;
            batter.PitchStrengths ??= new List<GameplayPitchType>();
            batter.PitchWeaknesses ??= new List<GameplayPitchType>();
            int adjustment = 0;
            if (batter.PitchStrengths.Contains(pitchType))
                adjustment += 10;
            if (batter.PitchWeaknesses.Contains(pitchType))
                adjustment -= 12;
            return adjustment;
        }

        public static string ArsenalSummary(Player player)
            => string.Join(", ", EnabledPitchProfiles(player)
                .OrderByDescending(p => p.Effectiveness)
                .Select(p => ShortName(p.PitchType) + ":" + Math.Clamp(p.Effectiveness, 0, 99)));

        public static string ArsenalScoutSummary(Player player)
            => string.Join(", ", EnabledPitchProfiles(player)
                .OrderByDescending(p => p.Effectiveness)
                .Select(p => ShortName(p.PitchType) + ":" + ScoutLabel(p.Effectiveness)));

        public static PitchScoutRating ScoutRating(int effectiveness)
        {
            effectiveness = Math.Clamp(effectiveness, 0, 99);
            if (effectiveness >= 90)
                return PitchScoutRating.Exceptional;
            if (effectiveness >= 70)
                return PitchScoutRating.AboveAverage;
            if (effectiveness >= 40)
                return PitchScoutRating.Average;
            return PitchScoutRating.Poor;
        }

        public static string ScoutLabel(int effectiveness)
            => ScoutRating(effectiveness) switch
            {
                PitchScoutRating.Exceptional => "Exceptional",
                PitchScoutRating.AboveAverage => "Above Avg",
                PitchScoutRating.Average => "Average",
                _ => "Poor"
            };

        public static void EnsurePitcherMinimumArsenal(Player player, Random rng)
        {
            if (player == null || !IsPitcherClassified(player))
                return;
            rng ??= new Random();
            NormalizeArsenalList(player);

            var fastball = player.PitchArsenal.First(p => p.PitchType == GameplayPitchType.Fastball);
            fastball.Enabled = true;
            if (fastball.Effectiveness <= 0)
                fastball.Effectiveness = DefaultPitchEffectiveness(player, rng);

            while (player.PitchArsenal.Count(p => p.Enabled) < 3)
            {
                var next = player.PitchArsenal
                    .Where(p => !p.Enabled && p.PitchType != GameplayPitchType.Fastball)
                    .OrderBy(_ => rng.Next())
                    .FirstOrDefault();
                if (next == null)
                    break;
                next.Enabled = true;
                next.Effectiveness = DefaultPitchEffectiveness(player, rng);
            }

            while (player.PitchArsenal.Count(p => p.Enabled) > 5)
            {
                var remove = player.PitchArsenal
                    .Where(p => p.Enabled && p.PitchType != GameplayPitchType.Fastball)
                    .OrderBy(p => p.Effectiveness)
                    .FirstOrDefault();
                if (remove == null)
                    break;
                remove.Enabled = false;
            }
        }

        public static void AssignEmergencyPitchArsenal(Player player, Random rng)
        {
            if (player == null)
                return;
            rng ??= new Random();

            var selected = AllPitchTypes.OrderBy(_ => rng.Next()).Take(3).ToHashSet();
            player.PitchArsenal = AllPitchTypes.Select(pitch =>
            {
                bool enabled = selected.Contains(pitch);
                return new PlayerPitchProfile
                {
                    PitchType = pitch,
                    Enabled = enabled,
                    Effectiveness = enabled ? EmergencyEffectiveness(rng) : Math.Clamp(rng.Next(10, 36), 0, 89)
                };
            }).ToList();
        }

        public static string PitchListText(IEnumerable<GameplayPitchType> pitches)
            => string.Join(", ", (pitches ?? Enumerable.Empty<GameplayPitchType>()).Distinct().Select(ShortName));

        public static List<GameplayPitchType> ParsePitchList(string? value)
        {
            var result = new List<GameplayPitchType>();
            foreach (string token in (value ?? "").Split(new[] { ',', '/', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParsePitch(token, out var pitch) && !result.Contains(pitch))
                    result.Add(pitch);
            }
            return result;
        }

        public static string ShortName(GameplayPitchType pitch)
            => pitch switch
            {
                GameplayPitchType.Fastball => "FB",
                GameplayPitchType.Curveball => "CB",
                GameplayPitchType.Slider => "SL",
                GameplayPitchType.Changeup => "CH",
                GameplayPitchType.Splitter => "SPL",
                GameplayPitchType.Forkball => "FORK",
                GameplayPitchType.Knuckleball => "KN",
                _ => pitch.ToString().ToUpperInvariant()
            };

        public static bool TryParsePitch(string value, out GameplayPitchType pitch)
        {
            string token = (value ?? "").Trim().Replace("-", "").Replace("_", "").ToUpperInvariant();
            switch (token)
            {
                case "FB":
                case "FAST":
                case "FASTBALL":
                    pitch = GameplayPitchType.Fastball;
                    return true;
                case "CB":
                case "CURVE":
                case "CURVEBALL":
                    pitch = GameplayPitchType.Curveball;
                    return true;
                case "SL":
                case "SLIDER":
                    pitch = GameplayPitchType.Slider;
                    return true;
                case "CH":
                case "CU":
                case "CHANGE":
                case "CHANGEUP":
                    pitch = GameplayPitchType.Changeup;
                    return true;
                case "SPL":
                case "SPLIT":
                case "SPLITTER":
                    pitch = GameplayPitchType.Splitter;
                    return true;
                case "FK":
                case "FORK":
                case "FORKBALL":
                    pitch = GameplayPitchType.Forkball;
                    return true;
                case "KN":
                case "KB":
                case "KNUCKLE":
                case "KNUCKLEBALL":
                    pitch = GameplayPitchType.Knuckleball;
                    return true;
                default:
                    return Enum.TryParse(value, true, out pitch) && AllPitchTypes.Contains(pitch);
            }
        }

        private static IEnumerable<PlayerPitchProfile> EnabledPitchProfiles(Player? pitcher)
        {
            if (pitcher == null)
                return Enumerable.Empty<PlayerPitchProfile>();
            pitcher.PitchArsenal ??= new List<PlayerPitchProfile>();
            return pitcher.PitchArsenal.Where(p => p != null && p.Enabled && AllPitchTypes.Contains(p.PitchType));
        }

        private static void EnsureProfilesReady(Player? player)
        {
            if (player == null)
                return;
            if (player.PitchArsenal == null || player.PitchArsenal.Count == 0 ||
                player.PitchStrengths == null || player.PitchWeaknesses == null)
            {
                NormalizePlayerPitchProfiles(player, new Random());
            }
            else if (IsPitcherClassified(player))
            {
                EnsurePitcherMinimumArsenal(player, new Random());
            }
        }

        private static void AssignDefaultPitchArsenal(Player player, Random rng)
        {
            bool pitcherClassified = IsPitcherClassified(player);
            int target = pitcherClassified ? rng.Next(3, 6) : 0;
            var enabled = new HashSet<GameplayPitchType>();
            if (pitcherClassified)
            {
                enabled.Add(GameplayPitchType.Fastball);
                while (enabled.Count < target)
                    enabled.Add(AllPitchTypes.Where(p => p != GameplayPitchType.Fastball).OrderBy(_ => rng.Next()).First());
            }

            player.PitchArsenal = AllPitchTypes.Select(pitch => new PlayerPitchProfile
            {
                PitchType = pitch,
                Enabled = enabled.Contains(pitch),
                Effectiveness = enabled.Contains(pitch)
                    ? DefaultPitchEffectiveness(player, rng)
                    : Math.Clamp(player.Pitching / 2 + rng.Next(-5, 10), 0, 55)
            }).ToList();
        }

        private static void AssignDefaultBatterPitchProfile(Player player, Random rng)
        {
            var counts = StrengthWeaknessCounts(player.Classification);
            var pool = AllPitchTypes.OrderBy(_ => rng.Next()).ToList();
            player.PitchStrengths = pool.Take(counts.strengths).ToList();
            player.PitchWeaknesses = pool.Skip(counts.strengths).Take(counts.weaknesses).ToList();
        }

        private static (int strengths, int weaknesses) StrengthWeaknessCounts(PlayerClassification classification)
            => classification switch
            {
                PlayerClassification.Freshman => (1, 3),
                PlayerClassification.Sophomore => (1, 2),
                PlayerClassification.Junior => (2, 1),
                PlayerClassification.Senior => (3, 1),
                _ => (1, 2)
            };

        private static int ClassPitchBonus(PlayerClassification classification)
            => classification switch
            {
                PlayerClassification.Freshman => -5,
                PlayerClassification.Sophomore => -2,
                PlayerClassification.Junior => 3,
                PlayerClassification.Senior => 6,
                _ => 0
            };

        private static int DefaultPitchEffectiveness(Player player, Random rng)
            => Math.Clamp((player?.Pitching ?? 50) + rng.Next(-12, 17) + ClassPitchBonus(player?.Classification ?? PlayerClassification.Unassigned), 20, 99);

        private static void NormalizeArsenalList(Player player)
        {
            var byType = player.PitchArsenal
                .Where(p => p != null && AllPitchTypes.Contains(p.PitchType))
                .GroupBy(p => p.PitchType)
                .ToDictionary(g => g.Key, g => g.First());
            player.PitchArsenal = AllPitchTypes.Select(pitch =>
            {
                if (byType.TryGetValue(pitch, out var existing))
                {
                    existing.Effectiveness = Math.Clamp(existing.Effectiveness, 0, 99);
                    return existing;
                }
                return new PlayerPitchProfile { PitchType = pitch, Enabled = false, Effectiveness = 40 };
            }).ToList();

            if (!player.PitchArsenal.Any(p => p.Enabled))
            {
                if (IsPitcherClassified(player))
                    player.PitchArsenal.First(p => p.PitchType == GameplayPitchType.Fastball).Enabled = true;
            }
        }

        private static void NormalizeBatterLists(Player player)
        {
            player.PitchStrengths = player.PitchStrengths.Where(AllPitchTypes.Contains).Distinct().ToList();
            player.PitchWeaknesses = player.PitchWeaknesses.Where(AllPitchTypes.Contains).Distinct().Except(player.PitchStrengths).ToList();
        }

        public static bool IsPitcherClassified(Player player)
            => player?.Role == PlayerRole.Pitcher || HasPitcherInSecondaryOrThirdPosition(player);

        public static bool HasPitcherInSecondaryOrThirdPosition(Player? player)
        {
            var parts = (player?.Positions ?? "")
                .Split(new[] { '/', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().ToUpperInvariant())
                .ToList();
            return parts.Skip(1).Take(2).Any(p => p == "P");
        }

        private static int EmergencyEffectiveness(Random rng)
        {
            if (rng.Next(100) < 80)
                return rng.Next(20, 61);
            return rng.Next(70, 90);
        }
    }
}
