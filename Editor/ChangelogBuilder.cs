using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace CommitAndTagVersion.Editor
{
    public static class ChangelogBuilder
    {
        public static void GenerateChangelog(string path, string version, IEnumerable<CommitMessage> commits)
        {
            var sb = new StringBuilder();
            
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            sb.AppendLine($"## [{version}] - {date}");
            sb.AppendLine();

            var breakingChanges = commits.Where(c => c.IsBreakingChange).ToList();
            var features = commits.Where(c => c.Type == "feat" && !c.IsBreakingChange).ToList();
            var fixes = commits.Where(c => c.Type == "fix" && !c.IsBreakingChange).ToList();

            if (breakingChanges.Any())
            {
                sb.AppendLine("### ⚠ BREAKING CHANGES");
                sb.AppendLine();
                foreach (var commit in breakingChanges)
                {
                    sb.AppendLine($"- {FormatCommit(commit.BreakingChangeDescription ?? commit.Description, commit)}");
                }
                sb.AppendLine();
            }

            if (features.Any())
            {
                sb.AppendLine("### Features");
                sb.AppendLine();
                foreach (var commit in features)
                {
                    sb.AppendLine($"- {FormatCommit(commit.Description, commit)}");
                }
                sb.AppendLine();
            }

            if (fixes.Any())
            {
                sb.AppendLine("### Bug Fixes");
                sb.AppendLine();
                foreach (var commit in fixes)
                {
                    sb.AppendLine($"- {FormatCommit(commit.Description, commit)}");
                }
                sb.AppendLine();
            }

            // Create file if it doesn't exist
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "# Changelog\n\n");
            }

            // Prepend new changelog content after the main header
            string existingContent = File.ReadAllText(path);
            
            // Try to find insertion point after # Changelog
            int insertionIndex = existingContent.IndexOf("# Changelog");
            if (insertionIndex != -1)
            {
                int nextLineIndex = existingContent.IndexOf('\n', insertionIndex);
                if (nextLineIndex != -1)
                {
                    existingContent = existingContent.Insert(nextLineIndex + 1, "\n" + sb.ToString());
                }
                else
                {
                    existingContent += "\n" + sb.ToString();
                }
            }
            else
            {
                existingContent = sb.ToString() + existingContent;
            }

            File.WriteAllText(path, existingContent);
            AssetDatabase.Refresh();
        }

        private static string FormatCommit(string text, CommitMessage commit)
        {
            if (string.IsNullOrEmpty(commit.Scope))
            {
                return text;
            }
            return $"**{commit.Scope}:** {text}";
        }
    }
}
