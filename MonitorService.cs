using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace DarPing
{
    public enum ConnectionState { Offline, Slow, Online }

    public sealed class MonitorReading
    {
        public DateTime Time { get; set; }
        public long Download { get; set; }
        public long Upload { get; set; }
        public long PingMs { get; set; }
        public ConnectionState State { get; set; }
    }

    public sealed class MonitorService
    {
        private static readonly int[] SoundFrequencies = { 620, 700, 780, 860, 940, 1020, 1100, 1180, 1260, 1340 };
        private static readonly int[] SoundDurations = { 35, 35, 35, 40, 40, 40, 45, 45, 50, 55 };
        private long lastReceived;
        private long lastSent;
        private bool firstSample = true;
        private readonly AppSettings settings;

        public MonitorService(AppSettings settings) { this.settings = settings; }

        public async Task<MonitorReading> ReadAsync()
        {
            var stats = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => n.GetIPv4Statistics())
                .ToArray();
            var received = stats.Sum(s => (long)s.BytesReceived);
            var sent = stats.Sum(s => (long)s.BytesSent);
            var down = firstSample ? 0 : Math.Max(0, received - lastReceived);
            var up = firstSample ? 0 : Math.Max(0, sent - lastSent);
            firstSample = false;
            lastReceived = received;
            lastSent = sent;

            var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            long pingMs = -1;
            try
            {
                var reply = await Task.Factory.StartNew(() => ping.Send(settings.Host, 1000)).ConfigureAwait(true);
                stopwatch.Stop();
                if (reply.Status == IPStatus.Success) pingMs = reply.RoundtripTime > 0 ? reply.RoundtripTime : stopwatch.ElapsedMilliseconds;
            }
            catch { }
            var state = pingMs < 0 ? ConnectionState.Offline : pingMs > settings.SlowPingMs ? ConnectionState.Slow : ConnectionState.Online;
            if (ShouldPlay(state)) PlaySound(GetSoundProfile(state));
            return new MonitorReading { Time = DateTime.Now, Download = down, Upload = up, PingMs = pingMs, State = state };
        }

        private bool ShouldPlay(ConnectionState state)
        {
            if (!IsWithinSoundSchedule(DateTime.Now.TimeOfDay)) return false;
            return (state == ConnectionState.Online && settings.SoundOnOnline) || (state == ConnectionState.Slow && settings.SoundOnSlow) || (state == ConnectionState.Offline && settings.SoundOnOffline);
        }

        private bool IsWithinSoundSchedule(TimeSpan time)
        {
            if (!settings.SoundScheduleEnabled) return true;
            var currentMinute = (int)time.TotalMinutes;
            var start = settings.SoundStartMinute;
            var end = settings.SoundEndMinute;
            if (start == end) return true;
            return start < end ? currentMinute >= start && currentMinute < end : currentMinute >= start || currentMinute < end;
        }

        private int GetSoundProfile(ConnectionState state)
        {
            if (state == ConnectionState.Online) return settings.SoundProfileOnline;
            if (state == ConnectionState.Slow) return settings.SoundProfileSlow;
            return settings.SoundProfileOffline;
        }

        public static void PlaySound(int profile)
        {
            var index = Math.Max(0, Math.Min(9, profile));
            ThreadPool.QueueUserWorkItem(delegate { try { Console.Beep(SoundFrequencies[index], SoundDurations[index]); } catch { } });
        }

        public static string[] SoundNames { get { return new[] { "Beep 1 - grave", "Beep 2", "Beep 3", "Beep 4", "Beep 5", "Beep 6", "Beep 7", "Beep 8", "Beep 9", "Beep 10 - agudo" }; } }
    }
}