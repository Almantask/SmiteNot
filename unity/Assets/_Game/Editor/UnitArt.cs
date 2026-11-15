namespace GodGame.Editor
{
    /// <summary>
    /// Which unit types have no model yet (presentation only). They are drawn as capsules until their art exists:
    /// drovers (M5 item 7). Everything else—including engine crews (<c>U_crew_a</c>) and cattle (<c>U_cattle_a</c>)—uses
    /// its <c>U_&lt;id&gt;_a</c> model.
    /// </summary>
    public static class UnitArt
    {
        public static bool AwaitsModel(string unitTypeId) => unitTypeId == "drovers";

        /// <summary>The art id of a unit type: its id without dashes (<c>U_&lt;clean id&gt;_a</c>).</summary>
        public static string CleanId(string unitTypeId) => unitTypeId.Replace("-", string.Empty);
    }
}
