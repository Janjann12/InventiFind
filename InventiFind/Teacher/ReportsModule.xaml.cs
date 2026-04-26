using Microsoft.Maui.Controls;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventiFind
{
    public partial class ReportsModule : ContentPage
    {
        // ── State ──────────────────────────────────────────────────────────
        private List<ReportsItems> _allReports = new();
        private string _activeTypeFilter = "all";
        private string _activeStatusFilter = "";

        private const string ConnStr =
            "Server=localhost;Port=3306;Database=inventifind2;Uid=root;Pwd=;";

        public ReportsModule()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadReportsAsync();
        }

        // ── Database Loading ───────────────────────────────────────────────
        private async Task LoadReportsAsync()
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ReportsContainer.IsVisible = false;
            EmptyState.IsVisible = false;

            try
            {
                var reports = await Task.Run(() => FetchReportsFromDb());
                _allReports = reports;
                LblRecordCount.Text = $"{reports.Count} record{(reports.Count != 1 ? "s" : "")} total";
                ApplyFilters();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not load reports:\n{ex.Message}", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ReportsContainer.IsVisible = true;
            }
        }

        private List<ReportsItems> FetchReportsFromDb()
        {
            var list = new List<ReportsItems>();

            using var conn = new MySqlConnection(ConnStr);
            conn.Open();

            var sql = @"
    SELECT r.report_id, r.user_id, r.report_type,
           r.item_name, r.category, r.description,
           r.location, r.status, r.date_reported,
           IFNULL(CONCAT(u.FirstName, ' ', u.Surname),
                  CONCAT('User #', r.user_id)) AS reporter_name
    FROM item_reports r
    LEFT JOIN users u ON u.UserID = r.user_id
    ORDER BY r.date_reported DESC";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new ReportsItems
                {
                    ReportId = reader.GetInt32("report_id"),
                    UserId = reader.GetInt32("user_id"),
                    ReportType = reader.GetString("report_type"),
                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                    Location = reader.IsDBNull(reader.GetOrdinal("location")) ? "" : reader.GetString("location"),
                    Status = reader.IsDBNull(reader.GetOrdinal("status")) ? "open" : reader.GetString("status"),
                    DateReported = reader.GetDateTime("date_reported"),
                    ReporterName = reader.GetString("reporter_name"),
                });
            }

            return list;
        }

        // ── Filtering & Search ─────────────────────────────────────────────
        private void ApplyFilters()
        {
            var query = SearchEntry.Text?.Trim() ?? "";

            var filtered = _allReports.Where(r =>
            {
                if (_activeTypeFilter == "lost" && r.ReportType != "lost") return false;
                if (_activeTypeFilter == "found" && r.ReportType != "found") return false;

                if (!string.IsNullOrEmpty(_activeStatusFilter) &&
                    !r.Status.Equals(_activeStatusFilter, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.IsNullOrEmpty(query))
                {
                    bool match =
                        r.ItemName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.Location.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.ReporterName.Contains(query, StringComparison.OrdinalIgnoreCase);
                    if (!match) return false;
                }

                return true;
            }).ToList();

            RenderCards(filtered);
        }

        private void RenderCards(List<ReportsItems> reports)
        {
            ReportsContainer.Children.Clear();

            if (reports.Count == 0)
            {
                EmptyState.IsVisible = true;
                return;
            }

            EmptyState.IsVisible = false;

            foreach (var r in reports)
                ReportsContainer.Children.Add(BuildCard(r));
        }

        // ── Modal Overlay ──────────────────────────────────────────────────
        private void ShowDetailModal(ReportsItems report)
        {
            bool isLost = report.ReportType.Equals("lost", StringComparison.OrdinalIgnoreCase);
            var accentColor = isLost ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32");
            var badgeBg = isLost ? Color.FromArgb("#FFEBEE") : Color.FromArgb("#E8F5E9");
            var badgeText = isLost ? "LOST" : "FOUND";

            (Color statusBg, Color statusFg, string statusLabel) = report.Status.ToLower() switch
            {
                "matched" => (Color.FromArgb("#E3F2FD"), Color.FromArgb("#1976D2"), "MATCHED"),
                "closed" => (Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"), "CLOSED"),
                "open" => (Color.FromArgb("#F3E5F5"), Color.FromArgb("#7B1FA2"), "OPEN"),
                _ => (Color.FromArgb("#FFF8E1"), Color.FromArgb("#F9A825"), "PENDING"),
            };

            // ── Dismiss logic ────────────────────────────────────────────
            Grid overlay = null!;
            void Dismiss()
            {
                if (Content is Grid root)
                    root.Children.Remove(overlay);
            }

            // ── Header ───────────────────────────────────────────────────
            var closeX = new Label
            {
                Text = "✕",
                FontSize = 18,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
            };
            closeX.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(Dismiss)
            });

            var headerGrid = new Grid
            {
                BackgroundColor = accentColor,
                Padding = new Thickness(20, 16),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                }
            };

            var headerLeft = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text              = report.ItemName,
                        FontSize          = 19,
                        FontAttributes    = FontAttributes.Bold,
                        TextColor         = Colors.White,
                        LineBreakMode     = LineBreakMode.WordWrap,
                    },
                    new Frame
                    {
                        BackgroundColor   = badgeBg,
                        CornerRadius      = 10,
                        Padding           = new Thickness(10, 3),
                        HasShadow         = false,
                        HorizontalOptions = LayoutOptions.Start,
                        Content = new Label
                        {
                            Text           = badgeText,
                            FontSize       = 11,
                            TextColor      = accentColor,
                            FontAttributes = FontAttributes.Bold,
                        }
                    }
                }
            };

            Grid.SetColumn(headerLeft, 0);
            Grid.SetColumn(closeX, 1);
            headerGrid.Children.Add(headerLeft);
            headerGrid.Children.Add(closeX);

            // ── Detail row helper ────────────────────────────────────────
            View DetailRow(string icon, string label, string value)
            {
                var rowGrid = new Grid
                {
                    Padding = new Thickness(20, 11),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = 30 },
                        new ColumnDefinition { Width = 90 },
                        new ColumnDefinition(GridLength.Star),
                    }
                };

                var iconLbl = new Label { Text = icon, FontSize = 15, VerticalOptions = LayoutOptions.Center };
                var keyLbl = new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#999"), VerticalOptions = LayoutOptions.Center };
                var valLbl = new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222"), LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center };

                Grid.SetColumn(iconLbl, 0);
                Grid.SetColumn(keyLbl, 1);
                Grid.SetColumn(valLbl, 2);
                rowGrid.Children.Add(iconLbl);
                rowGrid.Children.Add(keyLbl);
                rowGrid.Children.Add(valLbl);

                return new VerticalStackLayout
                {
                    Children =
                    {
                        rowGrid,
                        new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F0F0F0"), Margin = new Thickness(20, 0) }
                    }
                };
            }

            // ── Status chip row ──────────────────────────────────────────
            var statusGrid = new Grid
            {
                Padding = new Thickness(20, 11),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = 30 },
                    new ColumnDefinition { Width = 90 },
                    new ColumnDefinition(GridLength.Star),
                }
            };
            var sIconLbl = new Label { Text = "📌", FontSize = 15, VerticalOptions = LayoutOptions.Center };
            var sKeyLbl = new Label { Text = "Status", FontSize = 12, TextColor = Color.FromArgb("#999"), VerticalOptions = LayoutOptions.Center };
            var sChip = new Frame
            {
                BackgroundColor = statusBg,
                CornerRadius = 12,
                Padding = new Thickness(12, 4),
                HasShadow = false,
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label { Text = statusLabel, FontSize = 12, TextColor = statusFg, FontAttributes = FontAttributes.Bold }
            };
            Grid.SetColumn(sIconLbl, 0);
            Grid.SetColumn(sKeyLbl, 1);
            Grid.SetColumn(sChip, 2);
            statusGrid.Children.Add(sIconLbl);
            statusGrid.Children.Add(sKeyLbl);
            statusGrid.Children.Add(sChip);

            // ── Details body ─────────────────────────────────────────────
            var detailsStack = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    DetailRow("🏷", "Category", report.Category),
                    DetailRow("📍", "Location",  string.IsNullOrWhiteSpace(report.Location) ? "—" : report.Location),
                    DetailRow("📅", "Date",      report.DateReported.ToString("MMM dd, yyyy  HH:mm")),
                    DetailRow("👤", "Reporter",  report.ReporterName),
                    DetailRow("🔢", "Report ID", $"#{report.ReportId:D6}"),
                    statusGrid,
                }
            };

            if (!string.IsNullOrWhiteSpace(report.Description))
            {
                detailsStack.Children.Add(new BoxView { HeightRequest = 8, BackgroundColor = Color.FromArgb("#F5F5F5") });
                detailsStack.Children.Add(new VerticalStackLayout
                {
                    Padding = new Thickness(20, 12),
                    Spacing = 6,
                    Children =
                    {
                        new Label { Text = "📝  Description", FontSize = 12, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Bold },
                        new Label { Text = report.Description, FontSize = 13, TextColor = Color.FromArgb("#444"), LineBreakMode = LineBreakMode.WordWrap }
                    }
                });
            }

            // ── Close footer button ──────────────────────────────────────
            var closeBtn = new Frame
            {
                BackgroundColor = accentColor,
                CornerRadius = 14,
                Padding = new Thickness(0, 14),
                HasShadow = false,
                Margin = new Thickness(20, 14, 20, 20),
                Content = new Label
                {
                    Text = "Close",
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                }
            };
            closeBtn.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Dismiss) });

            // ── Assemble card ────────────────────────────────────────────
            var card = new Frame
            {
                BackgroundColor = Colors.White,
                CornerRadius = 20,
                HasShadow = true,
                Padding = 0,
                Margin = new Thickness(24, 0),
                Content = new VerticalStackLayout
                {
                    Children =
                    {
                        headerGrid,
                        new ScrollView { Content = detailsStack, MaximumHeightRequest = 380 },
                        closeBtn,
                    }
                }
            };

            // ── Overlay ──────────────────────────────────────────────────
            overlay = new Grid
            {
                BackgroundColor = Color.FromArgb("#99000000"),
                ZIndex = 99,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                Children =
                {
                    new Grid
                    {
                        VerticalOptions = LayoutOptions.Center,
                        Children        = { card }
                    }
                }
            };

            // Tap outside the card to dismiss
            overlay.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Dismiss) });

            if (Content is Grid pageRoot)
                pageRoot.Children.Add(overlay);
        }

        // ── Card Builder ───────────────────────────────────────────────────
        private View BuildCard(ReportsItems r)
        {
            bool isLost = r.ReportType.Equals("lost", StringComparison.OrdinalIgnoreCase);

            var badgeBg = isLost ? Color.FromArgb("#FFEBEE") : Color.FromArgb("#E8F5E9");
            var badgeFg = isLost ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32");
            var badgeText = isLost ? "LOST" : "FOUND";

            (Color statusBg, Color statusFg, string statusLabel) = r.Status.ToLower() switch
            {
                "matched" => (Color.FromArgb("#E3F2FD"), Color.FromArgb("#1976D2"), "MATCHED"),
                "closed" => (Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"), "CLOSED"),
                "open" => (Color.FromArgb("#F3E5F5"), Color.FromArgb("#7B1FA2"), "OPEN"),
                _ => (Color.FromArgb("#FFF8E1"), Color.FromArgb("#F9A825"), "PENDING"),
            };

            // Row 1: title + type badge
            var row1 = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                }
            };
            var titleLabel = new Label
            {
                Text = r.ItemName,
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222"),
            };
            var typeBadge = new Frame
            {
                BackgroundColor = badgeBg,
                CornerRadius = 10,
                Padding = new Thickness(9, 3),
                HasShadow = false,
                Content = new Label { Text = badgeText, FontSize = 10, TextColor = badgeFg, FontAttributes = FontAttributes.Bold }
            };
            Grid.SetColumn(titleLabel, 0);
            Grid.SetColumn(typeBadge, 1);
            row1.Children.Add(titleLabel);
            row1.Children.Add(typeBadge);

            // Description
            var descLabel = new Label
            {
                Text = string.IsNullOrWhiteSpace(r.Description) ? $"Category: {r.Category}" : r.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#666"),
                LineBreakMode = LineBreakMode.WordWrap,
            };

            // Meta rows
            var meta = new VerticalStackLayout { Spacing = 6 };
            meta.Children.Add(MetaRow("📍", string.IsNullOrWhiteSpace(r.Location) ? "—" : r.Location));
            meta.Children.Add(MetaRow("📅", r.DateReported.ToString("yyyy-MM-dd 'at' HH:mm")));
            meta.Children.Add(MetaRow("👤", r.ReporterName));
            meta.Children.Add(MetaRow("🏷", $"Category: {r.Category}"));

            // Bottom row: status + view
            var bottomRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                }
            };
            var statusChip = new Frame
            {
                BackgroundColor = statusBg,
                CornerRadius = 14,
                Padding = new Thickness(12, 5),
                HasShadow = false,
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label { Text = statusLabel, FontSize = 11, TextColor = statusFg, FontAttributes = FontAttributes.Bold }
            };
            var viewBtn = new Frame
            {
                BackgroundColor = Color.FromArgb("#FFF0F0"),
                CornerRadius = 14,
                Padding = new Thickness(12, 5),
                HasShadow = false,
                Content = new HorizontalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label { Text = "👁", FontSize = 12 },
                        new Label { Text = "View", FontSize = 12, TextColor = Color.FromArgb("#C62828"), FontAttributes = FontAttributes.Bold },
                    }
                }
            };
            viewBtn.GestureRecognizers.Add(new TapGestureRecognizer
            {
                CommandParameter = r,
                Command = new Command<ReportsItems>(report => ShowDetailModal(report))
            });

            Grid.SetColumn(statusChip, 0);
            Grid.SetColumn(viewBtn, 1);
            bottomRow.Children.Add(statusChip);
            bottomRow.Children.Add(viewBtn);

            // Left accent bar
            var accentBar = new BoxView
            {
                BackgroundColor = isLost ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32"),
                WidthRequest = 4,
                CornerRadius = 4,
                VerticalOptions = LayoutOptions.Fill,
            };
            var cardContent = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { row1, descLabel, meta, bottomRow }
            };
            var innerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                ColumnSpacing = 10,
            };
            Grid.SetColumn(accentBar, 0);
            Grid.SetColumn(cardContent, 1);
            innerGrid.Children.Add(accentBar);
            innerGrid.Children.Add(cardContent);

            return new Frame
            {
                BackgroundColor = Colors.White,
                CornerRadius = 14,
                Padding = new Thickness(16, 14),
                HasShadow = true,
                Content = innerGrid,
            };
        }

        private static HorizontalStackLayout MetaRow(string icon, string text) =>
            new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = icon, FontSize = 13, VerticalOptions = LayoutOptions.Center },
                    new Label { Text = text, FontSize = 12, TextColor = Color.FromArgb("#666"), VerticalOptions = LayoutOptions.Center },
                }
            };

        // ── Search Events ──────────────────────────────────────────────────
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            LblClear.IsVisible = !string.IsNullOrEmpty(e.NewTextValue);
            ApplyFilters();
        }

        private void OnClearSearch(object sender, TappedEventArgs e)
        {
            SearchEntry.Text = "";
            LblClear.IsVisible = false;
            ApplyFilters();
        }

        // ── Filter Chip Helpers ────────────────────────────────────────────
        private void ResetTypeChips()
        {
            foreach (var chip in new[] { ChipAll, ChipLost, ChipFound })
            {
                chip.BackgroundColor = Colors.White;
                ((Label)chip.Content).TextColor = Color.FromArgb("#888");
                ((Label)chip.Content).FontAttributes = FontAttributes.None;
            }
        }

        private void ResetStatusChips()
        {
            foreach (var chip in new[] { ChipPending, ChipMatched, ChipOpen })
            {
                chip.BackgroundColor = Colors.White;
                ((Label)chip.Content).TextColor = Color.FromArgb("#888");
            }
        }

        private void ActivateTypeChip(Frame chip)
        {
            ResetTypeChips();
            chip.BackgroundColor = Color.FromArgb("#B71C1C");
            ((Label)chip.Content).TextColor = Colors.White;
            ((Label)chip.Content).FontAttributes = FontAttributes.Bold;
        }

        private void ActivateStatusChip(Frame chip)
        {
            ResetStatusChips();
            chip.BackgroundColor = Color.FromArgb("#FFEBEE");
            ((Label)chip.Content).TextColor = Color.FromArgb("#B71C1C");
        }

        // ── Filter Chip Events ─────────────────────────────────────────────
        private void OnFilterAll(object sender, TappedEventArgs e)
        {
            _activeTypeFilter = "all";
            ActivateTypeChip(ChipAll);
            ApplyFilters();
        }

        private void OnFilterLost(object sender, TappedEventArgs e)
        {
            _activeTypeFilter = "lost";
            ActivateTypeChip(ChipLost);
            ApplyFilters();
        }

        private void OnFilterFound(object sender, TappedEventArgs e)
        {
            _activeTypeFilter = "found";
            ActivateTypeChip(ChipFound);
            ApplyFilters();
        }

        private void OnFilterPending(object sender, TappedEventArgs e)
        {
            if (_activeStatusFilter == "pending") { _activeStatusFilter = ""; ResetStatusChips(); }
            else { _activeStatusFilter = "pending"; ActivateStatusChip(ChipPending); }
            ApplyFilters();
        }

        private void OnFilterMatched(object sender, TappedEventArgs e)
        {
            if (_activeStatusFilter == "matched") { _activeStatusFilter = ""; ResetStatusChips(); }
            else { _activeStatusFilter = "matched"; ActivateStatusChip(ChipMatched); }
            ApplyFilters();
        }

        private void OnFilterOpen(object sender, TappedEventArgs e)
        {
            if (_activeStatusFilter == "open") { _activeStatusFilter = ""; ResetStatusChips(); }
            else { _activeStatusFilter = "open"; ActivateStatusChip(ChipOpen); }
            ApplyFilters();
        }

        // ── Nav Events ─────────────────────────────────────────────────────
        private async void OnHomeTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushModalAsync(new TeacherDashboard());
        }

        private async void OnReportItemTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushModalAsync(new FileReport());
        }

        private async void OnReturnTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushModalAsync(new ReturnModule());
        }

        private async void OnSettingsTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushModalAsync(new Settings());
        }
    }
}