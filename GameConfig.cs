namespace ClickerGame
{
    public static class GameConfig
    {
        public const int DefaultWidth = 800;
        public const int DefaultHeight = 600;
        public const float KeyFlashDuration = 0.25f;
        public const float MissFlashDuration = 0.35f;
        public const float ApproachTime = 1.5f;

        // Hit judgment windows (seconds)
        public const float PerfectWindow = 0.05f;
        public const float GreatWindow = 0.12f;
        public const float GoodWindow = 0.30f;

        // Scores per judgment
        public const int PerfectScore = 100;
        public const int GreatScore = 75;
        public const int GoodScore = 50;

        // Combo tier thresholds
        public const int ComboTier1 = 5;
        public const int ComboTier2 = 15;
        public const int ComboTier3 = 30;
        public const int ComboTier4 = 50;
    }
}
