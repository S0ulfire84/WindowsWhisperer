using System;
using System.IO;
using System.Text.Json;

namespace WindowsWhispererWidget
{
    public class ConfigurationManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsWhisperer"
        );
        private static readonly string ConfigFilePath = Path.Combine(AppDataPath, "appsettings.json");
        private static readonly string TemplateConfigPath = "appsettings.template.json";

        public static string GetApiKey()
        {
            EnsureConfigFileExists();
            var config = JsonSerializer.Deserialize<ConfigurationRoot>(File.ReadAllText(ConfigFilePath));
            return config?.OpenAI?.ApiKey ?? string.Empty;
        }

        public static void SaveApiKey(string apiKey)
        {
            EnsureConfigFileExists();
            var config = JsonSerializer.Deserialize<ConfigurationRoot>(File.ReadAllText(ConfigFilePath));
            if (config == null)
            {
                config = new ConfigurationRoot { OpenAI = new OpenAIConfig() };
            }
            config.OpenAI.ApiKey = apiKey;
            
            Directory.CreateDirectory(AppDataPath);
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            
            Console.WriteLine($"API key saved to: {ConfigFilePath}");
        }

        private static void EnsureConfigFileExists()
        {
            if (!File.Exists(ConfigFilePath))
            {
                Directory.CreateDirectory(AppDataPath);
                if (File.Exists(TemplateConfigPath))
                {
                    File.Copy(TemplateConfigPath, ConfigFilePath);
                }
                else
                {
                    var defaultConfig = new ConfigurationRoot { OpenAI = new OpenAIConfig { ApiKey = string.Empty } };
                    File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }

        private class ConfigurationRoot
        {
            public OpenAIConfig OpenAI { get; set; }
        }

        private class OpenAIConfig
        {
            public string ApiKey { get; set; }
        }
    }
} 