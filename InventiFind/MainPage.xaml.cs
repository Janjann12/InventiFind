namespace InventiFind;

public partial class MainPage : ContentPage
{
    private string detectedRole = "User";

    public MainPage()
    {
        InitializeComponent();
    }

    // ==========================================
    // ROLE DETECTION - Auto-detect from email
    // ==========================================
    private void OnEmailTextChanged(object sender, TextChangedEventArgs e)
    {
        string email = e.NewTextValue?.ToLower() ?? "";

        if (string.IsNullOrWhiteSpace(email))
        {
            RoleDisplayFrame.IsVisible = false;  // Hide role display if email is empty
            UpdateLoginButtonText("User");        // Default to "Login as User"
            return;
        }

        // Detect role based on email pattern
        if (email.Contains("@admin.") || email.Contains(".admin@") || email.EndsWith("@ue.edu.admin"))
        {
        }
        else if (email.Contains("@teacher.") || email.Contains(".teacher@") || email.EndsWith("@ue.edu.teacher") || email.Contains("@prof."))
        {
        }
        else
        {
        }

    }

    // ==========================================
    // UPDATE LOGIN BUTTON TEXT
    // ==========================================
    private void UpdateLoginButtonText(string role)
    {
        LoginButton.Text = $"Login as {role}";
    }

    // ==========================================
    // LOGIN BUTTON CLICKED
    // ==========================================
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string email = EmailEntry.Text;
        string password = PasswordEntry.Text;

        // Validation: Check if fields are empty
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please enter both email and password", "OK");
            return;
        }

        // Validate credentials (replace with real API)
        bool isValid = await ValidateCredentials(email, password, detectedRole);

        if (isValid)
        {
            // Success: Show welcome message then navigate
            await DisplayAlert("Success", $"Welcome back! Logged in as {detectedRole}", "OK");
            await NavigateBasedOnRole(detectedRole);  // Go to role-specific page
        }
        else
        {
            await DisplayAlert("Login Failed", "Invalid email or password", "OK");
        }
    }

    // ==========================================
    // VALIDATE CREDENTIALS (TODO: Replace with API)
    // ==========================================
    private async Task<bool> ValidateCredentials(string email, string password, string role)
    {
        // TODO: Replace with your actual authentication API
        await Task.Delay(500); // Simulate network delay
        return email.Contains("@") && password.Length > 5;  // Demo validation
    }

    // ==========================================
    // NAVIGATE TO ROLE-SPECIFIC DASHBOARD
    // ==========================================
    private async Task NavigateBasedOnRole(string role)
    {
        switch (role)
        {
            case "Admin":
                 await Navigation.PushAsync(new AdminDashboard());
                break;

            case "Teacher":
                // TODO: Replace with actual navigation
                 await Navigation.PushAsync(new TeacherDashboard());
                break;

            default:  // 
                // TODO: Replace with actual navigation
                 await Navigation.PushAsync(new StudentDashboard());
                break;
        }
    }

    // ==========================================
    // GOOGLE SIGN IN TAP
    // ==========================================
    private async void OnGoogleSignInTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // TODO: Implement Google Authentication
            await DisplayAlert("Google Sign-In", "Opening Google authentication...", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Google sign-in failed: {ex.Message}", "OK");
        }
    }

    // ==========================================
    // FORGOT PASSWORD TAP
    // ==========================================
    private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Forgot Password",
            $"Password reset link will be sent to your email for {detectedRole} account.", "OK");
    }

    // ==========================================
    // SIGN UP TAP
    // ==========================================
    private async void OnSignUpTapped(object sender, TappedEventArgs e)
    {
        // TODO: Navigate to registration page
        await Navigation.PushAsync(new SignUpPage());
    }
}