using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(30);

        public static string GetBranch(bool forceRefresh = false)
        {
            if (_cachedBranch == null || 
                forceRefresh || 
                DateTime.Now - _lastBranchUpdate > _cacheTimeout)
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
                DateTime.Now - _lastOriginUpdate > _cacheTimeout)
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
                DateTime.Now - _lastBranchesListUpdate > _cacheTimeout)
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
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
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
    }
}