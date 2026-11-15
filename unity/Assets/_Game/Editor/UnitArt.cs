namespace GodGame.Editor
{
    /// <summary>
    /// Which unit types have no model yet (presentation only). They are drawn as capsules until their art exists: the
    /// engine crews of S2 2.1 (M5 item 6). Everything else must have its <c>U_&lt;id&gt;_a</c> model.
    /// </summary>
    public static class UnitArt
    {
        public static bool AwaitsModel(string unitTypeId) => unitTypeId == "crew";

        /// <summary>The art id of a unit type: its id without dashes (<c>U_&lt;clean id&gt;_a</c>).</summary>
        public static string CleanId(string unitTypeId) => unitTypeId.Replace("-", string.Empty);
    }
}
