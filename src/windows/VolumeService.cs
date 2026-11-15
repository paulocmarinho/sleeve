using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace Sleeve;

/// <summary>
/// Volume só de um app (o mesmo controle do Mixer de volume do Windows),
/// sem mexer no volume geral do PC. Usado para o Spotify.
/// </summary>
public sealed class VolumeService
{
    private readonly MMDeviceEnumerator _enumerator = new();

    /// <summary>Volume atual do app (0 a 1), ou null se ele não estiver tocando som.</summary>
    public float? Get(string processName)
    {
        float? result = null;
        ForEachSession(processName, v => result ??= v.Mute ? 0f : v.Volume);
        return result;
    }

    /// <summary>Define o volume do app (0 a 1). Devolve false se não achou o app.</summary>
    public bool Set(string processName, float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        bool found = false;
        ForEachSession(processName, v =>
        {
            v.Volume = value;
            if (value > 0 && v.Mute) v.Mute = false;
            found = true;
        });
        return found;
    }

    private void ForEachSession(string processName, Action<SimpleAudioVolume> action)
    {
        try
        {
            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (BelongsTo(session, processName)) action(session.SimpleAudioVolume);
            }
        }
        catch
        {
            // sem dispositivo de áudio ou sessão encerrada no meio
        }
    }

    private static bool BelongsTo(AudioSessionControl session, string processName)
    {
        try
        {
            var pid = (int)session.GetProcessID;
            if (pid == 0) return false;
            using var process = Process.GetProcessById(pid);
            return process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
