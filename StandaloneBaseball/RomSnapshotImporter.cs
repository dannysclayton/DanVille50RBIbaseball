using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace StandaloneBaseball
{
    public static class RomSnapshotImporter
    {
        private static readonly int[] TeamOffsets =
        {
            0x3D9, 0x3EA, 0x406, 0x419, 0x433, 0x446, 0x463, 0x473, 0x490, 0x4A2,
            0x4C0, 0x4D0, 0x4EB, 0x500, 0x51C, 0x52B, 0x548, 0x55D, 0x578, 0x587,
            0x5A2, 0x5B5, 0x5D6, 0x5E7, 0x601, 0x615, 0x62E, 0x641, 0x65D, 0x66D
        };

        private static readonly int[] TeamWidths =
        {
            9, 12, 10, 11, 11, 12, 9, 13, 10, 12,
            8, 12, 11, 10, 8, 13, 10, 9, 8, 13,
            12, 13, 6, 9, 9, 9, 10, 11, 9, 13
        };

        public static LeagueFile Import(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            ValidateHeader(data);

            var league = new LeagueFile
            {
                Name = Path.GetFileNameWithoutExtension(path) + " Snapshot"
            };

            for (int teamIndex = 0; teamIndex < TeamOffsets.Length; teamIndex++)
            {
                var team = new Team
                {
                    City = Decode(data, TeamOffsets[teamIndex], TeamWidths[teamIndex]).Trim(),
                    Nickname = "Club " + (teamIndex + 1),
                    ScoreboardAbbreviation = Team.Limit(Decode(data, TeamOffsets[teamIndex], TeamWidths[teamIndex]), Team.MaxScoreboardAbbreviationLength).ToUpperInvariant()
                };

                int colorOffset = 0xFF67 + teamIndex * 3;
                if (colorOffset + 2 < data.Length)
                {
                    team.PrimaryArgb = ColorFromRomIndex(data[colorOffset]).ToArgb();
                    team.SecondaryArgb = ColorFromRomIndex(data[colorOffset + 2]).ToArgb();
                }

                int playerBase = 0x6010 + teamIndex * 16 * 16;
                for (int p = 0; p < 16; p++)
                {
                    int o = playerBase + p * 16;
                    byte slot = data[o];
                    bool pitcher = slot >= 12;
                    team.Roster.Add(new Player
                    {
                        Name = Decode(data, o + 1, 6),
                        Role = pitcher ? PlayerRole.Pitcher : PlayerRole.Batter,
                        Contact = pitcher ? 30 : Scale(data[o + 10]),
                        Power = pitcher ? 25 : Scale(data[o + 9]),
                        Speed = Scale(data[o + 13]),
                        Pitching = pitcher ? Scale(data[o + 9]) : 25,
                        Stamina = pitcher ? Scale(data[o + 13]) : 35
                    });
                }
                league.Teams.Add(team);
            }

            league.Seasons.Add(new Season { Year = DateTime.Now.Year, Name = DateTime.Now.Year + " Season" });
            PlayoffEngine.EnsureDefaultStructure(league);
            return league;
        }

        private static void ValidateHeader(byte[] data)
        {
            if (data.Length < 16 || data[0] != 0x4E || data[1] != 0x45 || data[2] != 0x53 || data[3] != 0x1A)
                throw new InvalidDataException("Not an iNES ROM.");

            int prg = data[4] * 16384;
            int chr = data[5] * 8192;
            int expected = 16 + prg + chr;
            if (data.Length < expected)
                throw new InvalidDataException("The ROM file is shorter than its iNES header describes.");
            if (0x6010 + 30 * 16 * 16 > data.Length)
                throw new InvalidDataException("The expected player table is outside this file.");
        }

        private static int Scale(byte b)
        {
            int n = (int)Math.Round(b / 255.0 * 99.0);
            return n < 1 ? 1 : n > 99 ? 99 : n;
        }

        private static string Decode(byte[] data, int offset, int length)
        {
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                chars[i] = b >= 0x0A && b <= 0x23 ? (char)('A' + b - 0x0A)
                    : b >= 0x28 && b <= 0x41 ? (char)('a' + b - 0x28)
                    : b == 0x24 ? ' '
                    : b == 0x25 ? '.'
                    : '?';
            }
            return new string(chars).TrimEnd();
        }

        private static Color ColorFromRomIndex(byte idx)
        {
            int hue = (idx * 47) % 360;
            return FromHsv(hue, 0.62, 0.82);
        }

        private static Color FromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);
            value *= 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));
            return hi switch
            {
                0 => Color.FromArgb(v, t, p),
                1 => Color.FromArgb(q, v, p),
                2 => Color.FromArgb(p, v, t),
                3 => Color.FromArgb(p, q, v),
                4 => Color.FromArgb(t, p, v),
                _ => Color.FromArgb(v, p, q)
            };
        }
    }
}
