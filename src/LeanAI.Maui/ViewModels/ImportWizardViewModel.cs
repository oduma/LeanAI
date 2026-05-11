using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeanAI.Application.WeightManagement.Commands.ImportGoogleSheets;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Queries.GetGoogleSpreadsheets;
using LeanAI.Application.WeightManagement.Queries.GetSheetColumns;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Infrastructure.Services;
using MediatR;

namespace LeanAI.Maui.ViewModels;

public partial class ImportWizardViewModel : ObservableObject
{
    // Replace with real values from Google Cloud Console
    private const string GoogleClientId   = "307869214559-0pelc79bt9v5stgc9m315rrhsalk6sud.apps.googleusercontent.com";
    private const string GoogleRedirectUri = "com.googleusercontent.apps.307869214559-0pelc79bt9v5stgc9m315rrhsalk6sud:/oauth2redirect";
    private const string GoogleAuthBase    = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleScope       = "https://www.googleapis.com/auth/spreadsheets.readonly https://www.googleapis.com/auth/drive.readonly";

    private readonly IMediator          _mediator;
    private readonly GoogleOAuthService _oauthService;

    private string? _accessToken;
    private string? _codeVerifier;

    [ObservableProperty] private int    _currentStep = 1;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Step 2 — spreadsheet list
    public ObservableCollection<SpreadsheetSummary> Spreadsheets { get; } = [];
    [ObservableProperty] private SpreadsheetSummary? _selectedSpreadsheet;

    // Step 3 — column mapping
    public ObservableCollection<string> Columns { get; } = [];
    [ObservableProperty] private string? _selectedDateColumn;
    [ObservableProperty] private string? _selectedWeightColumn;
    [ObservableProperty] private string? _selectedNotesColumn;
    [ObservableProperty] private bool    _isImperial;

    // Step 4 — confirmation preview text
    [ObservableProperty] private string _confirmationDetails = string.Empty;

    // Step 5 — import summary
    [ObservableProperty] private ImportSummaryDto? _importSummary;

    public ImportWizardViewModel(IMediator mediator, GoogleOAuthService oauthService)
    {
        _mediator     = mediator;
        _oauthService = oauthService;
    }

    // ─── Step 1: Sign In ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        IsBusy      = true;
        ErrorMessage = string.Empty;
        try
        {
            // Check for stored refresh token first
            var stored = await _oauthService.GetRefreshTokenAsync();
            if (stored is not null)
            {
                _accessToken = await _oauthService.RefreshAccessTokenAsync(GoogleClientId, stored);
                await LoadSpreadsheetsAsync();
                CurrentStep = 2;
                return;
            }

            // New OAuth flow with PKCE
            _codeVerifier = GenerateCodeVerifier();
            var challenge = GenerateCodeChallenge(_codeVerifier);

            var authUrl = $"{GoogleAuthBase}" +
                          $"?client_id={Uri.EscapeDataString(GoogleClientId)}" +
                          $"&redirect_uri={Uri.EscapeDataString(GoogleRedirectUri)}" +
                          $"&response_type=code" +
                          $"&scope={Uri.EscapeDataString(GoogleScope)}" +
                          $"&code_challenge={challenge}" +
                          $"&code_challenge_method=S256" +
                          $"&access_type=offline" +
                          $"&prompt=consent";

            var result = await WebAuthenticator.AuthenticateAsync(
                new Uri(authUrl), new Uri(GoogleRedirectUri));

            var code = result.Properties["code"];
            var tokenResponse = await _oauthService.ExchangeAuthCodeAsync(
                GoogleClientId, GoogleRedirectUri, code, _codeVerifier);

            _accessToken = tokenResponse.AccessToken;
            if (tokenResponse.RefreshToken is not null)
                await _oauthService.SetRefreshTokenAsync(tokenResponse.RefreshToken);

            await LoadSpreadsheetsAsync();
            CurrentStep = 2;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ─── Step 2: Select Spreadsheet ────────────────────────────────────────────

    private async Task LoadSpreadsheetsAsync()
    {
        BusyMessage = "Loading your spreadsheets…";
        var list = await _mediator.Send(new GetGoogleSpreadsheetsQuery(_accessToken!));
        Spreadsheets.Clear();
        foreach (var s in list) Spreadsheets.Add(s);
    }

    [RelayCommand]
    private async Task SelectSpreadsheetAsync(SpreadsheetSummary spreadsheet)
    {
        SelectedSpreadsheet = spreadsheet;
        IsBusy      = true;
        ErrorMessage = string.Empty;
        try
        {
            BusyMessage = "Reading column headers…";
            var headers = await _mediator.Send(
                new GetSheetColumnsQuery(spreadsheet.Id, _accessToken!));

            Columns.Clear();
            foreach (var h in headers) Columns.Add(h);

            // Pre-select sensible defaults (case-insensitive match)
            SelectedDateColumn   = FindColumn("date")   ?? Columns.FirstOrDefault();
            SelectedWeightColumn = FindColumn("weight")  ?? Columns.Skip(1).FirstOrDefault();
            SelectedNotesColumn  = FindColumn("notes") ?? FindColumn("comment") ?? FindColumn("comments");

            CurrentStep = 3;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? FindColumn(string keyword) =>
        Columns.FirstOrDefault(c => c.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    // ─── Step 3: Column Mapping → Confirm ──────────────────────────────────────

    [RelayCommand]
    private void ProceedToConfirm()
    {
        if (SelectedDateColumn is null || SelectedWeightColumn is null)
        {
            ErrorMessage = "Date and Weight columns are required.";
            return;
        }

        var units = IsImperial ? "Imperial (lbs)" : "Metric (kg)";
        ConfirmationDetails =
            $"Spreadsheet:   {SelectedSpreadsheet!.Name}\n" +
            $"Date column:   {SelectedDateColumn}\n" +
            $"Weight column: {SelectedWeightColumn}\n" +
            $"Notes column:  {SelectedNotesColumn ?? "(none)"}\n" +
            $"Sheet units:   {units}\n\n" +
            "Your ideal weight line will be recalculated from the first to the last date in the sheet.\n" +
            "Your goal period and starting weight will be updated.\n" +
            "Existing entries with a valid (non-zero) weight from the sheet will be overwritten.";

        CurrentStep = 4;
    }

    // ─── Step 4: Confirm Import ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        ErrorMessage = string.Empty;
        CurrentStep  = 5;
        IsBusy       = true;
        BusyMessage  = "Importing your data…";
        try
        {
            var cmd = new ImportGoogleSheetsCommand(
                SpreadsheetId:   SelectedSpreadsheet!.Id,
                DateColumn:      SelectedDateColumn!,
                WeightColumn:    SelectedWeightColumn!,
                NotesColumn:     SelectedNotesColumn,
                SheetUnitSystem: IsImperial ? UnitSystem.Imperial : UnitSystem.Metric,
                AccessToken:     _accessToken!);

            ImportSummary = await _mediator.Send(cmd);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => CurrentStep = Math.Max(1, CurrentStep - 1);

    // ─── PKCE helpers ───────────────────────────────────────────────────────────

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
