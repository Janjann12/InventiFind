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

public partial class LostitemsPage : ContentPage
{
    private List<ItemReport> _allItems = new();
    private string _activeTypeFilter = "all";   // all | lost | found
    private string _activeStatus = "All Status";

    public LostitemsPage()
	{
		InitializeComponent();
        StatusPicker.SelectedIndex = 0;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadItemsAsync();
        ApplyActiveFilterStyle();
    }

    // ── Data loading ───────────────────────────────────────────────────────

    private async Task LoadItemsAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // Join items with users to get reporter full name
            const string sql = """
                SELECT  i.L_ID, i.name, i.category, i.description,
                        i.location, i.date, i.r_type,
                        CONCAT(u.FirstName, ' ', u.Surname) AS reporter_name
                FROM items i
                LEFT JOIN users u ON u.UserID = i.L_ID   -- adjust FK if needed
                ORDER BY i.date DESC, i.L_ID DESC
            """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            _allItems.Clear();
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
                    Status = "Confirmed"  // placeholder — add a status column to extend
                });
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load items:\n{ex.Message}", "OK");
        }
    }

    // ── Filter logic ───────────────────────────────────────────────────────

    private void ApplyFilters()
    {
        var filtered = _allItems.AsEnumerable();

        // Type filter
        if (_activeTypeFilter != "all")
            filtered = filtered.Where(i =>
                i.RType.Equals(_activeTypeFilter, StringComparison.OrdinalIgnoreCase));

        // Status filter
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
        // Reset all to white
        AllItemsFrame.BackgroundColor = Colors.White;
        LostItemsFrame.BackgroundColor = Colors.White;
        FoundItemsFrame.BackgroundColor = Colors.White;
        AllItemsLabel.TextColor = Color.FromArgb("#1A1A1A");
        LostItemsLabel.TextColor = Color.FromArgb("#1A1A1A");
        FoundItemsLabel.TextColor = Color.FromArgb("#1A1A1A");

        // Highlight active
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