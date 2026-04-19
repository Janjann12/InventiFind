using MySqlConnector;
using System.Collections.ObjectModel;

namespace InventiFind;

public class ItemReport
{
    public int LId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime Date { get; set; }
    public string RType { get; set; } = "";
    public string ReporterName { get; set; } = "";
    public string Status { get; set; } = "Pending";

    // Derived display properties
    public string DateString => Date.ToString("MM/dd/yyyy");

    public Color StatusBadgeColor => Status?.ToLower() switch
    {
        "confirmed" => Color.FromArgb("#5A9E5A"),
        "resolved" => Color.FromArgb("#2563EB"),
        _ => Color.FromArgb("#F59E0B")   // Pending = amber
    };

    public bool HasImage => false; // extend when serving images from DB
    public string? ImageSource => null;
}
public partial class SurrenderedItemPage : ContentPage
{
    private List<ItemReport> _allItems = new();
    private string _activeTypeFilter = "all";
    private string _activeStatus = "All Status";

    public SurrenderedItemPage()
    {
        InitializeComponent();
        StatusPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
        ApplyActiveFilterStyle();
    }

    // ── Data loading ───────────────────────────────────────────────────────

    private async Task LoadDataAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // Summary counts (all unclaimed = "Pending" status proxy)
            int total = await GetScalarAsync(conn, "SELECT COUNT(*) FROM items");
            int lost = await GetScalarAsync(conn, "SELECT COUNT(*) FROM items WHERE r_type='lost'");
            int found = await GetScalarAsync(conn, "SELECT COUNT(*) FROM items WHERE r_type='found'");

            UnclaimedTotalLabel.Text = total.ToString();
            UnclaimedLostLabel.Text = lost.ToString();
            UnclaimedFoundLabel.Text = found.ToString();

            // Items list
            const string sql = """
                SELECT  i.L_ID, i.name, i.category, i.description,
                        i.location, i.date, i.r_type,
                        CONCAT(u.FirstName, ' ', u.Surname) AS reporter_name
                FROM items i
                LEFT JOIN users u ON u.UserID = i.L_ID
                ORDER BY i.date DESC, i.L_ID DESC
            """;

            _allItems.Clear();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _allItems.Add(new ItemReport
                {
                    LId = reader.GetInt32("L_ID"),
                    Name = reader.GetString("name"),
                    Category = reader.GetString("category"),
                    Description = reader.GetString("description"),
                    Location = reader.GetString("location"),
                    Date = reader.GetDateTime("date"),
                    RType = reader.GetString("r_type"),
                    ReporterName = reader.IsDBNull(reader.GetOrdinal("reporter_name"))
                                       ? "Unknown"
                                       : reader.GetString("reporter_name"),
                    Status = "Confirmed"
                });
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load data:\n{ex.Message}", "OK");
        }
    }

    private static async Task<int> GetScalarAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }

    // ── Filter logic ───────────────────────────────────────────────────────

    private void ApplyFilters()
    {
        var filtered = _allItems.AsEnumerable();

        if (_activeTypeFilter != "all")
            filtered = filtered.Where(i =>
                i.RType.Equals(_activeTypeFilter, StringComparison.OrdinalIgnoreCase));

        if (_activeStatus != "All Status")
            filtered = filtered.Where(i =>
                i.Status.Equals(_activeStatus, StringComparison.OrdinalIgnoreCase));

        ItemsCollection.ItemsSource =
            new ObservableCollection<ItemReport>(filtered.ToList());
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnFilterTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string param)
        {
            _activeTypeFilter = param;
            ApplyActiveFilterStyle();
            ApplyFilters();
        }
    }

    private void OnStatusPickerChanged(object sender, EventArgs e)
    {
        _activeStatus = StatusPicker.SelectedItem?.ToString() ?? "All Status";
        ApplyFilters();
    }

    // ── UI helpers ─────────────────────────────────────────────────────────

    private void ApplyActiveFilterStyle()
    {
        AllItemsFrame.BackgroundColor = Colors.White;
        LostItemsFrame.BackgroundColor = Colors.White;
        FoundItemsFrame.BackgroundColor = Colors.White;
        AllItemsLabel.TextColor = Color.FromArgb("#1A1A1A");
        LostItemsLabel.TextColor = Color.FromArgb("#1A1A1A");
        FoundItemsLabel.TextColor = Color.FromArgb("#1A1A1A");

        var (activeFrame, activeLabel) = _activeTypeFilter switch
        {
            "lost" => (LostItemsFrame, LostItemsLabel),
            "found" => (FoundItemsFrame, FoundItemsLabel),
            _ => (AllItemsFrame, AllItemsLabel)
        };

        activeFrame.BackgroundColor = Color.FromArgb("#8B0000");
        activeLabel.TextColor = Colors.White;
    }
}