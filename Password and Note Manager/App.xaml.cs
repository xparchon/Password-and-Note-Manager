using Password_and_Note_Manager.Helpers;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Password_and_Note_Manager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize the default vault when the application starts
            DatabaseHelper.InitializeDatabase();
        }

    }

}
