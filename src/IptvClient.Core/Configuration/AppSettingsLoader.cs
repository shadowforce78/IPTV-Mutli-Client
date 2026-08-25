using System.Text.Json;

namespace IptvClient.Core.Configuration;

public static class AppSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads settings from <paramref name="path"/>, or returns defaults if the file is absent.</summary>
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
    }
}
