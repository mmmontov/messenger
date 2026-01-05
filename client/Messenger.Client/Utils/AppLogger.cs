using System;
using System.IO;

namespace Messenger.Client.Utils;

public static class AppLogger
{
    private static readonly object LockObj = new();

    private static string LogPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Messenger.Client"
            );
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "client.log");
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(Exception ex, string context) =>
        Write("ERROR", $"{context}\n{ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] {message}\n";
        lock (LockObj)
        {
            File.AppendAllText(LogPath, line);
        }
    }
}


