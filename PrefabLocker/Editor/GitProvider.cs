using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace PrefabLocker.Editor
{
    public static class GitProvider
    {
        public static string GetBranch()
        {
            try
            {
                string output = ExecuteGitCommand("rev-parse --abbrev-ref HEAD");
                return string.IsNullOrEmpty(output) ? "unknown" : output.Trim();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to get git branch: {e.Message}");
                return "unknown";
            }
        }

        public static string GetOrigin()
        {
            try
            {
                string output = ExecuteGitCommand("config --get remote.origin.url");
                return string.IsNullOrEmpty(output) ? "unknown" : output.Trim();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to get git origin: {e.Message}");
                return "unknown";
            }
        }

        private static string ExecuteGitCommand(string arguments)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Application.dataPath
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            using (Process process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errorMsg = error.ToString().Trim();
                    if (!string.IsNullOrEmpty(errorMsg))
                        UnityEngine.Debug.LogWarning($"Git command error: {errorMsg}");
                    return string.Empty;
                }
            }

            return output.ToString().Trim();
        }
        
        public static List<string> GetAllBranches()
{
    try
    {
        // Get all branches including remote ones
        string output = ExecuteGitCommand("branch -a");
        if (string.IsNullOrEmpty(output))
            return new List<string>();
            
        List<string> branches = new List<string>();
        
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
            if (!branches.Contains(trimmedLine) && !trimmedLine.Contains("HEAD ->"))
                branches.Add(trimmedLine);
        }
        
        return branches;
    }
    catch (Exception e)
    {
        UnityEngine.Debug.LogError($"Failed to get git branches: {e.Message}");
        return new List<string>();
    }
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
                return false;
                
            string remote = parts[0];
            string branch = parts[1];
            
            output = ExecuteGitCommand($"checkout -b {branch} {remote}/{branch}");
        }
        
        return !output.Contains("error");
    }
    catch (Exception e)
    {
        UnityEngine.Debug.LogError($"Failed to checkout branch: {e.Message}");
        return false;
    }
}
    }
}