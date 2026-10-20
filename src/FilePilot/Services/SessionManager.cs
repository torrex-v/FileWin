using System;
using System.IO;
using System.Text.Json;
using FilePilot.Models;

namespace FilePilot.Services;

public class SessionManager
{
    private static readonly string VoidstarDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Voidstar",
        "FilePilot");

    private static readonly string SessionFilePath = Path.Combine(VoidstarDir, "FPilot-Session.json");

    public static SessionFileModel LoadSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
            {
                string json = File.ReadAllText(SessionFilePath);
                var model = JsonSerializer.Deserialize<SessionFileModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (model != null) return model;
            }
        }
        catch { }

        // Default session
        return new SessionFileModel();
    }

    public static void SaveSession(SessionFileModel model)
    {
        try
        {
            if (!Directory.Exists(VoidstarDir))
            {
                Directory.CreateDirectory(VoidstarDir);
            }

            string json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Write atomic using .tmp
            string tmpPath = SessionFilePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, SessionFilePath, true);
        }
        catch { }
    }
}
