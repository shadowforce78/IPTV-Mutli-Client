using System.Text.Json;

namespace IptvClient.Core.Configuration;

public static class AppSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads settings from <paramref name="path"/>, or returns defaults if the file is
    /// absent. Desktop-only — there's no equivalent "next to the exe" file location on Android,
    /// which uses <see cref="Load(Stream)"/> against a bundled asset instead.</summary>
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>Per-user settings file (playlist URL typed in the UI) in the platform's local
    /// app-data folder — works on desktop and on Android (app-private storage), unlike a
    /// "next to the exe" file.</summary>
    public static string UserSettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IptvClient",
        "settings.json");

    public static void Save(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    /// <summary>Reads settings from an already-open stream (e.g. an Android asset).</summary>
    public static AppSettings Load(Stream stream) =>
        JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
}
