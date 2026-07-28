using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace ProblemViewer
{
    public static class RepositoryManager
    {
        public static void UpdateRepository()
        {

            if (Settings.ForceUpdateRequired) {
                var p = Process.Start("cmd.exe","/c rd /s /q " + Settings.LocalRepositoryPath);
                p.WaitForExit();
            }
            
            // Check if the local repository exists
            if (!System.IO.Directory.Exists(Settings.LocalRepositoryPath))
            {
                // Clone the repository if it doesn't exist
                CloneRepository();
            }
            else
            {
                // Pull the latest changes if it exists
                PullRepository();
            }
            Settings.ForceUpdateRequired = false;
            Settings.SaveSettings();
        }
        private static void CloneRepository()
        {
            // Use LibGit2Sharp to clone the repository
            try
            {
                LibGit2Sharp.Repository.Clone(Settings.RepositoryURL, Settings.LocalRepositoryPath);
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., network issues, invalid URL)
                MessageBox.Show($"Error cloning repository: {ex.Message}");
                //TODO
            }
        }
        private static void PullRepository()
        {
            try
            {
                using (var repo = new LibGit2Sharp.Repository(Settings.LocalRepositoryPath))
                {
                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    LibGit2Sharp.Commands.Fetch(repo, remote.Name, refSpecs, null, null);
                    var mergeResult = repo.MergeFetchedRefs(
                        new LibGit2Sharp.Signature("AutoUpdater", "auto.updater@example.com", DateTimeOffset.Now),
                        null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pulling repository: {ex.Message}");
                //TODO
            }
        }
    }
}
