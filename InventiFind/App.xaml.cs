using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace InventiFind

{
    public partial class App : Application
    {
        public static int CurrentUserId => Preferences.Get("UserId", 0);
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());


    }
    }


}