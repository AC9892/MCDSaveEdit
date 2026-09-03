namespace MCDSaveEdit.Data
{
    // Local development configuration. This file is intentionally ignored by Git.
    public static class Secrets
    {
        public static readonly AesKey[] PAKS_AES_KEYS = new[]
        {
            new AesKey("0x7D5F892ECEBFA53CC22001DF48B871D51C0DF7C54CE41933BFB285219829B3A8", "1.17.0.0"),
        };
        public const string GAME_ANALYTICS_GAME_KEY = "";
        public const string GAME_ANALYTICS_SECRET_KEY = "";
    }
}
