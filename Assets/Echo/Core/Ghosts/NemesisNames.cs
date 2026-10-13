using System.Globalization;

namespace Echo.Core.Ghosts
{
    /// <summary>"The Seventh" up to 20, then "The 34th" (tech spec 7).</summary>
    public static class NemesisNames
    {
        static readonly string[] Words =
        {
            "Zeroth", "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth",
            "Eleventh", "Twelfth", "Thirteenth", "Fourteenth", "Fifteenth", "Sixteenth", "Seventeenth",
            "Eighteenth", "Nineteenth", "Twentieth",
        };

        public static string For(int runIndex) => "The " + Ordinal(runIndex);

        public static string Ordinal(int n)
        {
            if (n >= 1 && n <= 20) return Words[n];
            string digits = n.ToString(CultureInfo.InvariantCulture);
            int lastTwo = n % 100;
            if (lastTwo >= 11 && lastTwo <= 13) return digits + "th";
            switch (n % 10)
            {
                case 1: return digits + "st";
                case 2: return digits + "nd";
                case 3: return digits + "rd";
                default: return digits + "th";
            }
        }
    }
}
