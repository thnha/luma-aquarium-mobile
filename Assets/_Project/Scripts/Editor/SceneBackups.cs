using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Scene copies the builders make before they change a scene. They live outside Assets (mobile/Backups/Scenes) so
// Unity doesn't import them, and only the newest Keep files are kept (each builder run adds one or two).
public static class SceneBackups
{
    public const string Dir = "Backups/Scenes";
    const int Keep = 20;

    public static string Stamp() => DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

    // The scene file as saved on disk.
    public static void CopyFile(string scenePath, string name)
    {
        Directory.CreateDirectory(Dir);
        File.Copy(scenePath, Dir + "/" + name + ".unity");
        Prune();
    }

    // The open scene, unsaved changes included.
    public static void SaveCopy(Scene scene, string name)
    {
        Directory.CreateDirectory(Dir);
        EditorSceneManager.SaveScene(scene, Dir + "/" + name + ".unity", true);
        Prune();
    }

    static void Prune()
    {
        foreach (var old in new DirectoryInfo(Dir).GetFiles("*.unity").OrderByDescending(f => f.LastWriteTimeUtc).Skip(Keep))
        {
            old.Delete();
            File.Delete(old.FullName + ".meta");
        }
    }
}
