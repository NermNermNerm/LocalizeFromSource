using System.Text.RegularExpressions;
using LocalizeFromSource;

namespace LfsCompiler
{
    public class GitRepoInfo
    {
        private GitRepoInfo(string? repositoryPath, string? headCommit, string? githubRepoRootUrl)
        {
            this.RepositoryPath = repositoryPath;
            this.HeadCommit = headCommit;
            this.GithubRepoRootUrl = githubRepoRootUrl;
        }

        public string? RepositoryPath { get; }
        public string? HeadCommit { get; }
        public string? GithubRepoRootUrl { get; }

        public static GitRepoInfo CreateNull() => new GitRepoInfo(null, null, null);

        public static GitRepoInfo Create(Reporter reporter, string sourceRoot)
        {
            string? repositoryPath = GetRepoRoot(sourceRoot);
            if (repositoryPath is null)
            {
                reporter.ReportGitRepoError("Not executed from a git repository.  Hyperlinks will not be generated.");
                return new GitRepoInfo(null, null, null);
            }

            string? headCommit = GetHeadCommit(repositoryPath);
            if (headCommit is null)
            {
                reporter.ReportGitRepoError("Could not calculate the git HEAD commit.  Hyperlinks will not be generated.");
            }

            string? githubUri = GetGithubUri(repositoryPath);
            if (githubUri is null)
            {
                reporter.ReportGitRepoError("This repository does not appear to be hosted on Github.  Hyperlinks will not be generated.");
            }

            return new GitRepoInfo(repositoryPath, headCommit, githubUri);
        }

        private static string? GetRepoRoot(string sourceRoot)
        {
            string? path = sourceRoot;
            do
            {
                if (Directory.Exists(Path.Combine(path, ".git")))
                {
                    return path;
                }

                path = Path.GetDirectoryName(path);
            } while (path is not null);

            return null;
        }

        private static string? GetHeadCommit(string repositoryPath)
        {
            // Path to the .git directory
            string gitDirectory = Path.Combine(repositoryPath, ".git");

            // Read the contents of the HEAD file
            string headFilePath = Path.Combine(gitDirectory, "HEAD");
            string headFileContent = File.ReadAllText(headFilePath).Trim();

            // Check if HEAD is pointing to a ref or a commit directly
            if (headFileContent.StartsWith("ref:"))
            {
                // Extract the ref path
                string refPath = headFileContent.Substring(5).Trim();
                string refFilePath = Path.Combine(gitDirectory, refPath);

                // Check if the ref file exists as a loose ref
                if (File.Exists(refFilePath))
                {
                    // Read the contents of the ref file to get the commit hash
                    return File.ReadAllText(refFilePath).Trim();
                }
                else
                {
                    // If the ref file doesn't exist, check the packed-refs file
                    string packedRefsFilePath = Path.Combine(gitDirectory, "packed-refs");
                    if (File.Exists(packedRefsFilePath))
                    {
                        // Read the packed-refs file and search for the ref path
                        var lines = File.ReadAllLines(packedRefsFilePath);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            if (line.EndsWith(refPath))
                            {
                                // The line format is "<commit-hash> <ref-path>"
                                return line.Split(' ')[0].Trim();
                            }
                        }
                    }

                    return null;
                }
            }
            else
            {
                // HEAD is directly pointing to a commit hash
                return headFileContent;
            }
        }

        private static readonly Regex githubRemoteUrlPattern = new Regex(@"^\s+url\s*=\s*(?<uri>https://github.com/[^\s]*)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static string? GetGithubUri(string repositoryPath)
        {
            string configPath = Path.Combine(repositoryPath, ".git", "config");
            bool inRemote = false;
            foreach (string line in File.ReadAllLines(configPath))
            {
                if (line.StartsWith("[remote "))
                {
                    inRemote = true;
                }
                else if (line.StartsWith("["))
                {
                    inRemote = false;
                }
                else if (inRemote)
                {
                    var match = githubRemoteUrlPattern.Match(line);
                    if (match.Success)
                    {
                        return match.Groups["uri"].Value;
                    }
                }
            }

            return null;
        }
    }
}