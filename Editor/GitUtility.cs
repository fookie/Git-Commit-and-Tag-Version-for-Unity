using System;
using System.Diagnostics;
using System.Text;

namespace CommitAndTagVersion.Editor
{
    public static class GitUtility
    {
        /// <summary>
        /// Executes a git command synchronously and returns standard output and standard error.
        /// </summary>
        public static bool ExecuteGitCommand(string arguments, out string output, out string error)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    output = null;
                    error = "Failed to start git process.";
                    return false;
                }

                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                output = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Gets the latest tag from the repository. Falls back to an empty string if no tags exist.
        /// </summary>
        public static string GetLatestTag(string prefix = "v")
        {
            if (ExecuteGitCommand($"describe --tags --abbrev=0 --match \"{prefix}*\"", out string output, out _))
            {
                return output.Trim();
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets all commits from the specified tag to HEAD. If fromTag is empty, gets all commits.
        /// Format is "%s%n%b%n---COMMIT_END---" to easily split commit message subject and body.
        /// </summary>
        public static string[] GetCommits(string fromTag, string toTag = "HEAD")
        {
            string range = string.IsNullOrEmpty(fromTag) ? toTag : $"{fromTag}..{toTag}";
            const string format = "%s%n%b%n---COMMIT_END---";

            if (ExecuteGitCommand($"log --format=\"{format}\" {range}", out string output, out _))
            {
                return output.Split(new[] { "---COMMIT_END---\n", "---COMMIT_END---\r\n", "---COMMIT_END---" }, StringSplitOptions.RemoveEmptyEntries);
            }
            return Array.Empty<string>();
        }

        /// <summary>
        /// Adds a file or all files to git staging.
        /// </summary>
        public static bool Add(string path = ".")
        {
            return ExecuteGitCommand($"add \"{path}\"", out _, out string error);
        }

        /// <summary>
        /// Commits changes with the specified message.
        /// </summary>
        public static bool Commit(string message)
        {
            // Escape double quotes in message
            message = message.Replace("\"", "\\\"");
            return ExecuteGitCommand($"commit -m \"{message}\"", out _, out string error);
        }

        /// <summary>
        /// Creates an annotated tag.
        /// </summary>
        public static bool Tag(string tagName, string message)
        {
            message = message.Replace("\"", "\\\"");
            return ExecuteGitCommand($"tag -a \"{tagName}\" -m \"{message}\"", out _, out string error);
        }

        /// <summary>
        /// Gets the current branch name.
        /// </summary>
        public static string GetBranchName()
        {
            if (ExecuteGitCommand("rev-parse --abbrev-ref HEAD", out string output, out _))
            {
                return output.Trim();
            }
            return "unknown-branch";
        }

        /// <summary>
        /// Gets the short commit hash of HEAD.
        /// </summary>
        public static string GetShortCommitHash()
        {
            if (ExecuteGitCommand("rev-parse --short HEAD", out string output, out _))
            {
                return output.Trim();
            }
            return "unknown-hash";
        }

        /// <summary>
        /// Gets the total number of commits since the specified tag. If no tag is given, gets all commits.
        /// </summary>
        public static int GetCommitsCountSinceTag(string tag = "")
        {
            string range = string.IsNullOrEmpty(tag) ? "HEAD" : $"{tag}..HEAD";
            if (ExecuteGitCommand($"rev-list --count {range}", out string output, out _))
            {
                if (int.TryParse(output.Trim(), out int count))
                {
                    return count;
                }
            }
            return 0;
        }

        /// <summary>
        /// Reverts all uncommitted changes in the working directory.
        /// Used to roll back version bumps when a build fails.
        /// </summary>
        public static bool RevertAllChanges()
        {
            return ExecuteGitCommand("checkout -- .", out _, out _);
        }
    }
}
