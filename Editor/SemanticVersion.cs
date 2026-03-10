using System;
using System.Text.RegularExpressions;

namespace CommitAndTagVersion.Editor
{
    public enum ReleaseType
    {
        Patch,
        Minor,
        Major
    }

    public class SemanticVersion : IComparable<SemanticVersion>
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public string Prerelease { get; private set; }
        public string Build { get; private set; }

        private static readonly Regex SemVerRegex = new Regex(
            @"^(?:v|V)?(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<build>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
            RegexOptions.Compiled);

        public SemanticVersion(int major, int minor, int patch, string prerelease = "", string build = "")
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease;
            Build = build;
        }

        public static bool TryParse(string versionString, out SemanticVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(versionString))
                return false;

            var match = SemVerRegex.Match(versionString.Trim());
            if (!match.Success)
                return false;

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);
            int patch = int.Parse(match.Groups["patch"].Value);
            string prerelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : string.Empty;
            string build = match.Groups["build"].Success ? match.Groups["build"].Value : string.Empty;

            version = new SemanticVersion(major, minor, patch, prerelease, build);
            return true;
        }

        public SemanticVersion Increment(ReleaseType releaseType)
        {
            switch (releaseType)
            {
                case ReleaseType.Major:
                    return new SemanticVersion(Major + 1, 0, 0);
                case ReleaseType.Minor:
                    return new SemanticVersion(Major, Minor + 1, 0);
                case ReleaseType.Patch:
                default:
                    return new SemanticVersion(Major, Minor, Patch + 1);
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder($"{Major}.{Minor}.{Patch}");
            if (!string.IsNullOrEmpty(Prerelease))
            {
                sb.Append($"-{Prerelease}");
            }
            if (!string.IsNullOrEmpty(Build))
            {
                sb.Append($"+{Build}");
            }
            return sb.ToString();
        }

        public int CompareTo(SemanticVersion other)
        {
            if (other == null) return 1;

            int majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;

            int minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0) return minorComparison;

            int patchComparison = Patch.CompareTo(other.Patch);
            if (patchComparison != 0) return patchComparison;

            // Simple Prerelease comparison (not fully compliant with SemVer 2.0.0 rules but sufficient for most tags)
            if (string.IsNullOrEmpty(Prerelease) && !string.IsNullOrEmpty(other.Prerelease)) return 1;
            if (!string.IsNullOrEmpty(Prerelease) && string.IsNullOrEmpty(other.Prerelease)) return -1;
            
            return string.Compare(Prerelease, other.Prerelease, StringComparison.Ordinal);
        }
    }
}
