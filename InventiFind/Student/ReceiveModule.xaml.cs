using MySqlConnector;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace InventiFind;

public partial class ReceiveModule : ContentPage
{
    // 🔥 COLLECTION (bind to UI)
    public ObservableCollection<LostItem> LostItems { get; set; } = new();

    public ICommand ContactCommand { get; }

    public ReceiveModule()
    {
        InitializeComponent();

        // Bind page to itself
        BindingContext = this;

        // Initialize commands
        ContactCommand = new Command<LostItem>(OnContact);

        // Load data
        LoadItems();
    }

    // 🔥 LOAD DATA FROM DATABASE
    private async void LoadItems()
    {
        try
        {
            using (var conn = new MySqlConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                string query = @"
SELECT 
    ir.item_name,
    ir.description,
    ir.location,
    ir.date_reported,
    ir.report_id,
    u.FirstName,
    u.Surname
FROM item_reports ir
JOIN users u ON ir.user_id = u.UserID
WHERE ir.status = 'matched' OR ir.status = 'claimed'
ORDER BY ir.date_reported DESC";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    LostItems.Clear();

                    while (await reader.ReadAsync())
                    {
                        LostItems.Add(new LostItem
                        {
                            ItemName = reader["item_name"].ToString(),
                            Description = reader["description"]?.ToString(),
                            Location = reader["location"]?.ToString(),
                            FoundDate = Convert.ToDateTime(reader["date_reported"]),
                            ClaimCode = $"LF-{reader["report_id"]}",
                            Author = $"{reader["FirstName"]} {reader["Surname"]}"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // 🔥 CLAIM BUTTON — wired via Clicked="OnClaimButtonClicked" in XAML
    private async void OnClaimButtonClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var item = button?.BindingContext as LostItem;

        if (item == null) return;

        bool confirmed = await DisplayAlert(
            "Claim Item",
            $"Do you want to claim \"{item.ItemName}\"?\nClaim Code: {item.ClaimCode}",
            "Yes, Claim",
            "Cancel"
        );

        if (confirmed)
        {
            try
            {
                using (var conn = new MySqlConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    // Extract numeric ID from ClaimCode e.g. "LF-42" → 42
                    string reportIdStr = item.ClaimCode.Replace("LF-", "");

                    string update = @"
UPDATE item_reports 
SET status = 'claimed' 
WHERE report_id = @reportId";

                    using (var cmd = new MySqlCommand(update, conn))
                    {
                        cmd.Parameters.AddWithValue("@reportId", reportIdStr);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await DisplayAlert("Success", $"\"{item.ItemName}\" has been claimed!", "OK");

                // Refresh the list
                LoadItems();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }

    // 🔥 CONTACT ACTION
    private void OnContact(LostItem item)
    {
        DisplayAlert("Contact", $"Contacting about {item.ItemName}", "OK");
    }

    // 🔹 NAVIGATION
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
        // Already here
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

// 🔥 MODEL
public class LostItem
{
    public string ItemName { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public DateTime FoundDate { get; set; }
    public string ClaimCode { get; set; }
    public string Author { get; set; }
}