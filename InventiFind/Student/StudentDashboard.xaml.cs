
namespace InventiFind;

public partial class StudentDashboard : ContentPage
{
    public StudentDashboard()
    {
        InitializeComponent();
    }

    // View All tapped
    private async void OnViewAllTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("View All", "Show all items", "OK");
    }

    // Bottom navigation taps
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
        await Navigation.PushAsync(new NotificationModule());

    }

    private async void OnLogoutClicked(object sender, EventArgs e)  // For Button.Clicked
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
        }
    }
}