using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class BaseballFieldPreset
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string TeamLabel { get; set; } = "";
        public int OpenedYear { get; set; }
        public Color GrassColor { get; set; } = Color.FromArgb(34, 130, 68);
        public Color DarkGrassColor { get; set; } = Color.FromArgb(13, 74, 48);
        public Color InfieldColor { get; set; } = Color.FromArgb(182, 129, 70);
        public Color ClayColor { get; set; } = Color.FromArgb(158, 103, 56);
        public Color WallColor { get; set; } = Color.FromArgb(20, 76, 44);
        public Color SeatColor { get; set; } = Color.FromArgb(33, 96, 72);
        public Color StructureColor { get; set; } = Color.FromArgb(150, 132, 105);
        public Color AccentColor { get; set; } = Color.FromArgb(195, 65, 48);
        public string BackgroundAssetPath { get; set; } = "";
        public int Variant { get; set; }
        public float FenceTopOffset { get; set; } = -0.20f;
        public float FenceStartAngle { get; set; } = 20f;
        public float FenceSweepAngle { get; set; } = 140f;
        public bool UserCreated { get; set; }
        public List<FieldImageOverlay> Overlays { get; set; } = new List<FieldImageOverlay>();

        public string DisplayName => UserCreated ? "Custom - " + Name : OpenedYear + " - " + Name;

        public override string ToString() => DisplayName;
    }

    public static class BaseballFieldPresets
    {
        private static readonly List<BaseballFieldPreset> Presets = new List<BaseballFieldPreset>
        {
            Field("league-stadium", "League Stadium", "Dubois County Bombers", 1894, 0, "#3F8B48", "#1E5F3D", "#B87C48", "#7B5334", "#9B3737", "#C6B186", "#D7C4A2", "#7F2E2C"),
            Field("fenway-park", "Fenway Park", "Red Sox", 1912, 1, "#2F8E4E", "#174F36", "#B97B42", "#8D5C38", "#0B5A38", "#0A583D", "#91442F", "#C13E36"),
            Field("wrigley-field", "Wrigley Field", "Cubs", 1914, 2, "#3D9A58", "#1D6541", "#BF8049", "#9A6136", "#2D3F39", "#2E7B58", "#9A4F37", "#284C78"),
            Field("jackie-robinson-ballpark", "Jackie Robinson Ballpark", "Tortugas", 1914, 3, "#43A46B", "#216A4E", "#C88A53", "#9E6C45", "#3BAA96", "#9ED7CB", "#E5E0C9", "#237A73"),
            Field("bosse-field", "Bosse Field", "Otters", 1915, 4, "#378A4D", "#1A5638", "#B97842", "#8F5735", "#4B5149", "#375C55", "#D0C8A8", "#253B35"),
            Field("simmons-field", "Simmons Field", "Kenosha Kingfish", 1920, 5, "#3F9652", "#1F6541", "#BA7A43", "#8E5E3B", "#6F6D62", "#345E4C", "#C9C1AA", "#42423B"),
            Field("lecom-park", "LECOM Park", "Bradenton Marauders", 1923, 6, "#429B5A", "#1F6943", "#C4773E", "#9C5C34", "#DA6D36", "#2B684A", "#F2EFE2", "#CB4D28"),
            Field("mccormick-field", "McCormick Field", "Asheville Tourists", 1924, 7, "#3B9250", "#1C5C3D", "#BF8048", "#8B5D3B", "#5B6E55", "#8F8872", "#7C735F", "#2F5A43"),
            Field("grayson-stadium", "Grayson Stadium", "Savannah Bananas", 1926, 8, "#4BAA5F", "#226A43", "#C28A50", "#9B6842", "#7F6542", "#1F6047", "#D9C990", "#E3B33C"),
            Field("synovus-park", "Synovus Park", "Columbus Clingstones", 1926, 9, "#378C4B", "#1A5938", "#B87942", "#8D5635", "#263B36", "#284D45", "#B77B58", "#2D3330"),
            Field("bowman-field", "Journey Bank Ballpark at Historic Bowman Field", "Crosscutters", 1926, 10, "#3E9653", "#1F6443", "#BD7E47", "#8D5E3A", "#0A765F", "#4D7661", "#D5D1B8", "#0B6B58"),
            Field("newman-park", "Newman Park", "Catawba Indians", 1926, 11, "#41965C", "#1B6144", "#BE824C", "#8B6040", "#20654F", "#2E765C", "#B88A5D", "#16604C"),
            Field("luther-williams-field", "Luther Williams Field", "Macon Bacon", 1929, 12, "#37894A", "#1B5839", "#B67841", "#895334", "#303C38", "#285644", "#BE9771", "#8A4230"),
            Field("modern-woodmen-park", "Modern Woodmen Park", "River Bandits", 1931, 13, "#4AA866", "#226D4C", "#C5894F", "#9A6740", "#5D858C", "#77A9B0", "#D1D2C8", "#47767E"),
            Field("hinchliffe-stadium", "Hinchliffe Stadium", "Jackals", 1932, 14, "#3E9456", "#1E6444", "#C08049", "#8E5F3C", "#6B625A", "#235B45", "#B8B0A7", "#6E564D"),
            Field("dunn-field", "Dunn Field", "Elmira Pioneers", 1939, 15, "#3D9D62", "#1E6647", "#C58A52", "#9C6841", "#00917C", "#28A99A", "#C5BDA8", "#0C7569"),
            Field("city-stadium", "City Stadium", "Hill City Howlers", 1940, 16, "#3D8F4C", "#1B5A37", "#B87A43", "#8A5836", "#4D5A4B", "#8B826F", "#7B6D58", "#244332"),
            Field("excite-ballpark", "Excite Ballpark", "San Jose Giants", 1942, 17, "#43A45E", "#216B45", "#C98A4E", "#98633B", "#DAD1B9", "#1F704E", "#F3F1DF", "#29724F"),
            Field("firstenergy-stadium", "FirstEnergy Stadium", "Reading Fightin Phils", 1951, 18, "#398F55", "#1A6040", "#C47E48", "#965D38", "#6A3332", "#266D84", "#A34A3E", "#267DA0"),
            Field("historic-sanford-memorial-stadium", "Historic Sanford Memorial Stadium", "Sanford River Rats", 1951, 19, "#45A45E", "#216940", "#C98B50", "#9D6840", "#2E5C4D", "#22634D", "#DAD4B9", "#6A8A58"),
            Field("acu-baseball-field", "ACU Baseball Field", "Abilene Christian Wildcats", 2026, 20, "#78D122", "#2B641E", "#B77267", "#6F3D2D", "#07182B", "#0B2742", "#B8794F", "#7B2CBF", "Assets\\Stadiums\\acu-field.png"),
            Field("ballpark-at-arlington", "The Ballpark in Arlington", "Texas Rangers", 1994, 21, "#6A9C51", "#244E35", "#B97A4D", "#D6C8A0", "#1B2835", "#244D73", "#C9C2B4", "#C42235", "Assets\\Stadiums\\ballpark-at-arlington.jpg"),
            Field("field-of-dreams-custom", "Field of Dreams", "Custom Home Field", 2026, 22, "#4F9A43", "#224F2E", "#C58C4B", "#9C6C3B", "#254229", "#5B6E4B", "#C7B58E", "#F0D25A", "Assets\\Stadiums\\Custom\\field-of-dreams.dib"),
            Field("custom-field-1", "Custom Field 1", "Custom Home Field", 2026, 23, "#2F874D", "#142B28", "#D3934C", "#A86B34", "#30343A", "#B7192D", "#5D6068", "#DD1F3A", "Assets\\Stadiums\\Custom\\custom-field-1.jpg"),
            Field("custom-field-2", "Custom Field 2", "Custom Home Field", 2026, 24, "#2E8B4D", "#1E5D3B", "#BF7B45", "#8C5736", "#355E45", "#C13F32", "#86613C", "#244E83", "Assets\\Stadiums\\Custom\\custom-field-2.jpg"),
            Field("custom-field-3", "Custom Field 3", "Custom Home Field", 2026, 25, "#4F9A40", "#24542D", "#B7772C", "#D3A12F", "#1A5429", "#2A6A35", "#A97632", "#F2A51E", "Assets\\Stadiums\\Custom\\custom-field-3.jpg"),
            Field("custom-field-5", "Custom Field 5", "Custom Home Field", 2026, 26, "#177716", "#0B520B", "#F2B44D", "#EFA33F", "#1A6D1A", "#2F8D2F", "#E8C15A", "#F6C047", "Assets\\Stadiums\\Custom\\custom-field-5.jpg"),
            Field("custom-field-6", "Custom Field 6", "Custom Home Field", 2026, 27, "#3D8B2C", "#005386", "#E0A35B", "#C47D39", "#222F25", "#3A7B50", "#C7C7B8", "#C9D6E8", "Assets\\Stadiums\\Custom\\custom-field-6.jpg"),
            Field("custom-field-7", "Custom Field 7", "Custom Home Field", 2026, 28, "#608D35", "#203A21", "#D8934B", "#C77735", "#182D31", "#557A88", "#E7E0C9", "#FFFFFF", "Assets\\Stadiums\\Custom\\custom-field-7.jpg"),
            Field("custom-field-8", "Custom Field 8", "Custom Home Field", 2026, 29, "#4C8E38", "#235821", "#F19A20", "#E1811B", "#3C7028", "#5D8A3D", "#E6A22C", "#FFFFFF", "Assets\\Stadiums\\Custom\\custom-field-8.jpg"),
            Field("custom-field-9", "Custom Field 9", "Custom Home Field", 2026, 30, "#21994D", "#0E5D25", "#C69B6E", "#B58960", "#136628", "#1C8B43", "#B98E63", "#F0E9D8", "Assets\\Stadiums\\Custom\\custom-field-9.jpg"),
            Field("custom-field-10-overhead-diamond", "Custom Field 10 - Overhead Diamond", "Custom Home Field", 2026, 31, "#35B44A", "#07924E", "#E0AA7C", "#D49B65", "#8C6239", "#1FA164", "#B78359", "#FFFFFF", "Assets\\Stadiums\\Custom\\custom-field-10-overhead-diamond.jpg"),
            Field("custom-field-11-stadium-model", "Custom Field 11 - Stadium Model", "Custom Home Field", 2026, 32, "#8FD014", "#689F1F", "#F2C189", "#E0A765", "#2F3D38", "#BCA889", "#7B6248", "#CC3A28", "Assets\\Stadiums\\Custom\\custom-field-11-stadium-model.png"),
            Field("buda-johnson-field", "Buda Johnson Field", "Johnson Jaguars", 2026, 33, "#4F9D3D", "#245F2B", "#352018", "#1F1713", "#111111", "#D7D7D7", "#C9C4B9", "#C6A348", "Assets\\Stadiums\\Custom\\buda-johnson-field.jpg"),
            Field("aledo-field", "Aledo Field", "Aledo Bearcats", 2026, 34, "#58A53D", "#2A6A2D", "#332018", "#211610", "#143D2B", "#C6B8A6", "#B9B0A2", "#F05A24", "Assets\\Stadiums\\Custom\\aledo-field.jpg")
        };

        public static IReadOnlyList<BaseballFieldPreset> All => Presets;

        public static BaseballFieldPreset Default => Presets[0];

        public static BaseballFieldPreset Find(string? id)
            => Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;

        public static BaseballFieldPreset FromCustom(CustomBaseballField? field)
        {
            if (field == null)
                return Default;

            return new BaseballFieldPreset
            {
                Id = field.Id,
                Name = string.IsNullOrWhiteSpace(field.Name) ? "Custom Field" : field.Name,
                TeamLabel = string.IsNullOrWhiteSpace(field.TeamLabel) ? "Custom Home Field" : field.TeamLabel,
                OpenedYear = field.OpenedYear <= 0 ? DateTime.Now.Year : field.OpenedYear,
                GrassColor = Color.FromArgb(field.GrassArgb),
                DarkGrassColor = Color.FromArgb(field.DarkGrassArgb),
                InfieldColor = Color.FromArgb(field.InfieldArgb),
                ClayColor = Color.FromArgb(field.ClayArgb),
                WallColor = Color.FromArgb(field.WallArgb),
                SeatColor = Color.FromArgb(field.SeatArgb),
                StructureColor = Color.FromArgb(field.StructureArgb),
                AccentColor = Color.FromArgb(field.AccentArgb),
                BackgroundAssetPath = field.BackgroundAssetPath ?? "",
                Variant = 31,
                UserCreated = true,
                Overlays = field.Overlays ?? new List<FieldImageOverlay>()
            };
        }

        private static BaseballFieldPreset Field(
            string id,
            string name,
            string teamLabel,
            int openedYear,
            int variant,
            string grass,
            string darkGrass,
            string infield,
            string clay,
            string wall,
            string seats,
            string structure,
            string accent,
            string backgroundAssetPath = "")
        {
            return new BaseballFieldPreset
            {
                Id = id,
                Name = name,
                TeamLabel = teamLabel,
                OpenedYear = openedYear,
                Variant = variant,
                GrassColor = Hex(grass),
                DarkGrassColor = Hex(darkGrass),
                InfieldColor = Hex(infield),
                ClayColor = Hex(clay),
                WallColor = Hex(wall),
                SeatColor = Hex(seats),
                StructureColor = Hex(structure),
                AccentColor = Hex(accent),
                BackgroundAssetPath = backgroundAssetPath,
                FenceTopOffset = variant is 7 or 16 ? -0.12f : -0.20f,
                FenceStartAngle = variant is 1 or 11 ? 12f : 20f,
                FenceSweepAngle = variant is 1 or 11 ? 152f : 140f
            };
        }

        private static Color Hex(string value)
        {
            value = value.TrimStart('#');
            return Color.FromArgb(
                255,
                Convert.ToInt32(value.Substring(0, 2), 16),
                Convert.ToInt32(value.Substring(2, 2), 16),
                Convert.ToInt32(value.Substring(4, 2), 16));
        }
    }
}
