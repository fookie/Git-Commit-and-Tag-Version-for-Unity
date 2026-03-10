using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CommitAndTagVersion.Editor
{
    public class CommitAndTagWindow : EditorWindow
    {
        private string _latestTag;
        private SemanticVersion _currentVersion;
        private readonly List<CommitMessage> _commitsSinceLastTag = new List<CommitMessage>();
        private ReleaseType _suggestedReleaseType = ReleaseType.Patch;
        private string _prereleaseId = "";
        private bool _generateDeveloperVersion;
        private string _developerVersionStr = "";

        private Vector2 _scrollPos;

        [MenuItem("Window/Versioning/Commit and Tag Version")]
        public static void ShowWindow()
        {
            var window = GetWindow<CommitAndTagWindow>("Commit and Tag");
            window.minSize = new Vector2(400, 300);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            _latestTag = GitUtility.GetLatestTag("v");

            if (string.IsNullOrEmpty(_latestTag))
            {
                // Fallback to 0.0.0 if no tags found
                _currentVersion = new SemanticVersion(0, 0, 0);
            }
            else
            {
                if (SemanticVersion.TryParse(_latestTag, out var sv))
                {
                    _currentVersion = sv;
                }
                else
                {
                    Debug.LogWarning($"[CommitAndTagVersion] Could not parse latest tag '{_latestTag}' as SemVer.");
                    _currentVersion = new SemanticVersion(0, 0, 0);
                }
            }

            string[] rawCommits = GitUtility.GetCommits(_latestTag);
            _commitsSinceLastTag.Clear();

            foreach (string rawCommit in rawCommits)
            {
                if (!string.IsNullOrWhiteSpace(rawCommit))
                {
                    _commitsSinceLastTag.Add(ConventionalCommitParser.Parse(rawCommit));
                }
            }

            _suggestedReleaseType = ConventionalCommitParser.AnalyzeCommits(_commitsSinceLastTag);

            // Compute potential Developer Version
            string baseTarget = _currentVersion?.Increment(_suggestedReleaseType).ToString() ?? "0.0.1";
            string branchName = GitUtility.GetBranchName().Replace("/", "-").Replace("_", "-");
            string shortHash = GitUtility.GetShortCommitHash();
            int commitCount = GitUtility.GetCommitsCountSinceTag(_latestTag);

            _developerVersionStr = $"{baseTarget}-{branchName}.{commitCount}+{shortHash}";
        }

        private void OnGUI()
        {
            GUILayout.Label("Version Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Latest Tag:", string.IsNullOrEmpty(_latestTag) ? "None" : _latestTag);
            EditorGUILayout.LabelField("Current Version:", _currentVersion?.ToString() ?? "Unknown");

            string nextVersionStr = _currentVersion?.Increment(_suggestedReleaseType).ToString();
            if (!string.IsNullOrEmpty(_prereleaseId))
            {
                nextVersionStr += $"-{_prereleaseId}.0"; // Simplistic prerelease handling
            }

            EditorGUILayout.LabelField("Suggested Release Type:", _suggestedReleaseType.ToString());

            GUILayout.Space(10);

            _generateDeveloperVersion = EditorGUILayout.ToggleLeft("Generate Internal Developer Version", _generateDeveloperVersion);

            string targetVersion = _generateDeveloperVersion ? _developerVersionStr : nextVersionStr;

            EditorGUILayout.LabelField("Target Version:", targetVersion ?? "Unknown", EditorStyles.boldLabel);

            GUILayout.Space(10);

            if (!_generateDeveloperVersion)
            {
                _prereleaseId = EditorGUILayout.TextField("Prerelease ID (Optional):", _prereleaseId);
                GUILayout.Space(10);
            }

            GUILayout.Label($"Commits since {_latestTag} ({_commitsSinceLastTag.Count}):", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
            foreach (var commit in _commitsSinceLastTag)
            {
                string icon = commit.IsBreakingChange ? "💥" : (commit.Type == "feat" ? "✨" : (commit.Type == "fix" ? "🐛" : "🔧"));
                EditorGUILayout.LabelField($"{icon} {commit.RawSubject}");
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(_commitsSinceLastTag.Count == 0 && string.IsNullOrEmpty(_prereleaseId) && !_generateDeveloperVersion);
            if (GUILayout.Button("Release (Bump, Changelog, Commit & Tag)", GUILayout.Height(40)))
            {
                ExecuteRelease(targetVersion);
            }

            if (GUILayout.Button("Release with Build (Bump, Build, Commit & Tag)", GUILayout.Height(40)))
            {
                ExecuteReleaseWithBuild(targetVersion);
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Refresh"))
            {
                Refresh();
            }
        }

        private void ExecuteRelease(string targetVersion)
        {
            try
            {
                // 1. Update Configs
                FileUpdater.UpdatePlayerSettings(targetVersion);

                string packageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "package.json");
                FileUpdater.UpdatePackageJson(packageJsonPath, targetVersion);

                // 2. Generate Changelog
                string changelogPath = Path.Combine(Directory.GetCurrentDirectory(), "CHANGELOG.md");
                ChangelogBuilder.GenerateChangelog(changelogPath, targetVersion, _commitsSinceLastTag);

                // 3. Git Add, Commit, Tag
                GitUtility.Add(".");
                string message = $"chore(release): v{targetVersion}";
                GitUtility.Commit(message);
                GitUtility.Tag($"v{targetVersion}", message);

                Debug.Log($"[CommitAndTagVersion] Successfully released v{targetVersion}");

                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommitAndTagVersion] Failed to release: {ex.Message}");
            }
        }

        private const string BuildPathPrefKey = "CommitAndTagVersion_LastBuildPath";

        private void ExecuteReleaseWithBuild(string targetVersion)
        {
            try
            {
                // 1. Update Configs
                FileUpdater.UpdatePlayerSettings(targetVersion);

                string packageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "package.json");
                FileUpdater.UpdatePackageJson(packageJsonPath, targetVersion);

                // 2. Generate Changelog
                string changelogPath = Path.Combine(Directory.GetCurrentDirectory(), "CHANGELOG.md");
                ChangelogBuilder.GenerateChangelog(changelogPath, targetVersion, _commitsSinceLastTag);

                // 3. Prompt user to select build output path (remember last used path)
                BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
                string lastBuildPath = EditorPrefs.GetString(BuildPathPrefKey, "");
                string buildPath = EditorUtility.SaveFolderPanel(
                    "Select Build Output Folder", lastBuildPath, "");

                if (string.IsNullOrEmpty(buildPath))
                {
                    Debug.LogWarning("[CommitAndTagVersion] Build cancelled by user. Version changes are kept but not committed.");
                    return;
                }

                // Save the selected path for next time
                EditorPrefs.SetString(BuildPathPrefKey, buildPath);

                // 4. Gather scenes from Build Settings
                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    Debug.LogError("[CommitAndTagVersion] No scenes enabled in Build Settings. Version changes are kept but not committed.");
                    return;
                }

                // Determine the output file path based on target platform
                string executableName = Application.productName;
                switch (activeBuildTarget)
                {
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        executableName += ".exe";
                        break;
                    case BuildTarget.StandaloneOSX:
                        executableName += ".app";
                        break;
                    case BuildTarget.Android:
                        executableName += ".apk";
                        break;
                }

                string fullBuildPath = Path.Combine(buildPath, executableName);

                // 5. Execute Build
                Debug.Log($"[CommitAndTagVersion] Starting build for {activeBuildTarget} at: {fullBuildPath}");

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = fullBuildPath,
                    target = activeBuildTarget,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

                if (report.summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[CommitAndTagVersion] Build succeeded! Size: {report.summary.totalSize} bytes, Time: {report.summary.totalTime}");

                    // 6. Git Add, Commit, Tag
                    GitUtility.Add(".");
                    string message = $"chore(release): v{targetVersion}";
                    GitUtility.Commit(message);
                    GitUtility.Tag($"v{targetVersion}", message);

                    Debug.Log($"[CommitAndTagVersion] Successfully released v{targetVersion} with build.");

                    // 7. Open build output folder in OS file explorer
                    EditorUtility.RevealInFinder(fullBuildPath);

                    EditorUtility.DisplayDialog("Release with Build",
                        $"Build succeeded!\nVersion v{targetVersion} has been committed and tagged.\n\nOutput: {fullBuildPath}",
                        "OK");

                    Refresh();
                }
                else
                {
                    Debug.LogError($"[CommitAndTagVersion] Build failed with result: {report.summary.result}. Version changes are kept but not committed.");
                    EditorUtility.DisplayDialog("Release with Build",
                        $"Build failed ({report.summary.result}).\nVersion changes are kept but NOT committed.\nPlease fix the build issues and try again.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommitAndTagVersion] Failed to release with build: {ex.Message}");
            }
        }
    }
}
