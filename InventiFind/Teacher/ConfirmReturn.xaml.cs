using MySqlConnector;

namespace InventiFind;

public partial class ConfirmReturn : ContentPage
{
    private readonly MatchPair _pair;

    public ConfirmReturn(MatchPair pair)
    {
        InitializeComponent();
        _pair = pair;
        PopulateUI();
    }

    private void PopulateUI()
    {
        ItemNameLabel.Text = _pair.ItemName;
        OwnerNameLabel.Text = $"Owner: {_pair.ReporterName}";
        ClaimCodeLabel.Text = $"Claim Code: {_pair.LostReportNo}";
        CategoryLabel.Text = $"Category: {_pair.Category}";
        DateLabel.Text = $"Matched: {_pair.SubmittedDate}";
    }

    private async Task ProcessReturnAsync()
    {
        try
        {
            await using var conn =
                new MySqlConnection(DatabaseConfig.ConnectionString);

            await conn.OpenAsync();

            // 1) Get match_id
            int matchId = 0;

            const string getMatchSql = """
                SELECT match_id
                FROM matches
                WHERE lost_report_id = @lostId
                  AND found_report_id = @foundId
                LIMIT 1
            """;

            await using (var matchCmd = new MySqlCommand(getMatchSql, conn))
            {
                matchCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                matchCmd.Parameters.AddWithValue("@foundId", _pair.SurrenderedId);

                var result = await matchCmd.ExecuteScalarAsync();

                if (result != null)
                    matchId = Convert.ToInt32(result);
            }

            // 2) Get owner user_id
            int returnedTo = 0;

            const string ownerSql = """
                SELECT user_id
                FROM item_reports
                WHERE report_id = @lostId
                LIMIT 1
            """;

            await using (var ownerCmd = new MySqlCommand(ownerSql, conn))
            {
                ownerCmd.Parameters.AddWithValue("@lostId", _pair.LostId);

                var result = await ownerCmd.ExecuteScalarAsync();

                if (result != null)
                    returnedTo = Convert.ToInt32(result);
            }

            // 3) Insert into returns table
            int releasedBy = Preferences.Get("UserID", 0);

            const string insertReturnSql = """
                INSERT INTO returns
                (match_id, returned_to, released_by, return_date, notes)
                VALUES
                (@matchId, @returnedTo, @releasedBy, NOW(), @notes)
            """;

            await using (var returnCmd = new MySqlCommand(insertReturnSql, conn))
            {
                returnCmd.Parameters.AddWithValue("@matchId", matchId);
                returnCmd.Parameters.AddWithValue("@returnedTo", returnedTo);
                returnCmd.Parameters.AddWithValue(
                    "@releasedBy",
                    releasedBy == 0 ? DBNull.Value : releasedBy
                );
                returnCmd.Parameters.AddWithValue(
                    "@notes",
                    $"Released item: {_pair.ItemName}"
                );

                await returnCmd.ExecuteNonQueryAsync();
            }

            // 4) Update report status
            const string updateSql = """
                UPDATE item_reports
                SET status = 'claimed'
                WHERE report_id = @lostId
                   OR report_id = @foundId
            """;

            await using (var updateCmd = new MySqlCommand(updateSql, conn))
            {
                updateCmd.Parameters.AddWithValue("@lostId", _pair.LostId);
                updateCmd.Parameters.AddWithValue("@foundId", _pair.SurrenderedId);

                await updateCmd.ExecuteNonQueryAsync();
            }

            await DisplayAlert(
                "Success",
                "Item successfully released.",
                "OK"
            );

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // Back button / close modal
    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    // Confirm release button
    private async void OnReleaseTapped(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Release Item",
            $"Release {_pair.ItemName}?",
            "Release",
            "Cancel"
        );

        if (!confirm)
            return;

        await ProcessReturnAsync();
    }
}