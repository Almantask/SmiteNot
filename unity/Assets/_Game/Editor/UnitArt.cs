namespace GodGame.Editor
{
    /// <summary>
    /// Presentation helper for unit art ids. Every unit type—including engine crews (<c>U_crew_a</c>), cattle
    /// (<c>U_cattle_a</c>), and drovers (<c>U_drovers_a</c>)—now uses its authored <c>U_&lt;id&gt;_a</c> model and VAT.
    /// </summary>
    public static class UnitArt
    {
        public static bool AwaitsModel(string unitTypeId) => false;

        /// <summary>The art id of a unit type: its id without dashes (<c>U_&lt;clean id&gt;_a</c>).</summary>
        public static string CleanId(string unitTypeId) => unitTypeId.Replace("-", string.Empty);
    }
}
