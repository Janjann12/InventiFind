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
                "SELECT image FROM item_reports WHERE report_id = @id", conn);

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
            // keep placeholder if no image or load fails
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
            $"Confirm ownership of '{_pair.ItemName}' and mark as returned?",
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

            int adminId = Preferences.Get("UserID", 0);

            // 1) Update selected match
            const string updateMatch = """
                UPDATE matches
                SET match_status = @status,
                    verified_by = @verifiedBy
                WHERE lost_report_id = @lostId
                  AND found_report_id = @foundId
            """;

            await using (var matchCmd = new MySqlCommand(updateMatch, conn))
            {
                matchCmd.Parameters.AddWithValue("@status", newStatus);
                matchCmd.Parameters.AddWithValue("@verifiedBy", adminId == 0 ? DBNull.Value : adminId);
                matchCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                matchCmd.Parameters.AddWithValue("@foundId", _pair.SurrenderedId);
                await matchCmd.ExecuteNonQueryAsync();
            }

            if (newStatus == "confirmed")
            {
                // 2) Mark both reports as claimed
                const string updateReports = """
                    UPDATE item_reports
                    SET status = 'claimed'
                    WHERE report_id = @lostId OR report_id = @foundId
                """;

                await using (var reportCmd = new MySqlCommand(updateReports, conn))
                {
                    reportCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                    reportCmd.Parameters.AddWithValue("@foundId", _pair.SurrenderedId);
                    await reportCmd.ExecuteNonQueryAsync();
                }

                // 3) Get owner user_id from the lost report
                int returnedTo = 0;
                const string ownerSql = """
                    SELECT user_id
                    FROM item_reports
                    WHERE report_id = @lostId
                """;

                await using (var ownerCmd = new MySqlCommand(ownerSql, conn))
                {
                    ownerCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                    var ownerResult = await ownerCmd.ExecuteScalarAsync();
                    returnedTo = ownerResult == null ? 0 : Convert.ToInt32(ownerResult);
                }

                // 4) Get match_id for returns table
                int matchId = 0;
                const string getMatchIdSql = """
                    SELECT match_id
                    FROM matches
                    WHERE lost_report_id = @lostId
                      AND found_report_id = @foundId
                    LIMIT 1
                """;

                await using (var getMatchCmd = new MySqlCommand(getMatchIdSql, conn))
                {
                    getMatchCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                    getMatchCmd.Parameters.AddWithValue("@foundId", _pair.SurrenderedId);
                    var matchResult = await getMatchCmd.ExecuteScalarAsync();
                    matchId = matchResult == null ? 0 : Convert.ToInt32(matchResult);
                }

                // 5) Insert into returns
                if (matchId > 0 && returnedTo > 0)
                {
                    const string insertReturn = """
                        INSERT INTO returns (match_id, returned_to, released_by, return_date, notes)
                        VALUES (@matchId, @returnedTo, @releasedBy, NOW(), @notes)
                    """;

                    await using var returnCmd = new MySqlCommand(insertReturn, conn);
                    returnCmd.Parameters.AddWithValue("@matchId", matchId);
                    returnCmd.Parameters.AddWithValue("@returnedTo", returnedTo);
                    returnCmd.Parameters.AddWithValue("@releasedBy", adminId == 0 ? DBNull.Value : adminId);
                    returnCmd.Parameters.AddWithValue("@notes", $"Verified return for {_pair.ItemName}");
                    await returnCmd.ExecuteNonQueryAsync();
                }

                // 6) Reject other pending matches involving either report
                const string rejectOthers = """
                    UPDATE matches
                    SET match_status = 'rejected'
                    WHERE match_status = 'pending'
                      AND (lost_report_id = @lostId OR found_report_id = @foundId
                           OR lost_report_id = @foundId OR found_report_id = @lostId)
                      AND NOT (lost_report_id = @lostId AND found_report_id = @foundId)
                """;

                await using (var rejectCmd = new MySqlCommand(rejectOthers, conn))
                {
                    rejectCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                    rejectCmd.Parameters.AddWithValue("@foundId", _pair.SurrenderedId);
                    await rejectCmd.ExecuteNonQueryAsync();
                }
            }

            await DisplayAlert(
                "Done",
                newStatus == "confirmed"
                    ? "Match confirmed and return recorded."
                    : "Match rejected.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not update status:\n{ex.Message}", "OK");
        }
    }
}