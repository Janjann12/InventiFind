using MySqlConnector;

namespace InventiFind;

public partial class VerifyDetailPage : ContentPage
{
    private readonly MatchPair _pair;

    // ── Constructor ────────────────────────────────────────────────────────

    public VerifyDetailPage(MatchPair pair)
    {
        InitializeComponent();
        _pair = pair;
        PopulateUI();
    }

    // ── Populate ───────────────────────────────────────────────────────────

    private void PopulateUI()
    {
        // Header
        ItemNameLabel.Text = _pair.ItemName.ToUpper();
        StatusBadgeLabel.Text = _pair.Status;
        StatusBadgeFrame.BackgroundColor = _pair.StatusBadgeColor;

        // Reporter
        ReporterNameLabel.Text = _pair.ReporterName.Split(' ').FirstOrDefault() ?? _pair.ReporterName;
        ReporterIdLabel.Text = _pair.LostReportNo;    // use report no as student ID stand-in
           // use category as course stand-in

        // Report numbers
        LostReportNoLabel.Text = _pair.LostReportNo;
        SurrenderedNoLabel.Text = _pair.SurrenderedNo;

        // Ownership proof
        OwnershipProofLabel.Text = _pair.OwnershipProof;

        // Submitted date
        SubmittedDateLabel.Text = $"Submitted {_pair.SubmittedDate}";

        // Proof image — load from DB blob if available
        _ = LoadProofImageAsync();
    }

    private async Task LoadProofImageAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT image FROM items WHERE L_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", _pair.LostId);

            var result = await cmd.ExecuteScalarAsync();
            if (result is byte[] blob && blob.Length > 0)
            {
                ProofImage.Source = ImageSource.FromStream(() => new MemoryStream(blob));
                ProofImage.IsVisible = true;
            }
        }
        catch
        {
            // Silently fail — placeholder stays visible
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private async void OnBackdropTapped(object sender, TappedEventArgs e)
        => await Navigation.PopModalAsync(animated: true);

    private async void OnRejectTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Reject Match",
            $"Reject the match for '{_pair.ItemName}'?",
            "Yes, Reject", "Cancel");

        if (!confirm) return;

        await UpdateStatusAsync("Rejected");
        await Navigation.PopModalAsync(animated: true);
    }

    private async void OnConfirmTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Confirm Match",
            $"Confirm ownership of '{_pair.ItemName}' and mark as resolved?",
            "Yes, Confirm", "Cancel");

        if (!confirm) return;

        await UpdateStatusAsync("Confirmed");
        await Navigation.PopModalAsync(animated: true);
    }

    private async Task UpdateStatusAsync(string newStatus)
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // Update description as status proxy (replace with a real status column)
            const string sql = """
                UPDATE items
                SET description = CONCAT('[{0}] ', description)
                WHERE L_ID = @id
            """;

            await using var cmd = new MySqlCommand(
                sql.Replace("{0}", newStatus), conn);
            cmd.Parameters.AddWithValue("@id", _pair.LostId);
            await cmd.ExecuteNonQueryAsync();

            await DisplayAlert("Done", $"Item marked as {newStatus}.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not update status:\n{ex.Message}", "OK");
        }
    }
}