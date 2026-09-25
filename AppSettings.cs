using System;
using System.Drawing;
using System.IO;
using System.Xml.Serialization;

namespace DarPing
{
    public class AppSettings
    {
        public string Host { get; set; }
        public bool PlaySound { get; set; }
        public int SlowPingMs { get; set; }
        public int SoundProfile { get; set; }
        public int SoundProfileOnline { get; set; }
        public int SoundProfileSlow { get; set; }
        public int SoundProfileOffline { get; set; }
        public bool SoundOnOnline { get; set; }
        public bool SoundOnSlow { get; set; }
        public bool SoundOnOffline { get; set; }
        public int TaskbarTextColorArgb { get; set; }
        public bool StartWithWindows { get; set; }

        public static AppSettings Load()
        {
            var settings = CreateDefaults();
            try
            {
                var file = Path.Combine(DataFolder, "settings.xml");
                if (File.Exists(file))
                {
                    using (var reader = File.OpenText(file))
                        settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(reader);
                }
            }
            catch { }
            if (settings == null || string.IsNullOrWhiteSpace(settings.Host)) settings = CreateDefaults();
            if (settings.SoundProfile < 0 || settings.SoundProfile > 9) settings.SoundProfile = 0;
            if (settings.SoundProfileOnline < 0 || settings.SoundProfileOnline > 9) settings.SoundProfileOnline = settings.SoundProfile;
            if (settings.SoundProfileSlow < 0 || settings.SoundProfileSlow > 9) settings.SoundProfileSlow = settings.SoundProfile;
            if (settings.SoundProfileOffline < 0 || settings.SoundProfileOffline > 9) settings.SoundProfileOffline = settings.SoundProfile;
            if (settings.PlaySound && !settings.SoundOnOnline && !settings.SoundOnSlow && !settings.SoundOnOffline) settings.SoundOnOnline = true;
            if (settings.TaskbarTextColorArgb == 0) settings.TaskbarTextColorArgb = Color.White.ToArgb();
            return settings;
        }

        private static AppSettings CreateDefaults()
        {
            return new AppSettings { Host = "8.8.8.8", PlaySound = false, SlowPingMs = 180, SoundProfile = 0, SoundProfileOnline = 0, SoundProfileSlow = 1, SoundProfileOffline = 2, SoundOnOnline = false, SoundOnSlow = false, SoundOnOffline = false, TaskbarTextColorArgb = Color.White.ToArgb() };
        }

        public void Save()
        {
            Directory.CreateDirectory(DataFolder);
            var file = Path.Combine(DataFolder, "settings.xml");
            using (var writer = File.CreateText(file))
                new XmlSerializer(typeof(AppSettings)).Serialize(writer, this);
        }

        private static string DataFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DarPing"); } }
    }
}