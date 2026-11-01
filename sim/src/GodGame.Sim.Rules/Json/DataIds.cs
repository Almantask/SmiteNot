namespace GodGame.Sim.Rules.Json
{
    /// <summary>
    /// Ids in rules, maps and plans: 1 to 64 characters from a-z, 0-9, '_' and '-'. Plain ASCII keeps ids identical
    /// on every platform, in every file name and through every Unicode normalisation.
    /// </summary>
    public static class DataIds
    {
        public const int MaxLength = 64;

        public static string Require(JsonValue value)
        {
            string id = value.AsString;
            if (id.Length == 0 || id.Length > MaxLength)
            {
                throw new DataFormatException(value.Path + ": an id needs 1 to 64 characters.");
            }
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                {
                    throw new DataFormatException(
                        value.Path + ": ids use only a-z, 0-9, '_' and '-' (\"" + id + "\")."
                    );
                }
            }
            return id;
        }
    }
}
