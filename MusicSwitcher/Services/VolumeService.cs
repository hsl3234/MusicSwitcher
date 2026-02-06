using NAudio.CoreAudioApi;

namespace MusicSwitcher.Services;

public record VolumeSessionInfo(string ProcessName, string DisplayName);

public interface IVolumeService
{
    float GetVolume(string? processName);
    void SetVolume(float volume, string? processName);
    IReadOnlyList<VolumeSessionInfo> GetAudioSessions();
}

public class VolumeService : IVolumeService
{
    public float GetVolume(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return 1f;
        var target = processName.Trim();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (int d = 0; d < devices.Count; d++)
            {
                using var device = devices[d];
                var sessionManager = device.AudioSessionManager;
                if (sessionManager?.Sessions == null) continue;
                for (int s = 0; s < sessionManager.Sessions.Count; s++)
                {
                    try
                    {
                        using var control = sessionManager.Sessions[s];
                        if (string.Equals(GetProcessName(control.GetProcessID), target, StringComparison.OrdinalIgnoreCase))
                            return control.SimpleAudioVolume.Volume;
                    }
                    catch { /* skip */ }
                }
            }
        }
        catch { /* ignore */ }
        return 1f;
    }

    public void SetVolume(float volume, string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        volume = Math.Clamp(volume, 0f, 1f);
        var target = processName.Trim();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (int d = 0; d < devices.Count; d++)
            {
                using var device = devices[d];
                var sessionManager = device.AudioSessionManager;
                if (sessionManager?.Sessions == null) continue;
                for (int s = 0; s < sessionManager.Sessions.Count; s++)
                {
                    try
                    {
                        using var control = sessionManager.Sessions[s];
                        if (string.Equals(GetProcessName(control.GetProcessID), target, StringComparison.OrdinalIgnoreCase))
                            control.SimpleAudioVolume.Volume = volume;
                    }
                    catch { /* skip */ }
                }
            }
        }
        catch { /* ignore */ }
    }

    public IReadOnlyList<VolumeSessionInfo> GetAudioSessions()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<VolumeSessionInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var role in new[] { Role.Multimedia, Role.Console })
            {
                try
                {
                    using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
                    CollectSessionsFromDevice(defaultDevice, seen, list);
                    if (list.Count > 0) break;
                }
                catch { /* нет устройства по умолчанию или ошибка */ }
            }
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (int d = 0; d < devices.Count; d++)
            {
                using var device = devices[d];
                CollectSessionsFromDevice(device, seen, list);
            }
        }
        catch { /* ignore */ }
        return list.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectSessionsFromDevice(MMDevice device, HashSet<string> seen, List<VolumeSessionInfo> list)
    {
        try
        {
            var sessionManager = device.AudioSessionManager;
            if (sessionManager?.Sessions == null) return;
            int count = sessionManager.Sessions.Count;
            for (int s = 0; s < count; s++)
            {
                try
                {
                    using var control = sessionManager.Sessions[s];
                    uint pid = control.GetProcessID;
                    if (pid == 0) continue;
                    string processName = GetProcessName(pid);
                    if (string.IsNullOrEmpty(processName)) continue;
                    if (seen.Add(processName))
                    {
                        var sessionDisplay = control.DisplayName;
                        var display = string.IsNullOrWhiteSpace(sessionDisplay) ? processName : sessionDisplay;
                        list.Add(new VolumeSessionInfo(processName, display));
                    }
                }
                catch { /* skip session */ }
            }
        }
        catch { /* skip device */ }
    }

    private static string GetProcessName(uint pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName ?? "";
        }
        catch { return ""; }
    }
}
