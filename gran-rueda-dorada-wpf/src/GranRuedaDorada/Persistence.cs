using System;
using System.IO;
using System.Text.Json;

namespace GranRuedaDorada
{
    /// <summary>Keeps the demo credit meter and bet between sessions, in %APPDATA%\GranRuedaDorada.</summary>
    public sealed class SavedState
    {
        public long Credits { get; set; } = 1000;
        public int Bet { get; set; } = 3;

        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GranRuedaDorada", "state.json");

        public static SavedState Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(FilePath));
                    if (s != null && s.Credits >= 0 && s.Bet is >= 1 and <= 3) return s;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // A missing or corrupt save just starts a fresh game.
            }
            return new SavedState();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Saving is best effort.
            }
        }
    }
}
