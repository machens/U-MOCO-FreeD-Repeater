using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace U_MOCO_FreeD_Repeater
{
    [XmlRoot("AppConfig")]
    public class AppConfig
    {
        public int ReceivePort { get; set; } = 6301;

        [XmlArray("ForwardTargets")]
        [XmlArrayItem("Target")]
        public List<ForwardTargetConfig> ForwardTargets { get; set; } = new();
    }

    public class ForwardTargetConfig
    {
        [XmlAttribute]
        public string IP { get; set; } = "";

        [XmlAttribute]
        public string Port { get; set; } = "";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigDir =
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "U-MOCO FreeD Repeater");

        private static readonly string ConfigFile =
            Path.Combine(ConfigDir, "config.xml");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var ser = new XmlSerializer(typeof(AppConfig));
                    using var fs = File.OpenRead(ConfigFile);
                    return (AppConfig)ser.Deserialize(fs)!;
                }
            }
            catch { /* 配置损坏时忽略，使用默认值 */ }

            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var ser = new XmlSerializer(typeof(AppConfig));
                using var fs = File.Create(ConfigFile);
                ser.Serialize(fs, config);
            }
            catch { /* 保存失败时忽略 */ }
        }
    }
}