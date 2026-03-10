using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CommitAndTagVersion.Editor
{
    public class CommitMessage
    {
        public string RawSubject { get; set; }
        public string RawBody { get; set; }
        public string Type { get; set; }
        public string Scope { get; set; }
        public string Description { get; set; }
        public bool IsBreakingChange { get; set; }
        public string BreakingChangeDescription { get; set; }
    }

    public static class ConventionalCommitParser
    {
        // Matches: type(scope)!: description
        // Example: feat(ui)!: add new button
        private static readonly Regex HeaderRegex = new Regex(
            @"^(?<type>[a-zA-Z]+)(?:\((?<scope>[^\)]+)\))?(?<breaking>!)?:\s*(?<description>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex BreakingChangeRegex = new Regex(
            @"^[ \t]*BREAKING CHANGE:\s*(?<description>.*)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static CommitMessage Parse(string rawCommit)
        {
            // The raw commit is expected to be roughly "Subject\nBody"
            // We split by first newline or just take the whole string as subject
            var parts = rawCommit.Split(new[] { '\n', '\r' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string subject = parts.Length > 0 ? parts[0] : rawCommit;
            string body = parts.Length > 1 ? parts[1] : string.Empty;

            var commit = new CommitMessage
            {
                RawSubject = subject,
                RawBody = body
            };

            var headerMatch = HeaderRegex.Match(subject);
            if (headerMatch.Success)
            {
                commit.Type = headerMatch.Groups["type"].Value.ToLowerInvariant();
                commit.Scope = headerMatch.Groups["scope"].Success ? headerMatch.Groups["scope"].Value : null;
                commit.Description = headerMatch.Groups["description"].Value.Trim();
                
                if (headerMatch.Groups["breaking"].Success)
                {
                    commit.IsBreakingChange = true;
                    commit.BreakingChangeDescription = commit.Description;
                }
            }

            var breakingMatch = BreakingChangeRegex.Match(body);
            if (breakingMatch.Success)
            {
                commit.IsBreakingChange = true;
                commit.BreakingChangeDescription = breakingMatch.Groups["description"].Value.Trim();
            }

            return commit;
        }

        public static ReleaseType AnalyzeCommits(IEnumerable<CommitMessage> commits)
        {
            ReleaseType recommendedRelease = ReleaseType.Patch;

            foreach (var commit in commits)
            {
                if (commit.IsBreakingChange)
                {
                    return ReleaseType.Major; // Highest possible, can return early
                }

                if (commit.Type == "feat")
                {
                    recommendedRelease = ReleaseType.Minor;
                }
            }

            return recommendedRelease;
        }
    }
}
