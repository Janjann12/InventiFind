using MySqlConnector;

namespace InventiFind;

public partial class VerifyDetailPage : ContentPage
{
    private readonly MatchPair _pair;

    public VerifyDetailPage(MatchPair pair)
    {
        InitializeComponent();
        _pair = pair;
        PopulateUI();
    }

    private void PopulateUI()
    {
        ItemNameLabel.Text = _pair.ItemName.ToUpper();
        StatusBadgeLabel.Text = _pair.Status;
        StatusBadgeFrame.BackgroundColor = _pair.StatusBadgeColor;

        ReporterNameLabel.Text = _pair.ReporterName.Split(' ').FirstOrDefault() ?? _pair.ReporterName;
        ReporterIdLabel.Text = _pair.LostReportNo;
        ReporterCourseLabel.Text = _pair.Category;
        LostReportNoLabel.Text = _pair.LostReportNo;
        SurrenderedNoLabel.Text = _pair.SurrenderedNo;

        OwnershipProofLabel.Text = _pair.OwnershipProof;
        SubmittedDateLabel.Text = $"Submitted {_pair.SubmittedDate}";

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

    private async void OnBackdropTapped(object sender, TappedEventArgs e)
        => await Navigation.PopModalAsync(animated: true);

    private async void OnRejectTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Reject Match",
            $"Reject the match for '{_pair.ItemName}'?",
            "Yes, Reject", "Cancel");

        if (!confirm) return;

        await UpdateStatusAsync("rejected");
        await Navigation.PopModalAsync(animated: true);
    }

    private async void OnConfirmTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Confirm Match",
            $"Confirm ownership of '{_pair.ItemName}' and mark as resolved?",
            "Yes, Confirm", "Cancel");

        if (!confirm) return;

        await UpdateStatusAsync("confirmed");
        await Navigation.PopModalAsync(animated: true);
    }

    private async Task UpdateStatusAsync(string newStatus)
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // 1. Update the match row status
            const string updateMatch = """
                UPDATE matches
                SET status = @status
                WHERE lost_id = @lostId
                  AND surrendered_id = @surrenderedId
            """;

            await using var matchCmd = new MySqlCommand(updateMatch, conn);
            matchCmd.Parameters.AddWithValue("@status", newStatus);
            matchCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
            matchCmd.Parameters.AddWithValue("@surrenderedId", _pair.SurrenderedId);
            await matchCmd.ExecuteNonQueryAsync();

            if (newStatus == "confirmed")
            {
                // 2. Mark both items as resolved so they stop
                //    appearing in future matches
                const string updateItems = """
                    UPDATE items
                    SET status = 'resolved'
                    WHERE L_ID IN (@lostId, @surrenderedId)
                """;

                await using var itemsCmd = new MySqlCommand(updateItems, conn);
                itemsCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                itemsCmd.Parameters.AddWithValue("@surrenderedId", _pair.SurrenderedId);
                await itemsCmd.ExecuteNonQueryAsync();

                // 3. Auto-reject all other pending matches that involve
                //    either of these two items so the admin doesn't see
                //    stale matches for already-resolved items
                const string rejectOthers = """
                    UPDATE matches
                    SET status = 'rejected'
                    WHERE status = 'pending'
                      AND (lost_id = @lostId OR surrendered_id = @surrenderedId)
                      AND NOT (lost_id = @lostId AND surrendered_id = @surrenderedId)
                """;

                await using var rejectCmd = new MySqlCommand(rejectOthers, conn);
                rejectCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                rejectCmd.Parameters.AddWithValue("@surrenderedId", _pair.SurrenderedId);
                await rejectCmd.ExecuteNonQueryAsync();
            }

            await DisplayAlert("Done",
                newStatus == "confirmed"
                    ? "Match confirmed. Both items marked as resolved."
                    : "Match rejected.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not update status:\n{ex.Message}", "OK");
        }
    }
}