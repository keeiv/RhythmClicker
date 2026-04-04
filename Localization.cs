using System.Collections.Generic;

namespace ClickerGame
{
    public enum GameLanguage
    {
        English,
        EnglishUS,
        ZhTW,
        ZhCN,
        ZhHK,
    }

    public static class Localization
    {
        static GameLanguage _current = GameLanguage.English;

        public static GameLanguage Current
        {
            get => _current;
            set => _current = value;
        }

        public static readonly GameLanguage[] All = new[]
        {
            GameLanguage.English,
            GameLanguage.EnglishUS,
            GameLanguage.ZhTW,
            GameLanguage.ZhCN,
            GameLanguage.ZhHK,
        };

        public static string LanguageDisplayName(GameLanguage lang) => lang switch
        {
            GameLanguage.English   => "English",
            GameLanguage.EnglishUS => "English (US)",
            GameLanguage.ZhTW      => "\u7e41\u9ad4\u4e2d\u6587\uff08\u53f0\u7063\uff09",
            GameLanguage.ZhCN      => "\u7b80\u4f53\u4e2d\u6587\uff08\u4e2d\u56fd\u5927\u9646\uff09",
            GameLanguage.ZhHK      => "\u7e41\u9ad4\u4e2d\u6587\uff08\u9999\u6e2f\uff09",
            _ => "English",
        };

        static readonly Dictionary<string, Dictionary<GameLanguage, string>> _strings = new()
        {
            ["title"] = L("CLICK"),
            ["menu_start"] = L("Start Game", "Start Game", "\u958b\u59cb\u904a\u6232", "\u5f00\u59cb\u6e38\u620f", "\u958b\u59cb\u904a\u6232"),
            ["menu_editor"] = L("Beatmap Editor", "Beatmap Editor", "\u8b5c\u9762\u7de8\u8f2f\u5668", "\u8c31\u9762\u7f16\u8f91\u5668", "\u8b5c\u9762\u7de8\u8f2f\u5668"),
            ["menu_stats"] = L("Statistics", "Statistics", "\u7d71\u8a08\u6578\u64da", "\u7edf\u8ba1\u6570\u636e", "\u7d71\u8a08\u6578\u64da"),
            ["menu_account"] = L("Account", "Account", "\u5e33\u865f", "\u8d26\u53f7", "\u5e33\u865f"),
            ["menu_language"] = L("Language", "Language", "\u8a9e\u8a00", "\u8bed\u8a00", "\u8a9e\u8a00"),
            ["menu_exit"] = L("Exit", "Exit", "\u96e2\u958b", "\u9000\u51fa", "\u96e2\u958b"),
            ["hint_menu"] = L(
                "\u2191\u2193 Select   \u25c0\u25b6 Difficulty   Tab Song   Enter Confirm   Esc Exit",
                "\u2191\u2193 Select   \u25c0\u25b6 Difficulty   Tab Song   Enter Confirm   Esc Exit",
                "\u2191\u2193 \u9078\u64c7   \u25c0\u25b6 \u96e3\u5ea6   Tab \u5207\u6b4c   Enter \u78ba\u8a8d   Esc \u96e2\u958b",
                "\u2191\u2193 \u9009\u62e9   \u25c0\u25b6 \u96be\u5ea6   Tab \u5207\u6b4c   Enter \u786e\u8ba4   Esc \u9000\u51fa",
                "\u2191\u2193 \u9078\u64c7   \u25c0\u25b6 \u96e3\u5ea6   Tab \u5207\u6b4c   Enter \u78ba\u8a8d   Esc \u96e2\u958b"),
            ["difficulty"] = L("Difficulty", "Difficulty", "\u96e3\u5ea6", "\u96be\u5ea6", "\u96e3\u5ea6"),
            ["score"] = L("Score", "Score", "\u5206\u6578", "\u5206\u6570", "\u5206\u6578"),
            ["result"] = L("RESULT"),
            ["max_combo"] = L("Max Combo", "Max Combo", "\u6700\u5927\u9023\u64ca", "\u6700\u5927\u8fde\u51fb", "\u6700\u5927\u9023\u64ca"),
            ["hit"] = L("Hit", "Hit", "\u547d\u4e2d", "\u547d\u4e2d", "\u547d\u4e2d"),
            ["miss"] = L("Miss", "Miss", "\u5931\u8aa4", "\u5931\u8bef", "\u5931\u8aa4"),
            ["accuracy"] = L("Accuracy", "Accuracy", "\u6e96\u78ba\u7387", "\u51c6\u786e\u7387", "\u6e96\u78ba\u7387"),
            ["retry"] = L("Retry", "Retry", "\u91cd\u8a66", "\u91cd\u8bd5", "\u91cd\u8a66"),
            ["menu"] = L("Menu", "Menu", "\u9078\u55ae", "\u83dc\u5355", "\u9078\u55ae"),
            ["account_register"] = L("Register", "Register", "\u8a3b\u518a\u5e33\u865f", "\u6ce8\u518c\u8d26\u53f7", "\u8a3b\u518a\u5e33\u865f"),
            ["account_login"] = L("Login", "Login", "\u767b\u5165\u5e33\u865f", "\u767b\u5f55\u8d26\u53f7", "\u767b\u5165\u5e33\u865f"),
            ["login_success"] = L("Login successful", "Login successful", "\u767b\u5165\u6210\u529f", "\u767b\u5f55\u6210\u529f", "\u767b\u5165\u6210\u529f"),
            ["sync_ok"] = L("Cloud sync complete", "Cloud sync complete", "\u96f2\u7aef\u540c\u6b65\u5b8c\u6210", "\u4e91\u7aef\u540c\u6b65\u5b8c\u6210", "\u96f2\u7aef\u540c\u6b65\u5b8c\u6210"),
            ["sync_fail"] = L("Cloud sync failed", "Cloud sync failed", "\u96f2\u7aef\u540c\u6b65\u5931\u6557", "\u4e91\u7aef\u540c\u6b65\u5931\u8d25", "\u96f2\u7aef\u540c\u6b65\u5931\u6557"),
            ["register_success"] = L("Registered", "Registered", "\u8a3b\u518a\u6210\u529f", "\u6ce8\u518c\u6210\u529f", "\u8a3b\u518a\u6210\u529f"),
            ["hint_account"] = L(
                "Enter Submit  \u00b7  Tab Field  \u00b7  F1 Login/Register  \u00b7  Esc Cancel",
                "Enter Submit  \u00b7  Tab Field  \u00b7  F1 Login/Register  \u00b7  Esc Cancel",
                "Enter \u9001\u51fa  \u00b7  Tab \u5207\u63db  \u00b7  F1 \u767b\u5165/\u8a3b\u518a  \u00b7  Esc \u53d6\u6d88",
                "Enter \u63d0\u4ea4  \u00b7  Tab \u5207\u6362  \u00b7  F1 \u767b\u5f55/\u6ce8\u518c  \u00b7  Esc \u53d6\u6d88",
                "Enter \u9001\u51fa  \u00b7  Tab \u5207\u63db  \u00b7  F1 \u767b\u5165/\u8a3b\u518a  \u00b7  Esc \u53d6\u6d88"),
            ["logged_in_as"] = L("Logged in", "Logged in", "\u5df2\u767b\u5165", "\u5df2\u767b\u5f55", "\u5df2\u767b\u5165"),
            ["username"] = L("Username", "Username", "\u4f7f\u7528\u8005\u540d\u7a31", "\u7528\u6237\u540d", "\u7528\u6236\u540d\u7a31"),
            ["password"] = L("Password", "Password", "\u5bc6\u78bc", "\u5bc6\u7801", "\u5bc6\u78bc"),
            ["editor_hint"] = L("EDITOR  \u00b7  S save", "EDITOR  \u00b7  S save", "\u7de8\u8f2f  \u00b7  S \u5132\u5b58", "\u7f16\u8f91  \u00b7  S \u4fdd\u5b58", "\u7de8\u8f2f  \u00b7  S \u5132\u5b58"),
            ["unknown"] = L("Unknown", "Unknown", "\u672a\u77e5", "\u672a\u77e5", "\u672a\u77e5"),
            ["select_language"] = L("Select Language", "Select Language", "\u9078\u64c7\u8a9e\u8a00", "\u9009\u62e9\u8bed\u8a00", "\u9078\u64c7\u8a9e\u8a00"),
            ["hint_language"] = L(
                "\u2191\u2193 Select   Enter Confirm   Esc Back",
                "\u2191\u2193 Select   Enter Confirm   Esc Back",
                "\u2191\u2193 \u9078\u64c7   Enter \u78ba\u8a8d   Esc \u8fd4\u56de",
                "\u2191\u2193 \u9009\u62e9   Enter \u786e\u8ba4   Esc \u8fd4\u56de",
                "\u2191\u2193 \u9078\u64c7   Enter \u78ba\u8a8d   Esc \u8fd4\u56de"),
            // Stats
            ["stats_title"] = L("STATISTICS", "STATISTICS", "\u7d71\u8a08\u6578\u64da", "\u7edf\u8ba1\u6570\u636e", "\u7d71\u8a08\u6578\u64da"),
            ["total_plays"] = L("Total Plays", "Total Plays", "\u7e3d\u904a\u73a9", "\u603b\u6e38\u73a9", "\u7e3d\u904a\u73a9"),
            ["avg_accuracy"] = L("Avg Accuracy", "Avg Accuracy", "\u5e73\u5747\u6e96\u78ba\u7387", "\u5e73\u5747\u51c6\u786e\u7387", "\u5e73\u5747\u6e96\u78ba\u7387"),
            ["best_score"] = L("Best Score", "Best Score", "\u6700\u9ad8\u5206", "\u6700\u9ad8\u5206", "\u6700\u9ad8\u5206"),
            ["best_combo"] = L("Best Combo", "Best Combo", "\u6700\u9ad8\u9023\u64ca", "\u6700\u9ad8\u8fde\u51fb", "\u6700\u9ad8\u9023\u64ca"),
            ["total_hit"] = L("Total Hit", "Total Hit", "\u7e3d\u547d\u4e2d", "\u603b\u547d\u4e2d", "\u7e3d\u547d\u4e2d"),
            ["total_miss"] = L("Total Miss", "Total Miss", "\u7e3d\u5931\u8aa4", "\u603b\u5931\u8bef", "\u7e3d\u5931\u8aa4"),
            ["grade_dist"] = L("Grades", "Grades", "\u8a55\u7d1a", "\u8bc4\u7ea7", "\u8a55\u7d1a"),
            ["recent_plays"] = L("Recent", "Recent", "\u6700\u8fd1", "\u6700\u8fd1", "\u6700\u8fd1"),
            ["hint_stats"] = L("Esc Back", "Esc Back", "Esc \u8fd4\u56de", "Esc \u8fd4\u56de", "Esc \u8fd4\u56de"),
            ["no_data"] = L("No data yet", "No data yet", "\u5c1a\u7121\u6578\u64da", "\u6682\u65e0\u6570\u636e", "\u5c1a\u7121\u6578\u64da"),
            // Editor
            ["editor_title"] = L("BEATMAP EDITOR", "BEATMAP EDITOR", "\u8b5c\u9762\u7de8\u8f2f\u5668", "\u8c31\u9762\u7f16\u8f91\u5668", "\u8b5c\u9762\u7de8\u8f2f\u5668"),
            ["editor_name"] = L("Song Name", "Song Name", "\u6b4c\u66f2\u540d\u7a31", "\u6b4c\u66f2\u540d\u79f0", "\u6b4c\u66f2\u540d\u7a31"),
            ["editor_author"] = L("Author", "Author", "\u4f5c\u8005", "\u4f5c\u8005", "\u4f5c\u8005"),
            ["editor_audio"] = L("Audio File", "Audio File", "\u97f3\u6a94", "\u97f3\u9891", "\u97f3\u6a94"),
            ["editor_bpm"] = L("BPM"),
            ["editor_save"] = L("Save (Ctrl+S)", "Save (Ctrl+S)", "\u5132\u5b58 (Ctrl+S)", "\u4fdd\u5b58 (Ctrl+S)", "\u5132\u5b58 (Ctrl+S)"),
            ["editor_play"] = L("Preview (Space)", "Preview (Space)", "\u9810\u89bd (Space)", "\u9884\u89c8 (Space)", "\u9810\u89bd (Space)"),
            ["editor_notes"] = L("Notes", "Notes", "\u97f3\u7b26", "\u97f3\u7b26", "\u97f3\u7b26"),
            ["editor_hint_main"] = L(
                "Click place  \u00b7  Right-click delete  \u00b7  Scroll timeline  \u00b7  Drag notes  \u00b7  Drop audio",
                "Click place  \u00b7  Right-click delete  \u00b7  Scroll timeline  \u00b7  Drag notes  \u00b7  Drop audio",
                "\u5de6\u9375\u653e\u7f6e  \u00b7  \u53f3\u9375\u522a\u9664  \u00b7  \u6efe\u8f2a\u6372\u52d5  \u00b7  \u62d6\u66f3\u97f3\u7b26  \u00b7  \u62d6\u5165\u97f3\u6a94",
                "\u5de6\u952e\u653e\u7f6e  \u00b7  \u53f3\u952e\u5220\u9664  \u00b7  \u6eda\u8f6e\u6eda\u52a8  \u00b7  \u62d6\u62fd\u97f3\u7b26  \u00b7  \u62d6\u5165\u97f3\u9891",
                "\u5de6\u9375\u653e\u7f6e  \u00b7  \u53f3\u9375\u522a\u9664  \u00b7  \u6efe\u8f2a\u6372\u52d5  \u00b7  \u62d6\u66f3\u97f3\u7b26  \u00b7  \u62d6\u5165\u97f3\u6a94"),
            ["editor_err_audio"] = L("Audio file required", "Audio file required", "\u9700\u8981\u97f3\u6a94", "\u9700\u8981\u97f3\u9891", "\u9700\u8981\u97f3\u6a94"),
            ["editor_err_notes"] = L("Need \u22651 note", "Need \u22651 note", "\u81f3\u5c11 1 \u500b\u97f3\u7b26", "\u81f3\u5c11 1 \u4e2a\u97f3\u7b26", "\u81f3\u5c11 1 \u500b\u97f3\u7b26"),
            ["editor_err_name"] = L("Name required", "Name required", "\u9700\u8981\u540d\u7a31", "\u9700\u8981\u540d\u79f0", "\u9700\u8981\u540d\u7a31"),
            ["editor_err_author"] = L("Author required", "Author required", "\u9700\u8981\u4f5c\u8005", "\u9700\u8981\u4f5c\u8005", "\u9700\u8981\u4f5c\u8005"),
            ["editor_saved"] = L("Saved!", "Saved!", "\u5df2\u5132\u5b58\uff01", "\u5df2\u4fdd\u5b58\uff01", "\u5df2\u5132\u5b58\uff01"),
            // Settings
            ["menu_settings"] = L("Settings", "Settings", "\u8a2d\u5b9a", "\u8bbe\u5b9a", "\u8a2d\u5b9a"),
            ["settings_title"] = L("SETTINGS", "SETTINGS", "\u8a2d\u5b9a", "\u8bbe\u5b9a", "\u8a2d\u5b9a"),
            ["master_volume"] = L("Master Volume", "Master Volume", "\u4e3b\u97f3\u91cf", "\u4e3b\u97f3\u91cf", "\u4e3b\u97f3\u91cf"),
            ["music_volume"] = L("Music Volume", "Music Volume", "\u97f3\u6a02\u97f3\u91cf", "\u97f3\u4e50\u97f3\u91cf", "\u97f3\u6a02\u97f3\u91cf"),
            ["sfx_volume"] = L("SFX Volume", "SFX Volume", "\u97f3\u6548\u97f3\u91cf", "\u97f3\u6548\u97f3\u91cf", "\u97f3\u6548\u97f3\u91cf"),
            ["offset_ms"] = L("Offset (ms)", "Offset (ms)", "\u504f\u79fb (ms)", "\u504f\u79fb (ms)", "\u504f\u79fb (ms)"),
            ["key_bindings"] = L("Key Bindings", "Key Bindings", "\u6309\u9375\u7d81\u5b9a", "\u6309\u952e\u7ed1\u5b9a", "\u6309\u9375\u7d81\u5b9a"),
            ["press_key"] = L("Press a key...", "Press a key...", "\u8acb\u6309\u4e0b\u6309\u9375...", "\u8bf7\u6309\u4e0b\u6309\u952e...", "\u8acb\u6309\u4e0b\u6309\u9375..."),
            ["hint_settings"] = L(
                "\u2191\u2193 Select   \u25c0\u25b6 Adjust   Enter Bind   Esc Back",
                "\u2191\u2193 Select   \u25c0\u25b6 Adjust   Enter Bind   Esc Back",
                "\u2191\u2193 \u9078\u64c7   \u25c0\u25b6 \u8abf\u6574   Enter \u7d81\u5b9a   Esc \u8fd4\u56de",
                "\u2191\u2193 \u9009\u62e9   \u25c0\u25b6 \u8c03\u6574   Enter \u7ed1\u5b9a   Esc \u8fd4\u56de",
                "\u2191\u2193 \u9078\u64c7   \u25c0\u25b6 \u8abf\u6574   Enter \u7d81\u5b9a   Esc \u8fd4\u56de"),
            // Achievements
            ["menu_achievements"] = L("Achievements", "Achievements", "\u6210\u5c31", "\u6210\u5c31", "\u6210\u5c31"),
            ["achievements_title"] = L("ACHIEVEMENTS", "ACHIEVEMENTS", "\u6210\u5c31\u7cfb\u7d71", "\u6210\u5c31\u7cfb\u7edf", "\u6210\u5c31\u7cfb\u7d71"),
            ["achievement_unlocked"] = L("Achievement Unlocked!", "Achievement Unlocked!", "\u6210\u5c31\u89e3\u9396\uff01", "\u6210\u5c31\u89e3\u9501\uff01", "\u6210\u5c31\u89e3\u9396\uff01"),
            ["ach_first_play"] = L("First Steps", "First Steps", "\u521d\u6b21\u904a\u73a9", "\u521d\u6b21\u6e38\u73a9", "\u521d\u6b21\u904a\u73a9"),
            ["ach_first_play_desc"] = L("Complete your first play", "Complete your first play", "\u5b8c\u6210\u7b2c\u4e00\u6b21\u904a\u73a9", "\u5b8c\u6210\u7b2c\u4e00\u6b21\u6e38\u73a9", "\u5b8c\u6210\u7b2c\u4e00\u6b21\u904a\u73a9"),
            ["ach_first_fc"] = L("Full Combo!", "Full Combo!", "\u5168\u9023\uff01", "\u5168\u8fde\uff01", "\u5168\u9023\uff01"),
            ["ach_first_fc_desc"] = L("Get a Full Combo on any song", "Get a Full Combo on any song", "\u5728\u4efb\u4f55\u6b4c\u66f2\u4e0a\u53d6\u5f97\u5168\u9023", "\u5728\u4efb\u4f55\u6b4c\u66f2\u4e0a\u53d6\u5f97\u5168\u8fde", "\u5728\u4efb\u4f55\u6b4c\u66f2\u4e0a\u53d6\u5f97\u5168\u9023"),
            ["ach_first_s"] = L("S Rank!", "S Rank!", "S \u8a55\u7d1a\uff01", "S \u8bc4\u7ea7\uff01", "S \u8a55\u7d1a\uff01"),
            ["ach_first_s_desc"] = L("Achieve S or higher", "Achieve S or higher", "\u53d6\u5f97 S \u6216\u4ee5\u4e0a\u8a55\u7d1a", "\u53d6\u5f97 S \u6216\u4ee5\u4e0a\u8bc4\u7ea7", "\u53d6\u5f97 S \u6216\u4ee5\u4e0a\u8a55\u7d1a"),
            ["ach_first_ss"] = L("Perfect Score!", "Perfect Score!", "\u5b8c\u7f8e\u5206\u6578\uff01", "\u5b8c\u7f8e\u5206\u6570\uff01", "\u5b8c\u7f8e\u5206\u6578\uff01"),
            ["ach_first_ss_desc"] = L("Achieve SS grade", "Achieve SS grade", "\u53d6\u5f97 SS \u8a55\u7d1a", "\u53d6\u5f97 SS \u8bc4\u7ea7", "\u53d6\u5f97 SS \u8a55\u7d1a"),
            ["ach_combo_50"] = L("50 Combo!", "50 Combo!", "50 \u9023\u64ca\uff01", "50 \u8fde\u51fb\uff01", "50 \u9023\u64ca\uff01"),
            ["ach_combo_50_desc"] = L("Reach 50 combo", "Reach 50 combo", "\u9054\u5230 50 \u9023\u64ca", "\u8fbe\u5230 50 \u8fde\u51fb", "\u9054\u5230 50 \u9023\u64ca"),
            ["ach_combo_100"] = L("100 Combo!", "100 Combo!", "100 \u9023\u64ca\uff01", "100 \u8fde\u51fb\uff01", "100 \u9023\u64ca\uff01"),
            ["ach_combo_100_desc"] = L("Reach 100 combo", "Reach 100 combo", "\u9054\u5230 100 \u9023\u64ca", "\u8fbe\u5230 100 \u8fde\u51fb", "\u9054\u5230 100 \u9023\u64ca"),
            ["ach_plays_10"] = L("Dedicated", "Dedicated", "\u5c08\u6ce8", "\u4e13\u6ce8", "\u5c08\u6ce8"),
            ["ach_plays_10_desc"] = L("Play 10 times", "Play 10 times", "\u904a\u73a9 10 \u6b21", "\u6e38\u73a9 10 \u6b21", "\u904a\u73a9 10 \u6b21"),
            ["ach_plays_50"] = L("Veteran", "Veteran", "\u8001\u624b", "\u8001\u624b", "\u8001\u624b"),
            ["ach_plays_50_desc"] = L("Play 50 times", "Play 50 times", "\u904a\u73a9 50 \u6b21", "\u6e38\u73a9 50 \u6b21", "\u904a\u73a9 50 \u6b21"),
            ["ach_plays_100"] = L("Rhythm Master", "Rhythm Master", "\u7bc0\u594f\u5927\u5e2b", "\u8282\u594f\u5927\u5e08", "\u7bc0\u594f\u5927\u5e2b"),
            ["ach_plays_100_desc"] = L("Play 100 times", "Play 100 times", "\u904a\u73a9 100 \u6b21", "\u6e38\u73a9 100 \u6b21", "\u904a\u73a9 100 \u6b21"),
            ["ach_all_songs"] = L("Explorer", "Explorer", "\u63a2\u7d22\u8005", "\u63a2\u7d22\u8005", "\u63a2\u7d22\u8005"),
            ["ach_all_songs_desc"] = L("Play every song at least once", "Play every song at least once", "\u6bcf\u9996\u6b4c\u66f2\u81f3\u5c11\u904a\u73a9\u4e00\u6b21", "\u6bcf\u9996\u6b4c\u66f2\u81f3\u5c11\u6e38\u73a9\u4e00\u6b21", "\u6bcf\u9996\u6b4c\u66f2\u81f3\u5c11\u904a\u73a9\u4e00\u6b21"),
            ["ach_grade_all_a"] = L("A Student", "A Student", "A \u5b78\u751f", "A \u5b66\u751f", "A \u5b78\u751f"),
            ["ach_grade_all_a_desc"] = L("Get A or above on all songs", "Get A or above on all songs", "\u6240\u6709\u6b4c\u66f2\u53d6\u5f97 A \u4ee5\u4e0a", "\u6240\u6709\u6b4c\u66f2\u53d6\u5f97 A \u4ee5\u4e0a", "\u6240\u6709\u6b4c\u66f2\u53d6\u5f97 A \u4ee5\u4e0a"),
            ["ach_accuracy_95"] = L("Precision", "Precision", "\u7cbe\u6e96", "\u7cbe\u51c6", "\u7cbe\u6e96"),
            ["ach_accuracy_95_desc"] = L("Achieve 95%+ accuracy", "Achieve 95%+ accuracy", "\u9054\u5230 95% \u4ee5\u4e0a\u6e96\u78ba\u7387", "\u8fbe\u5230 95% \u4ee5\u4e0a\u51c6\u786e\u7387", "\u9054\u5230 95% \u4ee5\u4e0a\u6e96\u78ba\u7387"),
            // Replay
            ["watch_replay"] = L("Watch Replay", "Watch Replay", "\u89c0\u770b\u56de\u653e", "\u89c2\u770b\u56de\u653e", "\u89c2\u770b\u56de\u653e"),
            // Profile
            ["menu_profile"] = L("My Profile", "My Profile", "\u6211\u7684\u6a94\u6848", "\u6211\u7684\u6863\u6848", "\u6211\u7684\u6a94\u6848"),
            ["menu_search"] = L("Search Players", "Search Players", "\u641c\u5c0b\u73a9\u5bb6", "\u641c\u7d22\u73a9\u5bb6", "\u641c\u5c0b\u73a9\u5bb6"),
            ["profile_loading"] = L("Loading profile...", "Loading profile...", "\u8f09\u5165\u6a94\u6848\u4e2d...", "\u52a0\u8f7d\u6863\u6848\u4e2d...", "\u8f09\u5165\u6a94\u6848\u4e2d..."),
            ["profile_not_logged_in"] = L("Please login first", "Please login first", "\u8acb\u5148\u767b\u5165", "\u8bf7\u5148\u767b\u5f55", "\u8acb\u5148\u767b\u5165"),
            ["profile_joined"] = L("Joined", "Joined", "\u52a0\u5165\u65e5\u671f", "\u52a0\u5165\u65e5\u671f", "\u52a0\u5165\u65e5\u671f"),
            ["profile_stats"] = L("STATS", "STATS", "\u7d71\u8a08", "\u7edf\u8ba1", "\u7d71\u8a08"),
            ["profile_maps"] = L("BEST GRADES", "BEST GRADES", "\u6700\u4f73\u6210\u7e3e", "\u6700\u4f73\u6210\u7ee9", "\u6700\u4f73\u6210\u7e3e"),
            ["hint_profile"] = L(
                "F5 Refresh   \u2191\u2193 Scroll   Esc Back",
                "F5 Refresh   \u2191\u2193 Scroll   Esc Back",
                "F5 \u91cd\u65b0\u6574\u7406   \u2191\u2193 \u6372\u52d5   Esc \u8fd4\u56de",
                "F5 \u5237\u65b0   \u2191\u2193 \u6eda\u52a8   Esc \u8fd4\u56de",
                "F5 \u91cd\u65b0\u6574\u7406   \u2191\u2193 \u6372\u52d5   Esc \u8fd4\u56de"),
            ["search_title"] = L("SEARCH PLAYERS", "SEARCH PLAYERS", "\u641c\u5c0b\u73a9\u5bb6", "\u641c\u7d22\u73a9\u5bb6", "\u641c\u5c0b\u73a9\u5bb6"),
            ["search_placeholder"] = L("Type username and press Enter...", "Type username and press Enter...", "\u8f38\u5165\u7528\u6236\u540d\u5f8c\u6309 Enter...", "\u8f93\u5165\u7528\u6237\u540d\u540e\u6309 Enter...", "\u8f38\u5165\u7528\u6236\u540d\u5f8c\u6309 Enter..."),
            ["search_loading"] = L("Searching...", "Searching...", "\u641c\u5c0b\u4e2d...", "\u641c\u7d22\u4e2d...", "\u641c\u5c0b\u4e2d..."),
            ["search_no_results"] = L("No players found", "No players found", "\u627e\u4e0d\u5230\u73a9\u5bb6", "\u627e\u4e0d\u5230\u73a9\u5bb6", "\u627e\u4e0d\u5230\u73a9\u5bb6"),
            ["hint_search"] = L(
                "Enter Search/View   \u2191\u2193 Select   Esc Back",
                "Enter Search/View   \u2191\u2193 Select   Esc Back",
                "Enter \u641c\u5c0b/\u67e5\u770b   \u2191\u2193 \u9078\u64c7   Esc \u8fd4\u56de",
                "Enter \u641c\u7d22/\u67e5\u770b   \u2191\u2193 \u9009\u62e9   Esc \u8fd4\u56de",
                "Enter \u641c\u5c0b/\u67e5\u770b   \u2191\u2193 \u9078\u64c7   Esc \u8fd4\u56de"),
        };

        // Helper to create language dictionaries concisely
        static Dictionary<GameLanguage, string> L(string all)
            => new() { [GameLanguage.English]=all, [GameLanguage.EnglishUS]=all,
                       [GameLanguage.ZhTW]=all, [GameLanguage.ZhCN]=all, [GameLanguage.ZhHK]=all };
        static Dictionary<GameLanguage, string> L(string en, string enUs, string tw, string cn, string hk)
            => new() { [GameLanguage.English]=en, [GameLanguage.EnglishUS]=enUs,
                       [GameLanguage.ZhTW]=tw, [GameLanguage.ZhCN]=cn, [GameLanguage.ZhHK]=hk };

        public static string Get(string key)
        {
            if (_strings.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(_current, out var val)) return val;
                if (dict.TryGetValue(GameLanguage.English, out var fallback)) return fallback;
            }
            return key;
        }
    }
}
