using System;
using System.IO;
using System.Text.Json;

namespace subtitles_maker.Services
{
    public class AppConfig
    {
        public string? ModelPath { get; set; }
        public string? OutputPath { get; set; }
        public string? Language { get; set; }
    }

    public static class ConfigService
    {
        public static readonly string ConfigDir = @"C:\subtitles-maker";
        public static readonly string ConfigPath = Path.Combine(ConfigDir, "subtitles-maker.cfg");
        public static readonly string DefaultOutputDir = Path.Combine(ConfigDir, "output");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Directory.CreateDirectory(ConfigDir);
                    Directory.CreateDirectory(DefaultOutputDir);
                    var cfg = new AppConfig
                    {
                        OutputPath = DefaultOutputDir,
                        Language = "English"
                    };
                    Save(cfg);
                    return cfg;
                }

                var json = File.ReadAllText(ConfigPath);
                var cfgObj = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                // If no output set, ensure default exists and use it
                if (string.IsNullOrWhiteSpace(cfgObj.OutputPath))
                {
                    Directory.CreateDirectory(DefaultOutputDir);
                    cfgObj.OutputPath = DefaultOutputDir;
                }
                // Default language
                if (string.IsNullOrWhiteSpace(cfgObj.Language))
                {
                    cfgObj.Language = "English";
                }
                return cfgObj;
            }
            catch
            {
                try
                {
                    Directory.CreateDirectory(DefaultOutputDir);
                }
                catch { }
                return new AppConfig { OutputPath = DefaultOutputDir, Language = "English" };
            }
        }

        public static void Save(AppConfig cfg)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // swallow
            }
        }
    }
}
