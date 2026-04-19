using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace InventiFind

{
    public partial class App : Application
    {

        public App()
        {
            InitializeComponent();

        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new ReturnedItemsPage());


        }
    }


}