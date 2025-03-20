using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PrefabLocker.Editor
{
    public static class GitProvider
    {
        private static string _cachedBranch;
        private static string _cachedOrigin;
        private static List<string> _cachedBranches;
        private static DateTime _lastBranchUpdate = DateTime.MinValue;
        private static DateTime _lastOriginUpdate = DateTime.MinValue;
        private static DateTime _lastBranchesListUpdate = DateTime.MinValue;
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromSeconds(30);

        public static string GetBranch(bool forceRefresh = false)
        {
            if (_cachedBranch == null ||
                forceRefresh ||
                DateTime.Now - _lastBranchUpdate > CacheTimeout)
            {
                try
                {
                    string output = ExecuteGitCommand("rev-parse --abbrev-ref HEAD");
                    _cachedBranch = string.IsNullOrEmpty(output) ? "unknown" : output.Trim();
                    _lastBranchUpdate = DateTime.Now;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to get git branch: {e.Message}");
                    _cachedBranch = "unknown";
                }
            }

            return _cachedBranch;
        }

        public static string GetOrigin(bool forceRefresh = false)
        {
            if (_cachedOrigin == null ||
                forceRefresh ||
                DateTime.Now - _lastOriginUpdate > CacheTimeout)
            {
                try
                {
                    string output = ExecuteGitCommand("config --get remote.origin.url");
                    _cachedOrigin = string.IsNullOrEmpty(output) ? "unknown" : output.Trim();
                    _lastOriginUpdate = DateTime.Now;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to get git origin: {e.Message}");
                    _cachedOrigin = "unknown";
                }
            }

            return _cachedOrigin;
        }

        public static List<string> GetAllBranches(bool forceRefresh = false)
        {
            if (_cachedBranches == null ||
                forceRefresh ||
                DateTime.Now - _lastBranchesListUpdate > CacheTimeout)
            {
                try
                {
                    // Get all branches including remote ones
                    string output = ExecuteGitCommand("branch -a");
                    if (string.IsNullOrEmpty(output))
                    {
                        _cachedBranches = new List<string>();
                        return _cachedBranches;
                    }

                    _cachedBranches = new List<string>();

                    // Parse the output to extract branch names
                    string[] lines = output.Split('\n');
                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();

                        // Skip empty lines
                        if (string.IsNullOrEmpty(trimmedLine))
                            continue;

                        // Remove the asterisk from current branch
                        if (trimmedLine.StartsWith("*"))
                            trimmedLine = trimmedLine.Substring(1).Trim();

                        // Skip remotes/ prefixes
                        if (trimmedLine.StartsWith("remotes/"))
                            trimmedLine = trimmedLine.Substring("remotes/".Length);

                        // Skip duplicate branches and HEAD pointers
                        if (!_cachedBranches.Contains(trimmedLine) && !trimmedLine.Contains("HEAD ->"))
                            _cachedBranches.Add(trimmedLine);
                    }

                    _lastBranchesListUpdate = DateTime.Now;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to get git branches: {e.Message}");
                    _cachedBranches = new List<string>();
                }
            }

            return _cachedBranches;
        }

        public static bool CheckoutBranch(string branchName)
        {
            try
            {
                // First try to check out directly (local branch)
                string output = ExecuteGitCommand($"checkout {branchName}");

                // If that fails, it might be a remote branch, try to create a local tracking branch
                if (output.Contains("error"))
                {
                    // Extract the remote name and actual branch name
                    string[] parts = branchName.Split('/', 2);
                    if (parts.Length < 2)
                    {
                        return false;
                    }

                    string remote = parts[0];
                    string branch = parts[1];

                    output = ExecuteGitCommand($"checkout -b {branch} {remote}/{branch}");
                }

                // Force refresh all cached git information after branch switch
                GetBranch(true);
                GetOrigin(true);
                GetAllBranches(true);

                return !output.Contains("error");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to checkout branch: {e.Message}");
                return false;
            }
        }

        private static string ExecuteGitCommand(string arguments)
        {
            ProcessStartInfo processInfo = new()
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Application.dataPath
            };

            StringBuilder output = new();
            StringBuilder error = new();

            using (Process process = new())
            {
                process.StartInfo = processInfo;
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errorMsg = error.ToString().Trim();
                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        UnityEngine.Debug.LogWarning($"Git command error: {errorMsg}");
                    }

                    return string.Empty;
                }
            }

            return output.ToString().Trim();
        }

        public static bool HasLocalChanges(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string normalizedFilePath = filePath.Replace('\\', '/').Trim();
            string repoRelativePath = ConvertToRepoRelativePath(normalizedFilePath);

            // Check main repository changes
            if (CheckPathInGitStatus(repoRelativePath, "status --porcelain -uall"))
            {
                return true;
            }

            string submoduleStatus = ExecuteGitCommand("submodule --quiet foreach git status --porcelain -uall");
            if (string.IsNullOrWhiteSpace(submoduleStatus))
            {
                return false;
            }

            string[] changedFiles = submoduleStatus.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            return changedFiles.Any(statusLine =>
            {
                string statusPath = statusLine.Trim().Substring(statusLine.IndexOf(" ", StringComparison.Ordinal) + 1).Trim();
                return repoRelativePath.Contains(statusPath, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool CheckPathInGitStatus(string repoRelativePath, string gitCommand)
        {
            string gitStatus = ExecuteGitCommand(gitCommand);
            return CheckPathInGitStatusOutput(repoRelativePath, gitStatus);
        }

        private static bool CheckPathInGitStatusOutput(string repoRelativePath, string gitStatus)
        {
            if (string.IsNullOrWhiteSpace(gitStatus))
                return false;

            string[] changedFiles = gitStatus.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            return changedFiles.Any(statusLine =>
                statusLine.Contains(repoRelativePath, StringComparison.OrdinalIgnoreCase));
        }

        // Helper method to convert Unity asset path to Git repo relative path
        private static string ConvertToRepoRelativePath(string unityPath)
        {
            try
            {
                // Get the repo root directory (absolute path)
                string repoRoot = ExecuteGitCommand("rev-parse --show-toplevel").Replace('\\', '/');
                if (string.IsNullOrEmpty(repoRoot))
                    return unityPath; // Fallback if we can't get repo root

                // Get Unity project path (Application.dataPath is /path/to/project/Assets)
                string unityProjectPath = Application.dataPath.Replace('\\', '/');
                // Remove the trailing "Assets" to get the project root
                string projectRoot = unityProjectPath.Substring(0, unityProjectPath.LastIndexOf("/Assets"));

                // Check if path is already a full path
                if (System.IO.Path.IsPathRooted(unityPath))
                {
                    // Normalize path format with forward slashes
                    unityPath = unityPath.Replace('\\', '/');
                }
                else if (unityPath.StartsWith("Assets/") || unityPath == "Assets")
                {
                    // Prepend the project root to get absolute path
                    unityPath = $"{projectRoot}/{unityPath}";
                }
                else
                {https://github.com/zbyhoo/package_locker_unity/blob/master/PrefabLocker/Editor/GitProvider.cs
                    // Assume it's already relative to project root
                    unityPath = $"{projectRoot}/{unityPath}";
                }

                // Now determine the path relative to the git repository
                // Check if unityPath is within the repo
                if (unityPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
                {
                    // Path is inside repo, make it relative to repo root
                    return unityPath.Substring(repoRoot.Length).TrimStart('/');
                }

                // Path is outside repo, can't make it relative
                UnityEngine.Debug.LogWarning($"Path is outside git repository: {unityPath}");
                return unityPath;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error converting path to repo relative: {ex.Message}");
                return unityPath; // Return original path on error
            }
        }

        public static bool IsLastCommitPushedToRemote()
        {
            try
            {
                string localCommit = ExecuteGitCommand($"rev-parse HEAD");

                // If we can't get local commit hash, the file might not be tracked
                if (string.IsNullOrEmpty(localCommit))
                    return false;


                // Check if the local commit exists on the remote
                string localHash = localCommit.Trim();
                string checkIfPushed = ExecuteGitCommand($"branch -r --contains {localHash}");

                // If we find any remote branch containing this commit, it's been pushed
                return !string.IsNullOrEmpty(checkIfPushed);
            }
            catch
            {
                // If any errors occur (like upstream not set), it's not synced
                return false;
            }
        }
    }
}
