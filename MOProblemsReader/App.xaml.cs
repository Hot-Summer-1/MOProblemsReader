using System.Configuration;
using System.Data;
using System.Windows;
using ProblemViewer;

namespace MOProblemsReader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Settings.SaveSettings();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Settings.LoadSettings();
            if (Settings.ForceUpdateRequired)
            {
                RepositoryManager.UpdateRepository();
            }
        }
    }

}
