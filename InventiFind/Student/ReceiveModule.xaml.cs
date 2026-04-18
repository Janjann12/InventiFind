using System.Collections.ObjectModel;
using System.Windows.Input;

namespace InventiFind;

public partial class ReceiveModule : ContentPage
{
    public ReceiveModule()
    {
        InitializeComponent();
        BindingContext = new ReceiveModuleViewModel();
    }

    private async void OnHomeTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new StudentDashboard());
    }

    private async void OnReportTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReportModule());
    }

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
    {
        // Already on Receive page - optional refresh
        // await Navigation.PushAsync(new ReceiveModule());
    }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new NotificationModule());
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");
        if (confirm)
        {
            await Shell.Current.GoToAsync("//MainPage");

        }
    }
}

public class ReceiveModuleViewModel
{
    public ObservableCollection<LostItem> LostItems { get; set; }
    public ICommand ClaimItemCommand { get; }
    public ICommand ContactCommand { get; }

    public ReceiveModuleViewModel()
    {
        LostItems = new ObservableCollection<LostItem>
        {
            new LostItem
            {
                ItemName = "CLN Wallet",
                Description = "Contains ID and Credit Cards",
                Location = "Lost & Found Office, SAO",
                FoundDate = new DateTime(2026, 4, 5),
                ClaimCode = "LF-2024-001"
            },
            new LostItem
            {
                ItemName = "iPhone 67",
                Description = "-----",
                Location = "Lost & Found Office, SAO",
                FoundDate = new DateTime(2026, 4, 5),
                ClaimCode = "LF-2024-001"
            },
            new LostItem
            {
                ItemName = "CLN Wallet",
                Description = "Contains ID and Credit Cards",
                Location = "Lost & Found Office, SAO",
                FoundDate = new DateTime(2026, 4, 5),
                ClaimCode = "LF-2024-001"
            }
        };

        ClaimItemCommand = new Command<LostItem>(OnClaimItem);
        ContactCommand = new Command<LostItem>(OnContact);


    }

 


    private void OnClaimItem(LostItem item)
    {
        // Handle claim item logic
        Shell.Current.DisplayAlert("Claim", $"Claiming {item.ItemName}", "OK");
    }

    private void OnContact(LostItem item)
    {
        // Handle contact logic
        Shell.Current.DisplayAlert("Contact", $"Contacting about {item.ItemName}", "OK");
    }
}



public class LostItem
{
    public string ItemName { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public DateTime FoundDate { get; set; }
    public string ClaimCode { get; set; }
}