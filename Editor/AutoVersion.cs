using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CommitAndTagVersion.Editor
{
    public static class AutoVersion
    {
        /// <summary>
        /// Entry point for CI/CD batch mode execution.
        /// E.g. Unity -quit -batchmode -executeMethod CommitAndTagVersion.Editor.AutoVersion.Run
        /// </summary>
        public static void Run()
        {
            try
            {
                Debug.Log("[CommitAndTagVersion] Starting headless auto-versioning...");

                string latestTag = GitUtility.GetLatestTag("v");
                SemanticVersion currentVersion;

                if (string.IsNullOrEmpty(latestTag))
                {
                    currentVersion = new SemanticVersion(0, 0, 0);
                    Debug.Log("[CommitAndTagVersion] No existing tag found. Starting from 0.0.0.");
                }
                else
                {
                    if (!SemanticVersion.TryParse(latestTag, out currentVersion))
                    {
                        Debug.LogError($"[CommitAndTagVersion] Failed to parse tag {latestTag}. Aborting.");
                        EditorApplication.Exit(1);
                        return;
                    }
                }

                string[] rawCommits = GitUtility.GetCommits(latestTag);
                var commits = new List<CommitMessage>();
                foreach (var rc in rawCommits)
                {
                    if (!string.IsNullOrWhiteSpace(rc))
                    {
                        commits.Add(ConventionalCommitParser.Parse(rc));
                    }
                }

                if (commits.Count == 0)
                {
                    Debug.Log("[CommitAndTagVersion] No new commits since last release. Aborting auto-versioning.");
                    EditorApplication.Exit(0);
                    return;
                }

                ReleaseType suggestedType = ConventionalCommitParser.AnalyzeCommits(commits);
                string prereleaseId = Environment.GetEnvironmentVariable("COMMIT_TAG_PRERELEASE") ?? "";
                
                string nextVersionStr;
                string useDeveloperVersion = Environment.GetEnvironmentVariable("COMMIT_TAG_DEVELOPER_VERSION") ?? "false";
                
                if (useDeveloperVersion.ToLower() == "true" || useDeveloperVersion == "1")
                {
                    string baseTarget = currentVersion.Increment(suggestedType).ToString();
                    string branchName = GitUtility.GetBranchName().Replace("/", "-").Replace("_", "-");
                    string shortHash = GitUtility.GetShortCommitHash();
                    int commitCount = GitUtility.GetCommitsCountSinceTag(latestTag);
                    
                    nextVersionStr = $"{baseTarget}-{branchName}.{commitCount}+{shortHash}";
                }
                else
                {
                    SemanticVersion nextVersion = currentVersion.Increment(suggestedType);
                    nextVersionStr = nextVersion.ToString();
                    
                    if (!string.IsNullOrEmpty(prereleaseId))
                    {
                        nextVersionStr += $"-{prereleaseId}.0";
                    }
                }

                Debug.Log($"[CommitAndTagVersion] Suggested bump: {suggestedType}. New version: {nextVersionStr}");

                // 1. Update Configs
                FileUpdater.UpdatePlayerSettings(nextVersionStr);
                string packageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "package.json");
                FileUpdater.UpdatePackageJson(packageJsonPath, nextVersionStr);
                
                // 2. Generate Changelog
                string changelogPath = Path.Combine(Directory.GetCurrentDirectory(), "CHANGELOG.md");
                ChangelogBuilder.GenerateChangelog(changelogPath, nextVersionStr, commits);

                // 3. Git Add, Commit, Tag
                GitUtility.Add(".");
                string message = $"chore(release): v{nextVersionStr}";
                GitUtility.Commit(message);
                GitUtility.Tag($"v{nextVersionStr}", message);

                Debug.Log($"[CommitAndTagVersion] Auto-versioning successful. Tag: v{nextVersionStr}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommitAndTagVersion] Exception during auto-versioning: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
            }
        }
    }
}
