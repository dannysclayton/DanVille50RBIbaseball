#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public enum InjuryExposureType
    {
        PlateAppearance,
        FieldingPlay,
        Baserunning,
        StealOrSlide,
        CatcherInning,
        PitchThrown,
        Collision
    }

    public static class InjuryEngine
    {
        private static readonly string[] PitcherInjuries =
        {
            "Shoulder soreness",
            "Elbow strain",
            "Forearm tightness",
            "Back stiffness"
        };

        private static readonly string[] PositionInjuries =
        {
            "Hamstring strain",
            "Wrist sprain",
            "Ankle sprain",
            "Shoulder bruise",
            "Knee soreness"
        };

        public static bool IsAvailable(Player player)
        {
            return player == null ||
                (!player.RedshirtActive &&
                 (player.InjuryStatus != PlayerInjuryStatus.Out || player.InjuryGamesRemaining <= 0));
        }

        public static int EffectiveRating(Player player, int rating)
        {
            rating = Math.Clamp(rating, 0, 99);
            return player?.InjuryStatus == PlayerInjuryStatus.DayToDay
                ? Math.Clamp((int)Math.Round(rating * 0.90), 0, 99)
                : rating;
        }

        public static bool TryEventInjury(Player player, Random rng, int extraChancePerThousand)
        {
            if (player == null || rng == null || player.RedshirtActive || player.InjuryStatus != PlayerInjuryStatus.Healthy)
                return false;
            int chance = Math.Clamp(BaseInjuryChance(player, null) + Math.Max(0, extraChancePerThousand), 2, 180);
            if (rng.Next(1000) >= chance)
                return false;
            AssignInjury(player, rng);
            return true;
        }

        public static bool TryParticipationInjury(
            Player? player,
            Team? team,
            Random? rng,
            InjuryExposureType exposure,
            int workload = 1)
        {
            if (player == null || rng == null || workload <= 0 || player.RedshirtActive ||
                player.InjuryStatus != PlayerInjuryStatus.Healthy)
            {
                return false;
            }

            int chance = ParticipationInjuryChancePerHundredThousand(player, team, exposure, workload);
            if (rng.Next(100000) >= chance)
                return false;

            AssignInjury(player, rng);
            return true;
        }

        public static int ParticipationInjuryChancePerHundredThousand(
            Player player,
            Team? team,
            InjuryExposureType exposure,
            int workload = 1)
        {
            if (player == null || workload <= 0)
                return 0;

            int baseChance = exposure switch
            {
                InjuryExposureType.PlateAppearance => 12,
                InjuryExposureType.FieldingPlay => 18,
                InjuryExposureType.Baserunning => 20,
                InjuryExposureType.StealOrSlide => 45,
                InjuryExposureType.CatcherInning => 28,
                InjuryExposureType.PitchThrown => 10,
                InjuryExposureType.Collision => 4000,
                _ => 0
            };

            int durabilityMultiplier = 70 + Math.Max(0, 99 - Math.Clamp(player.Durability, 0, 99));
            int chance = (int)Math.Ceiling(baseChance * durabilityMultiplier / 100.0) * Math.Clamp(workload, 1, 1000);
            if (exposure == InjuryExposureType.PitchThrown)
            {
                chance = (int)Math.Ceiling(chance *
                    (100 + PitchingRotationEngine.RotationInjuryRiskBonusPercent(team)) / 100.0);
                chance = (int)Math.Ceiling(chance *
                    (100 + Math.Max(0, player.ConsecutiveReliefGames) * 10) / 100.0);
            }

            return Math.Clamp(chance, 0, 99999);
        }

        public static int PregameInjuryChancePerThousand(Player player)
        {
            if (player == null)
                return 0;
            return Math.Clamp(player.Durability < 35 ? 2 : 1, 1, 2);
        }

        public static void ProcessGameInjuries(Team away, Team home, Random rng)
        {
            RecoverOneGame(away);
            RecoverOneGame(home);
            RollPregameInjuries(away, rng);
            RollPregameInjuries(home, rng);
            CountUnavailablePlayers(away);
            CountUnavailablePlayers(home);
        }

        public static string InjurySummary(Player player)
        {
            if (player == null || player.InjuryStatus == PlayerInjuryStatus.Healthy)
            {
                if (player?.RedshirtActive == true) return "Redshirt";
                if (player?.MedicalTagEligible == true) return "IR - Medical Eligible";
                return player?.MedicalTag == true ? "Medical Tag" : "";
            }

            string label = string.IsNullOrWhiteSpace(player.InjuryName) ? player.InjuryStatus.ToString() : player.InjuryName;
            if (player.InjuryStatus == PlayerInjuryStatus.DayToDay)
                return label + " (DTD)";

            return label + " (" + Math.Max(0, player.InjuryGamesRemaining) + "G)";
        }

        private static void CountUnavailablePlayers(Team? team)
        {
            var injuredReserveIds = (team?.InjuredReserve ?? new List<Player>()).Select(player => player.Id).ToHashSet();
            foreach (var player in TeamOrganizationPlayers(team))
            {
                if (injuredReserveIds.Contains(player.Id) ||
                    player.InjuryStatus == PlayerInjuryStatus.Out && player.InjuryGamesRemaining > 0)
                    player.InjuryMissedGamesThisSeason++;
            }
        }

        private static void RecoverOneGame(Team team)
        {
            foreach (var player in TeamOrganizationPlayers(team))
            {
                if (player.InjuryStatus == PlayerInjuryStatus.Healthy)
                    continue;

                if (player.InjuryGamesRemaining > 0)
                    player.InjuryGamesRemaining--;

                if (player.InjuryGamesRemaining <= 0)
                {
                    player.InjuryStatus = PlayerInjuryStatus.Healthy;
                    player.InjuryName = "";
                    player.InjurySeverity = 0;
                    player.InjuryGamesRemaining = 0;
                }
            }
        }

        private static IEnumerable<Player> TeamOrganizationPlayers(Team? team)
            => (team?.Roster ?? Enumerable.Empty<Player>())
                .Concat(team?.InjuredReserve ?? Enumerable.Empty<Player>())
                .Where(player => player != null)
                .GroupBy(player => player.Id)
                .Select(group => group.First());

        private static void RollPregameInjuries(Team? team, Random rng)
        {
            var candidates = (team?.Roster ?? new List<Player>())
                .Where(p => !p.RedshirtActive && p.InjuryStatus == PlayerInjuryStatus.Healthy)
                .ToList();
            if (candidates.Count == 0)
                return;

            foreach (var player in candidates)
            {
                int chance = PregameInjuryChancePerThousand(player);
                if (rng.Next(1000) < chance)
                    AssignInjury(player, rng);
            }
        }

        private static int BaseInjuryChance(Player player, Team? team)
        {
            int chance = player.Role == PlayerRole.Pitcher ? 7 : 5;
            chance += Math.Max(0, 55 - player.Durability) / 6;
            chance += player.InjurySeverity > 0 ? 2 : 0;
            if (player.Role == PlayerRole.Pitcher)
                chance += PitchingRotationEngine.RotationInjuryRiskBonusPercent(team) * 10;
            return Math.Clamp(chance, 2, 80);
        }

        private static void AssignInjury(Player player, Random rng)
        {
            int severityRoll = rng.Next(100);
            if (severityRoll < 58)
            {
                player.InjuryStatus = PlayerInjuryStatus.DayToDay;
                player.InjuryGamesRemaining = rng.Next(1, 4);
                player.InjurySeverity = 1;
            }
            else if (severityRoll < 92)
            {
                player.InjuryStatus = PlayerInjuryStatus.Out;
                player.InjuryGamesRemaining = rng.Next(4, 16);
                player.InjurySeverity = 2;
            }
            else
            {
                player.InjuryStatus = PlayerInjuryStatus.Out;
                player.InjuryGamesRemaining = rng.Next(16, 41);
                player.InjurySeverity = 3;
            }

            var list = player.Role == PlayerRole.Pitcher ? PitcherInjuries : PositionInjuries;
            player.InjuryName = list[rng.Next(list.Length)];
        }
    }
}
