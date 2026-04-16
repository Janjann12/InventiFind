namespace InventiFind;

public partial class NotificationModule : ContentPage
{
    public NotificationModule()
    {
        InitializeComponent();
    }

    private async void OnHomeTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopToRootAsync();
        await Navigation.PushAsync(new StudentDashboard());
    }

    private async void OnReportTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReportModule());
    }

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReceiveModule());
    }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        // Already on News/NotificationModule page
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
        }
    }
}