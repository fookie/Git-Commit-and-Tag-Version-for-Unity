using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CommitAndTagVersion.Editor
{
    public static class FileUpdater
    {
        public static void UpdatePlayerSettings(string newVersion)
        {
            PlayerSettings.bundleVersion = newVersion;

            // Increment Android Version Code
            PlayerSettings.Android.bundleVersionCode++;

            // Increment iOS Build Number (assuming it's formatted as a number)
            PlayerSettings.iOS.buildNumber = int.TryParse(PlayerSettings.iOS.buildNumber, out int iosBuildNum) ? (iosBuildNum + 1).ToString() :
              // Fallback if not pure integer
              newVersion;

            // Save the settings
            AssetDatabase.SaveAssets();
            Debug.Log($"[CommitAndTagVersion] Updated PlayerSettings to version {newVersion}");
        }

        public static void UpdatePackageJson(string packageJsonPath, string newVersion)
        {
            if (!File.Exists(packageJsonPath))
            {
                Debug.LogWarning($"[CommitAndTagVersion] No package.json found at {packageJsonPath}, skipping.");
                return;
            }

            string content = File.ReadAllText(packageJsonPath);
            // Replace "version": "..." with "version": "newVersion"
            var regex = new Regex(@"(\""version\""\s*:\s*\"")[^\""]+(\"")");
            string newContent = regex.Replace(content, $"${{1}}{newVersion}$2");

            File.WriteAllText(packageJsonPath, newContent);
            Debug.Log($"[CommitAndTagVersion] Updated {packageJsonPath} to version {newVersion}");
        }
    }
}
