using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    internal static class GameLineupTracker
    {
        public static void RecordPitcherChange(
            List<GameLineupEntry> entries,
            Player incoming,
            Player? outgoing,
            int inning,
            HalfInning half,
            int battingOrder = 0,
            Player? displacedBatter = null,
            string reason = "Pitcher change")
        {
            if (entries == null || incoming == null)
                return;

            GameLineupEntry? outgoingEntry = outgoing == null ? null : ActiveEntry(entries, outgoing.Id);
            if (outgoingEntry != null)
            {
                outgoingEntry.ExitedInning = inning;
                outgoingEntry.ExitedHalf = half;
            }

            GameLineupEntry? displacedEntry = displacedBatter == null ? null : ActiveEntry(entries, displacedBatter.Id);
            if (displacedEntry != null && displacedEntry.PlayerId != outgoing?.Id)
            {
                displacedEntry.ExitedInning = inning;
                displacedEntry.ExitedHalf = half;
            }

            GameLineupEntry? entry = entries.LastOrDefault(item => item.PlayerId == incoming.Id && !item.ExitedInning.HasValue);
            if (entry == null)
            {
                entry = new GameLineupEntry
                {
                    BattingOrder = battingOrder,
                    AppearanceOrder = entries.Count + 1,
                    PlayerId = incoming.Id,
                    PlayerName = incoming.Name ?? "",
                    DefensivePosition = "P",
                    IsStarter = false,
                    ReplacedPlayerId = displacedBatter?.Id ?? outgoing?.Id,
                    ReplacedPlayerName = displacedBatter?.Name ?? outgoing?.Name ?? "",
                    EnteredInning = Math.Max(1, inning),
                    EnteredHalf = half,
                    Positions = incoming.Positions ?? "",
                    BatGrade = LineupCardExporter.BatGrade(incoming)
                };
                entries.Add(entry);
            }
            else if (battingOrder > 0)
            {
                entry.BattingOrder = battingOrder;
            }

            RecordPosition(entry, inning, half, "P", reason);
        }

        public static void RecordPositionChange(List<GameLineupEntry> entries, Player player, int inning, HalfInning half, string position, string reason)
        {
            if (entries == null || player == null || string.IsNullOrWhiteSpace(position))
                return;
            GameLineupEntry? entry = ActiveEntry(entries, player.Id) ?? entries.LastOrDefault(item => item.PlayerId == player.Id);
            if (entry == null)
            {
                entry = new GameLineupEntry
                {
                    AppearanceOrder = entries.Count + 1,
                    PlayerId = player.Id,
                    PlayerName = player.Name ?? "",
                    DefensivePosition = position,
                    IsStarter = false,
                    EnteredInning = Math.Max(1, inning),
                    EnteredHalf = half,
                    Positions = player.Positions ?? "",
                    BatGrade = LineupCardExporter.BatGrade(player)
                };
                entries.Add(entry);
            }
            RecordPosition(entry, inning, half, position, reason);
        }

        private static GameLineupEntry? ActiveEntry(IEnumerable<GameLineupEntry> entries, Guid playerId)
            => entries.LastOrDefault(item => item.PlayerId == playerId && !item.ExitedInning.HasValue);

        private static void RecordPosition(GameLineupEntry entry, int inning, HalfInning half, string position, string reason)
        {
            entry.PositionHistory ??= new List<GamePositionChange>();
            if (!entry.PositionHistory.Any(change => change.Inning == inning && change.Half == half &&
                string.Equals(change.Position, position, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(change.Reason, reason, StringComparison.OrdinalIgnoreCase)))
            {
                entry.PositionHistory.Add(new GamePositionChange { Inning = Math.Max(1, inning), Half = half, Position = position, Reason = reason ?? "" });
            }
            entry.DefensivePosition = position;
            entry.DesignatedHitter = string.Equals(position, "DH", StringComparison.OrdinalIgnoreCase);
        }
    }
}
