namespace InventiFind;

public partial class ReturnModule : ContentPage
{
	public ReturnModule()
	{
		InitializeComponent();
	}



    private async void OnDashboardTapped(object sender, TappedEventArgs e)
    {
         await Navigation.PushAsync(new TeacherDashboard());
    }

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReportsModule());
    }

    private async void OnReturnTapped(object sender, TappedEventArgs e)
    {
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Log Out", "Are you sure you want to log out?", "Yes", "No");

        if (confirm)
        {


            // Navigate to login page and clear navigation stack
            await Navigation.PopToRootAsync();
            // Or: Application.Current.MainPage = new NavigationPage(new MainPage());
        }
    }
}