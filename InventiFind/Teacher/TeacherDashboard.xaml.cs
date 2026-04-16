namespace InventiFind;

public partial class TeacherDashboard : ContentPage
{
    public TeacherDashboard()
    {
        InitializeComponent();
    }

    private async void OnReportTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Report", "Create new report", "OK");
    }

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Receive", "Receive item", "OK");
    }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("News", "View notifications", "OK");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
        }
    }
}