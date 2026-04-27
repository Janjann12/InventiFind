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
    public string Status { get; set; } = "open";

    public string DateString => Date.ToString("MM/dd/yyyy");

    public Color StatusBadgeColor => Status?.ToLower() switch
    {
        "claimed" => Color.FromArgb("#2563EB"),
        "confirmed" => Color.FromArgb("#16A34A"),
        "open" => Color.FromArgb("#F59E0B"),
        _ => Color.FromArgb("#9CA3AF")
    };
    public string CategoryIcon => Category?.ToLower() switch
    {
        "phone" => "mobile.png",
        "wallet" => "wallet.png",
        "id" => "id.png",
        "watch" => "watch1.png",
        "others" => "dots.png",
        _ => "dots.png"
    };

    public string ReportTypeLabel =>
        string.IsNullOrWhiteSpace(RType)
            ? "Unknown"
            : char.ToUpper(RType[0]) + RType.Substring(1).ToLower();

    public string DescriptionText =>
        string.IsNullOrWhiteSpace(Description)
            ? "No description provided."
            : Description;

    public string LocationText =>
        string.IsNullOrWhiteSpace(Location)
            ? "No location provided."
            : Location;
}

public partial class SurrenderedItemPage : ContentPage
{
    private List<ItemReport> _allItems = new();
    private string _activeTypeFilter = "all";
    private string _activeStatus = "All Status";

    public SurrenderedItemPage()
    {
        InitializeComponent();

        StatusPicker.Items.Add("All Status");
        StatusPicker.Items.Add("open");
        StatusPicker.Items.Add("claimed");
        StatusPicker.Items.Add("confirmed");
        StatusPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadDataAsync();
        ApplyActiveFilterStyle();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            int total = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE status = 'open'");

            int lost = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE report_type = 'lost' AND status = 'open'");

            int found = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE report_type = 'found' AND status = 'open'");

            UnclaimedTotalLabel.Text = total.ToString();
            UnclaimedLostLabel.Text = lost.ToString();
            UnclaimedFoundLabel.Text = found.ToString();

            const string sql = """
                SELECT
                    i.report_id,
                    i.item_name,
                    i.category,
                    i.description,
                    i.location,
                    i.date_reported,
                    i.report_type,
                    i.status,
                    CONCAT(u.FirstName, ' ', u.Surname) AS reporter_name
                FROM item_reports i
                LEFT JOIN users u ON u.UserID = i.user_id
                ORDER BY i.date_reported DESC, i.report_id DESC
            """;

            _allItems.Clear();

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _allItems.Add(new ItemReport
                {
                    LId = reader.GetInt32("report_id"),
                    Name = reader.IsDBNull(reader.GetOrdinal("item_name")) ? "Unnamed Item" : reader.GetString("item_name"),
                    Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "Unknown" : reader.GetString("category"),
                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                    Location = reader.IsDBNull(reader.GetOrdinal("location")) ? "" : reader.GetString("location"),
                    Date = reader.GetDateTime("date_reported"),
                    RType = reader.IsDBNull(reader.GetOrdinal("report_type")) ? "" : reader.GetString("report_type"),
                    ReporterName = reader.IsDBNull(reader.GetOrdinal("reporter_name")) ? "Unknown" : reader.GetString("reporter_name"),
                    Status = reader.IsDBNull(reader.GetOrdinal("status")) ? "open" : reader.GetString("status")
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

    private void ApplyFilters()
    {
        var filtered = _allItems.AsEnumerable();

        if (_activeTypeFilter != "all")
        {
            filtered = filtered.Where(i =>
                i.RType.Equals(_activeTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (_activeStatus != "All Status")
        {
            filtered = filtered.Where(i =>
                i.Status.Equals(_activeStatus, StringComparison.OrdinalIgnoreCase));
        }

        ItemsCollection.ItemsSource =
            new ObservableCollection<ItemReport>(filtered.ToList());
    }

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

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ItemReport item)
            return;

        ItemsCollection.SelectedItem = null;
        await OpenItemModal(item);
    }

    private async Task OpenItemModal(ItemReport item)
    {
        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#C8102E"),
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };

        var modal = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = new Grid
            {
                Padding = 20,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 22,
                        Padding = 18,
                        HasShadow = true,
                        Content = new ScrollView
                        {
                            Content = new VerticalStackLayout
                            {
                                Spacing = 12,
                                Children =
                                {
                                    new Label
                                    {
                                        Text = "Item Details",
                                        FontSize = 22,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#111827")
                                    },

                                    new Label
                                    {
                                        Text = item.Name,
                                        FontSize = 17,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#C8102E")
                                    },

                                    new Label
                                    {
                                        Text = $"Report Type: {item.ReportTypeLabel}",
                                        FontSize = 13,
                                        TextColor = Color.FromArgb("#4B5563")
                                    },

                                    new Label
                                    {
                                        Text = $"Category: {item.Category}",
                                        FontSize = 13,
                                        TextColor = Color.FromArgb("#4B5563")
                                    },

                                    new Label
                                    {
                                        Text = $"Reporter: {item.ReporterName}",
                                        FontSize = 13,
                                        TextColor = Color.FromArgb("#4B5563")
                                    },

                                    new Label
                                    {
                                        Text = $"Date Reported: {item.DateString}",
                                        FontSize = 13,
                                        TextColor = Color.FromArgb("#4B5563")
                                    },

                                    new Label
                                    {
                                        Text = $"Location: {item.LocationText}",
                                        FontSize = 13,
                                        TextColor = Color.FromArgb("#4B5563")
                                    },

                                    new Label
                                    {
                                        Text = $"Status: {item.Status}",
                                        FontSize = 13,
                                        TextColor = Color.FromArgb("#4B5563")
                                    },

                                    new Label
                                    {
                                        Text = "Description",
                                        FontSize = 14,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#111827"),
                                        Margin = new Thickness(0, 8, 0, 0)
                                    },

                                    new Frame
                                    {
                                        BackgroundColor = Color.FromArgb("#F9FAFB"),
                                        BorderColor = Color.FromArgb("#E5E7EB"),
                                        CornerRadius = 14,
                                        Padding = 12,
                                        HasShadow = false,
                                        Content = new Label
                                        {
                                            Text = item.DescriptionText,
                                            FontSize = 13,
                                            TextColor = Color.FromArgb("#4B5563")
                                        }
                                    },

                                    closeBtn
                                }
                            }
                        }
                    }
                }
            }
        };

        closeBtn.Clicked += async (s, e) =>
        {
            await Navigation.PopModalAsync();
        };

        await Navigation.PushModalAsync(modal);
    }

    private void ApplyActiveFilterStyle()
    {
        AllItemsFrame.BackgroundColor = Colors.White;
        LostItemsFrame.BackgroundColor = Colors.White;
        FoundItemsFrame.BackgroundColor = Colors.White;

        AllItemsFrame.BorderColor = Color.FromArgb("#E5E7EB");
        LostItemsFrame.BorderColor = Color.FromArgb("#E5E7EB");
        FoundItemsFrame.BorderColor = Color.FromArgb("#E5E7EB");

        AllItemsLabel.TextColor = Color.FromArgb("#111827");
        LostItemsLabel.TextColor = Color.FromArgb("#111827");
        FoundItemsLabel.TextColor = Color.FromArgb("#111827");

        var (activeFrame, activeLabel) = _activeTypeFilter switch
        {
            "lost" => (LostItemsFrame, LostItemsLabel),
            "found" => (FoundItemsFrame, FoundItemsLabel),
            _ => (AllItemsFrame, AllItemsLabel)
        };

        activeFrame.BackgroundColor = Color.FromArgb("#C8102E");
        activeFrame.BorderColor = Color.FromArgb("#C8102E");
        activeLabel.TextColor = Colors.White;
    }

    private async void OnDashboardTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new AdminDashboard());
    }

    private async void OnVerifyTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new LostItemDetailPage());
    }

    private async void OnReturnedTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReturnedItemsPage());
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");

        if (!confirm) return;

        Preferences.Remove("UserID");
        Preferences.Remove("UserEmail");

        await Shell.Current.GoToAsync("//MainPage");
    }
}