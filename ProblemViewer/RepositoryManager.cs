using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace ProblemViewer
{
    public static class RepositoryManager

    {
        public static bool Working { get; private set; } = false;

        public static void UpdateRepository(Window? parentwindow=null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var updateWindow = new UpdateWindow
                {
                    Owner = parentwindow 
                };
                updateWindow.Show();
            });
            
        }
        public static void UpdateRepositoryWorker()
        {
            Working = true;
            if (Settings.ForceUpdateRequired)
            {
                /*var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c rd /s /q " + Settings.LocalRepositoryPath,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var p = Process.Start(startInfo);
                p.WaitForExit();*/
                try
                {
                    foreach (string file in Directory.EnumerateFileSystemEntries(Settings.LocalRepositoryPath, "*",
                                 SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(Settings.LocalRepositoryPath, true);
                }
                catch (DirectoryNotFoundException)
                {
                    //Do nothing
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(()=>MessageBox.Show(Application.Current.MainWindow!,$"Error deleting existed repository:{ex.Message}"));
                    Working = false;
                    return;
                    //TODO
                }


            }
            Settings.ForceUpdateRequired = true;
            // Check if the local repository exists
            if (!System.IO.Directory.Exists(Path.Combine(Settings.LocalRepositoryPath, ".git")))
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
            Working = false;
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
                Application.Current.Dispatcher.Invoke(()=>MessageBox.Show(Application.Current.MainWindow!,$"Error cloning repository: {ex.Message}"));
                Working = false;
                //TODO
            }
        }
        private static void PullRepository()
        {
            try
            {
                using var repo = new LibGit2Sharp.Repository(Settings.LocalRepositoryPath);
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                LibGit2Sharp.Commands.Fetch(repo, remote.Name, refSpecs, null, null);
                var mergeResult = repo.MergeFetchedRefs(
                    new LibGit2Sharp.Signature("AutoUpdater", "auto.updater@example.com", DateTimeOffset.Now),
                    null);
            }
            catch (Exception ex)
            {

                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(Application.Current.MainWindow!,$"Error pulling repository: {ex.Message}"));
                Working = false;
                //TODO
            }
        }
    }
}
