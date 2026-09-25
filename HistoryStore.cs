using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DarPing
{
    public sealed class HistoryStore
    {
        private readonly string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DarPing", "history.log");
        public List<MonitorReading> Load()
        {
            var result = new List<MonitorReading>();
            try
            {
                if (!File.Exists(file)) return result;
                foreach (var line in File.ReadAllLines(file))
                {
                    var p = line.Split('|');
                    if (p.Length != 5) continue;
                    DateTime time; long down, up, ping; ConnectionState state;
                    if (DateTime.TryParse(p[0], null, DateTimeStyles.RoundtripKind, out time) && long.TryParse(p[1], out down) && long.TryParse(p[2], out up) && long.TryParse(p[3], out ping) && Enum.TryParse(p[4], out state))
                        result.Add(new MonitorReading { Time = time, Download = down, Upload = up, PingMs = ping, State = state });
                }
            }
            catch { }
            return result;
        }
        public void Add(MonitorReading reading)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.AppendAllText(file, string.Format(CultureInfo.InvariantCulture, "{0:o}|{1}|{2}|{3}|{4}{5}", reading.Time, reading.Download, reading.Upload, reading.PingMs, reading.State, Environment.NewLine)); }
            catch { }
        }

        public void Clear()
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch { }
        }
    }
}