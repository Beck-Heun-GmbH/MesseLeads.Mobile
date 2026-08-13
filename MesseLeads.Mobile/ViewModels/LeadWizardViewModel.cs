using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class LeadWizardViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly LocalLeadService _localLeadService;
    private readonly LookupSyncService _lookupSyncService;
    private readonly LocalLeadImageService _imageService;
    private readonly ILocalAiService _localAiService;
    private readonly ILocalVisionService _visionService;
    private readonly LeadSyncService _leadSyncService;
    private readonly VCardParserService _vCardParserService;
    private readonly LocalAiDiagnosticsService _aiDiagnosticsService;
    private readonly AuthSessionService _authSessionService;

    private const string MaterialGroupKey = "Material";
    private const string FollowUpGroupKey = "FollowUp";

    private Guid? _localLeadId;
    private LeadStartMode _startMode = LeadStartMode.Photo;
    private readonly List<LeadWizardStep> _steps = [];
    private readonly Dictionary<string, ObservableCollection<SelectableLookupItem>> _itemsByGroup = [];
    private List<LocalLookupGroup> _lookupGroups = [];
    private bool _hasLoaded;
    private bool _lookupItemsLoaded;
    private string? _savedSignature;
    private bool _aiDiagnosticsLoaded;
    private bool _openedExistingLead;
    private bool _initialPhotoSourceStarted;
    public bool IsFinishing { get; private set; }

    [ObservableProperty] private int stepIndex;
    [ObservableProperty] private LeadWizardStepKind? currentStepKind;
    [ObservableProperty] private string stepTitle = "";
    [ObservableProperty] private string progressText = "";
    [ObservableProperty] private string leadTitle = "Unbenannter Lead";

    [ObservableProperty] private bool showBusinessCardStep;
    [ObservableProperty] private bool showCropImageStep;
    [ObservableProperty] private bool showBusinessCardAnalysisStep;
    [ObservableProperty] private bool showOcrStep;
    [ObservableProperty] private bool showContactStep;
    [ObservableProperty] private bool showAddressStep;
    [ObservableProperty] private bool showSummaryStep;
    [ObservableProperty] private bool showQrScanStep;
    [ObservableProperty] private bool showQrReviewStep;
    [ObservableProperty] private string stepDescription = "";

    [ObservableProperty] private bool canGoPrevious;
    [ObservableProperty] private bool canGoNext = true;
    [ObservableProperty] private bool canFinish;
    [ObservableProperty] private bool isAdmin;
    [ObservableProperty] private bool isAnalyzingBusinessCard;
    [ObservableProperty] private bool showCameraPhotoAction = true;
    [ObservableProperty] private bool showGalleryPhotoAction = true;
    [ObservableProperty] private bool showLookupDownloadPrompt;
    [ObservableProperty] private bool isDownloadingLookups;
    [ObservableProperty] private string lookupDownloadText =
        "Es sind noch keine Stammdaten auf dem Gerät. Lade sie jetzt herunter, um diese Auswahl zu füllen.";
    [ObservableProperty] private string tradeFairHeaderText = "MesseLeads";

    [ObservableProperty] private string? firstName;
    [ObservableProperty] private string? lastName;
    [ObservableProperty] private string? company;
    [ObservableProperty] private string? jobTitle;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string? mobile;
    [ObservableProperty] private string? website;

    [ObservableProperty] private string? street;
    [ObservableProperty] private string? zipCode;
    [ObservableProperty] private string? city;
    [ObservableProperty] private string? country = "Deutschland";

    [ObservableProperty] private string? visitDay;
    [ObservableProperty] private bool wantsNewsletter;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private string? followUpNotes;

    [ObservableProperty]
    private string imageStatusText = "Noch kein Foto aufgenommen.";

    [ObservableProperty]
    private bool hasBusinessCardImage;

    [ObservableProperty]
    private string cropImagePath = "";

    [ObservableProperty]
    private string cropStatusText =
        "Ziehe die Regler so, dass nur die Visitenkarte und relevante Daten im hellen Rahmen liegen.";

    [ObservableProperty]
    private string cropSelectionText = "Ausschnitt: 90% Breite, 76% Höhe";

    [ObservableProperty]
    private double cropLeft = 0.05d;

    [ObservableProperty]
    private double cropTop = 0.12d;

    [ObservableProperty]
    private double cropRight = 0.95d;

    [ObservableProperty]
    private double cropBottom = 0.88d;

    [ObservableProperty]
    private ObservableCollection<SelectableLookupItem> currentLookupItems = [];

    [ObservableProperty]
    private bool currentGroupUsesPrintDigital;

    [ObservableProperty]
    private bool showFollowUpNotes;

    [ObservableProperty]
    private string aiStatusText = "KI noch nicht ausgeführt.";

    [ObservableProperty]
    private string ocrRawText = "";

    [ObservableProperty]
    private string aiRuntimeStatus = "Runtime wird geprüft...";

    [ObservableProperty]
    private string aiModelStatus = "Modell wird geprüft...";

    [ObservableProperty]
    private string aiModelPath = "";

    [ObservableProperty]
    private string aiEngineText = "Noch keine Analyse ausgeführt.";

    [ObservableProperty]
    private string aiTimingText = "Noch keine Zeitmessung vorhanden.";

    [ObservableProperty]
    private string aiRawResponse = "";

    [ObservableProperty]
    private string aiParsedJson = "";

    [ObservableProperty]
    private string aiErrorText = "";

    [ObservableProperty]
    private bool hasAiError;

    [ObservableProperty]
    private bool hasAiRawResponse;

    [ObservableProperty]
    private bool hasAiParsedJson;

    [ObservableProperty]
    private string qrStatusText = "Kamera wird vorbereitet...";

    [ObservableProperty]
    private string qrRawContent = "";

    [ObservableProperty]
    private bool isQrScannerActive;

    [ObservableProperty]
    private bool qrScanCompleted;

    public LocalLead? Lead { get; private set; }

    public LeadWizardViewModel(
        INavigationService navigationService,
        LocalLeadService localLeadService,
        LookupSyncService lookupSyncService,
        LocalLeadImageService imageService,
        ILocalAiService localAiService,
        ILocalVisionService visionService,
        LeadSyncService leadSyncService,
        VCardParserService vCardParserService,
        LocalAiDiagnosticsService aiDiagnosticsService,
        AuthSessionService authSessionService)
    {
        _navigationService = navigationService;
        _localLeadService = localLeadService;
        _lookupSyncService = lookupSyncService;
        _imageService = imageService;
        _localAiService = localAiService;
        _visionService = visionService;

        Title = "Lead erfassen";
        UpdateStep();
        _leadSyncService = leadSyncService;
        _vCardParserService = vCardParserService;
        _aiDiagnosticsService = aiDiagnosticsService;
        _authSessionService = authSessionService;
    }

    partial void OnCropLeftChanged(double value)
    {
        CropLeft = Math.Clamp(value, 0d, CropRight - 0.05d);
        UpdateCropSelectionText();
    }

    partial void OnCropTopChanged(double value)
    {
        CropTop = Math.Clamp(value, 0d, CropBottom - 0.05d);
        UpdateCropSelectionText();
    }

    partial void OnCropRightChanged(double value)
    {
        CropRight = Math.Clamp(value, CropLeft + 0.05d, 1d);
        UpdateCropSelectionText();
    }

    partial void OnCropBottomChanged(double value)
    {
        CropBottom = Math.Clamp(value, CropTop + 0.05d, 1d);
        UpdateCropSelectionText();
    }

    public void SetLeadId(Guid localLeadId)
    {
        _localLeadId = localLeadId;
        _openedExistingLead = true;
    }

    public void SetStartMode(LeadStartMode startMode)
    {
        _startMode = startMode;
        _initialPhotoSourceStarted = false;
        BuildSteps();
        StepIndex = 0;
        UpdateStep();
    }

    private void BuildSteps()
    {
        _steps.Clear();

        switch (_startMode)
        {
            case LeadStartMode.Photo:
            case LeadStartMode.Gallery:
                var startsWithGallery = _startMode == LeadStartMode.Gallery;

                _steps.Add(new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.BusinessCardPhoto,
                    Title = startsWithGallery
                        ? "Bild auswählen"
                        : "Visitenkarte fotografieren",
                    Description = startsWithGallery
                        ? "Wähle ein vorhandenes Foto der Visitenkarte aus der Galerie."
                        : "Nimm ein Foto der Visitenkarte auf."
                });

                _steps.Add(new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.CropImage,
                    Title = "Foto zuschneiden",
                    Description = "Grenze das Foto auf die Visitenkarte und die relevanten Daten ein."
                });

                if (IsAdmin)
                {
                    _steps.Add(new LeadWizardStep
                    {
                        Kind = LeadWizardStepKind.AnalyzeImage,
                        Title = "Visitenkarte analysieren",
                        Description = "OCR und lokale KI lesen die Daten aus der Visitenkarte."
                    });
                }
                else
                {
                    _steps.Add(new LeadWizardStep
                    {
                        Kind = LeadWizardStepKind.BusinessCardAnalysis,
                        Title = "Foto wird analysiert",
                        Description = "Das Visitenkartenfoto wird lokal ausgelesen und strukturiert."
                    });
                }

                _steps.AddRange([
                    new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.Contact,
                    Title = "Kontaktdaten prüfen",
                    Description = "Prüfe Name, Firma, Telefon, E-Mail und Webseite."
                },
                new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.Address,
                    Title = "Adresse",
                    Description = "Erfasse oder prüfe die Anschrift."
                }
                ]);
                break;

            case LeadStartMode.Qr:
                _steps.AddRange([
                    new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.QrScan,
                    Title = "QR-Code scannen",
                    Description = "Scanne den QR-Code des Messekontakts."
                },
                new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.QrReview,
                    Title = "QR-Daten prüfen",
                    Description = "Prüfe die aus dem QR-Code übernommenen Daten."
                },
                new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.Contact,
                    Title = "Kontaktdaten",
                    Description = "Ergänze oder korrigiere die Kontaktdaten."
                },
                new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.Address,
                    Title = "Adresse",
                    Description = "Erfasse oder prüfe die Anschrift."
                }
                ]);
                break;

            case LeadStartMode.Manual:
                _steps.AddRange([
                    new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.Contact,
                    Title = "Kontaktdaten",
                    Description = "Erfasse die wichtigsten Daten des Messekontakts."
                },
                new LeadWizardStep
                {
                    Kind = LeadWizardStepKind.Address,
                    Title = "Adresse",
                    Description = "Erfasse optional die Anschrift."
                }
                ]);
                break;
        }

        AddLookupGroupSteps();

        _steps.Add(new LeadWizardStep
        {
            Kind = LeadWizardStepKind.Summary,
            Title = "Zusammenfassung",
            Description = "Prüfe alles und speichere den Lead."
        });
    }

    private void AddLookupGroupSteps()
    {
        if (_lookupGroups.Count == 0)
        {
            _steps.Add(new LeadWizardStep
            {
                Kind = LeadWizardStepKind.LookupGroup,
                Title = "Auswahlbereiche",
                Description = "Die Auswahlbereiche sind noch nicht auf dem Gerät."
            });

            return;
        }

        foreach (var group in _lookupGroups)
        {
            _steps.Add(new LeadWizardStep
            {
                Kind = LeadWizardStepKind.LookupGroup,
                Title = group.Label,
                Description = group.Description ?? "",
                GroupKey = group.Key
            });
        }
    }

    private void RebuildStepsPreservingPosition()
    {
        var current = StepIndex >= 0 && StepIndex < _steps.Count
            ? _steps[StepIndex]
            : null;

        BuildSteps();

        var index = current is null
            ? -1
            : _steps.FindIndex(x => x.Kind == current.Kind && x.GroupKey == current.GroupKey);

        if (index < 0)
        {
            index = _steps.FindIndex(x => x.Kind == LeadWizardStepKind.LookupGroup);
        }

        StepIndex = Math.Max(index, 0);
        UpdateStep();
    }

    public async Task LoadAsync()
    {
        if (_hasLoaded)
        {
            UpdateStep();
            await EnsureCurrentStepDataLoadedAsync();
            return;
        }

        if (_localLeadId is null)
        {
            Lead = await _localLeadService.CreateDraftAsync();
            _localLeadId = Lead.LocalId;
        }
        else
        {
            Lead = await _localLeadService.GetAsync(_localLeadId.Value);
        }

        if (Lead is null)
        {
            Lead = await _localLeadService.CreateDraftAsync();
            _localLeadId = Lead.LocalId;
        }

        IsAdmin = await _authSessionService.IsAdminAsync();
        _lookupGroups = await _lookupSyncService.GetGroupsAsync(Lead?.TradeFairKey);
        BuildSteps();

        LoadFromLead();
        _savedSignature = BuildLeadSignature(Lead);
        await LoadImagesAsync();

        _hasLoaded = true;
        UpdateLeadTitle();
        MovePastCompletedBusinessCardFlowForExistingLead();
        UpdateStep();
        await EnsureCurrentStepDataLoadedAsync();
    }

    public async Task StartInitialPhotoSourceIfNeededAsync()
    {
        if (_initialPhotoSourceStarted || _openedExistingLead || Lead is null)
        {
            return;
        }

        if (_startMode == LeadStartMode.Photo)
        {
            _initialPhotoSourceStarted = true;
            await TakeBusinessCardPhotoAsync();
            return;
        }

        if (_startMode == LeadStartMode.Gallery)
        {
            _initialPhotoSourceStarted = true;
            await PickBusinessCardPhotoAsync();
        }
    }

    public async Task AcceptQrCodeAsync(string? rawContent)
    {
        if (QrScanCompleted || string.IsNullOrWhiteSpace(rawContent))
        {
            return;
        }

        IsQrScannerActive = false;
        QrStatusText = "QR-Code erkannt. Kontaktdaten werden übernommen...";

        try
        {
            var parsed = _vCardParserService.Parse(rawContent);
            QrRawContent = parsed.RawContent;

            if (!parsed.HasContactData)
            {
                QrStatusText = "Der QR-Code enthält keine unterstützten Kontaktdaten.";
                IsQrScannerActive = true;
                return;
            }

            FirstName = parsed.FirstName ?? FirstName;
            LastName = parsed.LastName ?? LastName;
            Company = parsed.Company ?? Company;
            JobTitle = parsed.JobTitle ?? JobTitle;
            Email = parsed.Email ?? Email;
            Phone = parsed.Phone ?? Phone;
            Mobile = parsed.Mobile ?? Mobile;
            Website = parsed.Website ?? Website;
            Street = parsed.Street ?? Street;
            ZipCode = parsed.ZipCode ?? ZipCode;
            City = parsed.City ?? City;
            Country = parsed.Country ?? Country;

            QrScanCompleted = true;
            QrStatusText = "Kontaktdaten wurden erfolgreich übernommen.";

            await SaveAsync();

            if (_steps.Count > 0 &&
                StepIndex < _steps.Count - 1 &&
                _steps[StepIndex].Kind == LeadWizardStepKind.QrScan)
            {
                StepIndex++;
                UpdateStep();
                await EnsureCurrentStepDataLoadedAsync();
            }
        }
        catch (Exception ex)
        {
            QrScanCompleted = false;
            IsQrScannerActive = true;
            QrStatusText = "QR-Code konnte nicht verarbeitet werden: " + ex.Message;
        }
    }

    public void SetQrPermissionState(bool granted)
    {
        if (QrScanCompleted)
        {
            IsQrScannerActive = false;
            return;
        }

        IsQrScannerActive = granted && ShowQrScanStep;
        QrStatusText = granted
            ? "Halte den QR-Code vollständig in den markierten Bereich."
            : "Für den QR-Scan wird der Kamerazugriff benötigt.";
    }

    [RelayCommand]
    private void RestartQrScan()
    {
        QrScanCompleted = false;
        QrRawContent = string.Empty;
        QrStatusText = "Kamera wird vorbereitet...";

        if (_steps.Count > 0 &&
            StepIndex > 0 &&
            _steps[StepIndex].Kind == LeadWizardStepKind.QrReview)
        {
            StepIndex--;
        }

        UpdateStep();
    }

    [RelayCommand]
    private async Task AnalyzeBusinessCardPhotoAsync()
    {
        await AnalyzeBusinessCardPhotoCoreAsync(advanceAfterSuccess: false);
    }

    private async Task<bool> AnalyzeBusinessCardPhotoCoreAsync(bool advanceAfterSuccess)
    {
        if (Lead is null)
        {
            return false;
        }

        if (IsAnalyzingBusinessCard)
        {
            return false;
        }

        try
        {
            IsBusy = true;
            IsAnalyzingBusinessCard = true;
            AiStatusText = "Foto wird lokal gelesen...";
            ImageStatusText = "Foto wird analysiert...";
            UpdateStep();

            var image = await _imageService.GetLatestBusinessCardImageAsync(Lead.LocalId);

            if (image is null || string.IsNullOrWhiteSpace(image.LocalFilePath))
            {
                AiStatusText = "Kein Visitenkartenfoto gefunden.";
                ImageStatusText = "Kein Visitenkartenfoto gefunden.";
                await MoveAfterFailedAutomaticBusinessCardAnalysisAsync(advanceAfterSuccess);
                return false;
            }

            ClearAiExecutionDiagnostics();
            var ocrWatch = System.Diagnostics.Stopwatch.StartNew();
            var rawText = await _visionService.ReadBusinessCardAsync(image.LocalFilePath);
            ocrWatch.Stop();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                AiStatusText = "Kein Text erkannt.";
                ImageStatusText = "Kein Text erkannt.";
                await MoveAfterFailedAutomaticBusinessCardAnalysisAsync(advanceAfterSuccess);
                return false;
            }

            OcrRawText = rawText;
            AiStatusText = "Text erkannt. Felder werden vorgeschlagen...";

            var result = await _localAiService.ReadBusinessCardAsync(rawText);
            result.OcrDurationMs = ocrWatch.ElapsedMilliseconds;
            ApplyAiDiagnostics(result);

            FirstName = result.FirstName ?? FirstName;
            LastName = result.LastName ?? LastName;
            Company = result.Company ?? Company;
            JobTitle = result.JobTitle ?? JobTitle;
            Email = result.Email ?? Email;
            Phone = result.Phone ?? Phone;
            Mobile = result.Mobile ?? Mobile;
            Website = result.Website ?? Website;
            Street = result.Street ?? Street;
            ZipCode = result.ZipCode ?? ZipCode;
            City = result.City ?? City;
            Country = result.Country ?? Country;

            AiStatusText = $"Analyse abgeschlossen ({result.Engine}).";
            ImageStatusText = "Foto analysiert. Kontaktdaten wurden übernommen.";

            await SaveAsync();

            if (advanceAfterSuccess)
            {
                await MoveAfterAutomaticBusinessCardAnalysisAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            HasAiError = true;
            AiErrorText = $"{ex.GetType().Name}: {ex.Message}";
            AiStatusText = "Analyse-Fehler: " + ex.Message;
            ImageStatusText = "Analyse-Fehler: " + ex.Message;

            if (advanceAfterSuccess)
            {
                await MoveAfterAutomaticBusinessCardAnalysisAsync();
            }

            return false;
        }
        finally
        {
            IsAnalyzingBusinessCard = false;
            IsBusy = false;
            UpdateStep();
        }
    }

    private async Task MoveAfterFailedAutomaticBusinessCardAnalysisAsync(bool shouldMove)
    {
        if (!shouldMove)
        {
            return;
        }

        await MoveAfterAutomaticBusinessCardAnalysisAsync();
    }

    private async Task MoveAfterAutomaticBusinessCardAnalysisAsync()
    {
        var targetKind = IsAdmin
            ? LeadWizardStepKind.AnalyzeImage
            : LeadWizardStepKind.Contact;

        var targetIndex = _steps.FindIndex(x => x.Kind == targetKind);
        if (targetIndex < 0)
        {
            return;
        }

        StepIndex = targetIndex;
        UpdateStep();
        await EnsureCurrentStepDataLoadedAsync();
    }

    [RelayCommand]
    private async Task RunLocalAiAsync()
    {
        if (Lead is null)
        {
            return;
        }

        try
        {
            AiStatusText = "Lokale KI analysiert Visitenkarte...";

            // Übergangslösung:
            // Bis echte OCR/LLM angebunden ist, nutzen wir hier testweise vorhandenen OCR-Text.
            // Später kommt hier: Bild -> OCR/Vision-Modell -> Rohtext -> LLM.
            var inputText = string.IsNullOrWhiteSpace(OcrRawText)
                ? BuildTemporaryTextFromCurrentFields()
                : OcrRawText;

            ClearAiExecutionDiagnostics();
            var result = await _localAiService.ReadBusinessCardAsync(inputText);
            ApplyAiDiagnostics(result);

            FirstName = result.FirstName ?? FirstName;
            LastName = result.LastName ?? LastName;
            Company = result.Company ?? Company;
            JobTitle = result.JobTitle ?? JobTitle;
            Email = result.Email ?? Email;
            Phone = result.Phone ?? Phone;
            Mobile = result.Mobile ?? Mobile;
            Website = result.Website ?? Website;
            Street = result.Street ?? Street;
            ZipCode = result.ZipCode ?? ZipCode;
            City = result.City ?? City;
            Country = result.Country ?? Country;

            AiStatusText = $"KI-Felder übernommen ({result.Engine}).";

            await SaveAsync();
        }
        catch (Exception ex)
        {
            HasAiError = true;
            AiErrorText = $"{ex.GetType().Name}: {ex.Message}";
            AiStatusText = "KI-Fehler: " + ex.Message;
        }
    }

    private async Task RefreshAiDiagnosticsAsync()
    {
        try
        {
            var snapshot = await _aiDiagnosticsService.GetSnapshotAsync();
            AiRuntimeStatus = snapshot.RuntimeStatus;
            AiModelStatus = snapshot.ModelStatus;
            AiModelPath = snapshot.ModelPath;
        }
        catch (Exception ex)
        {
            AiRuntimeStatus = "Runtime-Status konnte nicht ermittelt werden.";
            AiModelStatus = "Modellstatus konnte nicht ermittelt werden.";
            AiErrorText = $"Diagnosefehler: {ex.GetType().Name}: {ex.Message}";
            HasAiError = true;
        }
    }

    private void ClearAiExecutionDiagnostics()
    {
        AiEngineText = "Analyse läuft...";
        AiTimingText = "Zeitmessung läuft...";
        AiRawResponse = string.Empty;
        AiParsedJson = string.Empty;
        AiErrorText = string.Empty;
        HasAiRawResponse = false;
        HasAiParsedJson = false;
        HasAiError = false;
    }

    private void ApplyAiDiagnostics(BusinessCardAiResult result)
    {
        AiEngineText = result.UsedFallback
            ? $"{result.Engine} – Fallback aktiv"
            : result.Engine;

        AiRuntimeStatus = result.NativeRuntimeAvailable
            ? "llama.cpp Runtime verfügbar"
            : "llama.cpp Runtime nicht verfügbar";

        if (!string.IsNullOrWhiteSpace(result.ModelStatus))
        {
            AiModelStatus = result.ModelStatus;
        }

        if (!string.IsNullOrWhiteSpace(result.ModelPath))
        {
            AiModelPath = result.ModelPath;
        }

        AiRawResponse = result.GeneratedText;
        AiParsedJson = result.ParsedJson;
        AiErrorText = result.DiagnosticError ?? string.Empty;
        HasAiRawResponse = !string.IsNullOrWhiteSpace(AiRawResponse);
        HasAiParsedJson = !string.IsNullOrWhiteSpace(AiParsedJson);
        HasAiError = !string.IsNullOrWhiteSpace(AiErrorText);

        var timings = new List<string>();
        if (result.OcrDurationMs > 0) timings.Add($"OCR: {FormatDuration(result.OcrDurationMs)}");
        if (result.ModelPreparationDurationMs > 0) timings.Add($"Modell: {FormatDuration(result.ModelPreparationDurationMs)}");
        if (result.InferenceDurationMs > 0) timings.Add($"Inferenz: {FormatDuration(result.InferenceDurationMs)}");
        if (result.JsonParsingDurationMs > 0) timings.Add($"JSON: {FormatDuration(result.JsonParsingDurationMs)}");
        if (result.TotalDurationMs > 0) timings.Add($"KI gesamt: {FormatDuration(result.TotalDurationMs)}");
        if (result.PromptLength > 0) timings.Add($"Prompt: {result.PromptLength:N0} Zeichen");

        AiTimingText = timings.Count > 0
            ? string.Join(" · ", timings)
            : "Keine Zeitmessung vorhanden.";
    }

    private static string FormatDuration(long milliseconds)
    {
        return milliseconds < 1000
            ? $"{milliseconds} ms"
            : $"{milliseconds / 1000d:0.00} s";
    }

    private string BuildTemporaryTextFromCurrentFields()
    {
        return string.Join(Environment.NewLine, new[]
        {
        FirstName,
        LastName,
        Company,
        JobTitle,
        Email,
        Phone,
        Mobile,
        Website,
        Street,
        ZipCode,
        City,
        Country
    }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    [RelayCommand]
    private async Task TakeBusinessCardPhotoAsync()
    {
        if (Lead is null)
            return;

        try
        {
            ImageStatusText = "Kamera wird geöffnet...";
            ShowAutomaticBusinessCardAnalysisStep(
                aiStatusText: "Kamera wird geöffnet...",
                imageStatusText: "Kamera wird geöffnet...");

            var image = await _imageService.AddBusinessCardPhotoAsync(Lead.LocalId);

            if (image is null)
            {
                MoveToBusinessCardPhotoStep();
                ImageStatusText = "Fotoaufnahme abgebrochen.";
                return;
            }

            await MoveToCropImageStepAsync(image);
        }
        catch (Exception ex)
        {
            ImageStatusText = "Kamera-Fehler: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task PickBusinessCardPhotoAsync()
    {
        if (Lead is null)
            return;

        try
        {
            ImageStatusText = "Bildauswahl wird geöffnet...";
            ShowAutomaticBusinessCardAnalysisStep(
                aiStatusText: "Bildauswahl wird geöffnet...",
                imageStatusText: "Bildauswahl wird geöffnet...");

            var image = await _imageService.PickBusinessCardPhotoAsync(Lead.LocalId);

            if (image is null)
            {
                MoveToBusinessCardPhotoStep();
                ImageStatusText = "Bildauswahl abgebrochen.";
                return;
            }

            await MoveToCropImageStepAsync(image);
        }
        catch (Exception ex)
        {
            ImageStatusText = "Bildauswahl-Fehler: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task CropBusinessCardImageAsync()
    {
        if (Lead is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            CropStatusText = "Foto wird zugeschnitten...";

            var image = await _imageService.GetLatestBusinessCardImageAsync(Lead.LocalId);
            if (image is null)
            {
                CropStatusText = "Kein Visitenkartenfoto gefunden.";
                return;
            }

            await _imageService.CropAsync(image, CropLeft, CropTop, CropRight, CropBottom);
            CropStatusText = "Foto zugeschnitten.";
            await ContinueWithBusinessCardImageAsync();
        }
        catch (Exception ex)
        {
            CropStatusText = "Zuschneiden fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            UpdateStep();
        }
    }

    [RelayCommand]
    private async Task UseOriginalBusinessCardImageAsync()
    {
        CropStatusText = "Originalfoto wird verwendet.";
        await ContinueWithBusinessCardImageAsync();
    }

    private async Task MoveToCropImageStepAsync(LocalLeadImage image)
    {
        await LoadImagesAsync(updateStatusText: false);
        ResetCropSelection();

        CropImagePath = image.LocalFilePath;
        CropStatusText =
            "Ziehe die Regler so, dass nur die Visitenkarte und relevante Daten im hellen Rahmen liegen.";
        ImageStatusText = "Foto aufgenommen. Optional zuschneiden.";

        var cropIndex = _steps.FindIndex(x => x.Kind == LeadWizardStepKind.CropImage);
        if (cropIndex < 0)
        {
            await ContinueWithBusinessCardImageAsync();
            return;
        }

        StepIndex = cropIndex;
        UpdateStep();
    }

    private async Task ContinueWithBusinessCardImageAsync()
    {
        ImageStatusText = "Foto wird vorbereitet...";
        ShowAutomaticBusinessCardAnalysisStep();
        await LoadImagesAsync(updateStatusText: false);
        ImageStatusText = "Foto wird analysiert...";
        await AnalyzeBusinessCardPhotoCoreAsync(advanceAfterSuccess: true);
    }

    private void ResetCropSelection()
    {
        CropLeft = 0.05d;
        CropTop = 0.12d;
        CropRight = 0.95d;
        CropBottom = 0.88d;
        UpdateCropSelectionText();
    }

    private void UpdateCropSelectionText()
    {
        var width = Math.Max(0d, CropRight - CropLeft);
        var height = Math.Max(0d, CropBottom - CropTop);
        CropSelectionText = $"Ausschnitt: {width:P0} Breite, {height:P0} Höhe";
    }

    private void ShowAutomaticBusinessCardAnalysisStep(
        string aiStatusText = "Analyse wird vorbereitet...",
        string imageStatusText = "Foto wird analysiert...")
    {
        if (IsAdmin)
        {
            return;
        }

        var analysisIndex = _steps.FindIndex(x => x.Kind == LeadWizardStepKind.BusinessCardAnalysis);
        if (analysisIndex < 0)
        {
            return;
        }

        StepIndex = analysisIndex;
        AiStatusText = aiStatusText;
        ImageStatusText = imageStatusText;
        UpdateStep();
    }

    private void MoveToBusinessCardPhotoStep()
    {
        var photoIndex = _steps.FindIndex(x => x.Kind == LeadWizardStepKind.BusinessCardPhoto);
        if (photoIndex < 0)
        {
            return;
        }

        StepIndex = photoIndex;
        UpdateStep();
    }

    private async Task LoadImagesAsync(bool updateStatusText = true)
    {
        if (Lead is null)
        {
            HasBusinessCardImage = false;
            if (updateStatusText)
            {
                ImageStatusText = "Noch kein Foto aufgenommen.";
            }
            return;
        }

        var images = await _imageService.GetForLeadAsync(Lead.LocalId);
        var businessCardImages = images.Where(x => x.ImageType == "BusinessCard").ToList();

        HasBusinessCardImage = businessCardImages.Any();

        if (updateStatusText)
        {
            ImageStatusText = HasBusinessCardImage
                ? $"{businessCardImages.Count} Foto(s) lokal gespeichert."
                : "Noch kein Foto aufgenommen.";
        }
    }

    public async Task SaveAsync()
    {
        if (Lead is null)
        {
            return;
        }

        Lead.FirstName = FirstName;
        Lead.LastName = LastName;
        Lead.Company = Company;
        Lead.JobTitle = JobTitle;
        Lead.Email = Email;
        Lead.Phone = Phone;
        Lead.Mobile = Mobile;
        Lead.Website = Website;

        Lead.Street = Street;
        Lead.ZipCode = ZipCode;
        Lead.City = City;
        Lead.Country = Country;

        Lead.VisitDay = VisitDay;
        Lead.WantsNewsletter = WantsNewsletter;
        Lead.Notes = Notes;
        Lead.FollowUpNotes = FollowUpNotes;
        if (_lookupItemsLoaded)
        {
            Lead.SelectionsJson = BuildSelectionsJson();
        }

        var signature = BuildLeadSignature(Lead);

        if (_savedSignature == signature)
        {
            UpdateLeadTitle();
            return;
        }

        await _localLeadService.SaveAsync(Lead);
        _savedSignature = signature;
        UpdateLeadTitle();
    }

    private static string BuildLeadSignature(LocalLead lead)
    {
        return string.Join("~|~",
            lead.FirstName, lead.LastName, lead.Company, lead.JobTitle,
            lead.Email, lead.Phone, lead.Mobile, lead.Website,
            lead.Street, lead.ZipCode, lead.City, lead.Country,
            lead.VisitDay, lead.WantsNewsletter, lead.Notes, lead.FollowUpNotes,
            lead.SelectionsJson);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await SaveAsync();

        if (StepIndex < _steps.Count - 1)
        {
            StepIndex++;
            UpdateStep();
            await EnsureCurrentStepDataLoadedAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await SaveAsync();

        if (StepIndex > 0)
        {
            StepIndex--;
            UpdateStep();
            await EnsureCurrentStepDataLoadedAsync();
        }
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        if (IsFinishing)
        {
            return;
        }

        IsFinishing = true;

        await SaveAsync();

        if (Lead is not null && Lead.SyncStatus == "Draft")
        {
            Lead.SyncStatus = "PendingUpload";
            await _localLeadService.SaveAsync(Lead);
        }

        LeadSaveNotificationState.Set(LeadSaveNotificationKind.Syncing);
        await _navigationService.GoToHomeAsync();
        _ = SyncAfterFinishAsync();
    }

    private async Task SyncAfterFinishAsync()
    {
        try
        {
            var result = await _leadSyncService.SyncPendingAsync("Nach Lead-Speicherung");

            if (result.Success)
            {
                await SecureStorage.SetAsync("last_sync_utc", DateTime.UtcNow.ToString("O"));
                LeadSaveNotificationState.Publish(LeadSaveNotificationKind.Synced);
                return;
            }

            var notificationKind = result.Synced > 0 && result.Failed == 0 && result.ImageFailed > 0
                ? LeadSaveNotificationKind.PartiallySynced
                : LeadSaveNotificationKind.LocalOnly;

            LeadSaveNotificationState.Publish(notificationKind);
        }
        catch
        {
            LeadSaveNotificationState.Publish(LeadSaveNotificationKind.LocalOnly);
        }
    }

    [RelayCommand]
    private async Task ToggleSelectionAsync(SelectableLookupItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        await SaveAsync();
    }

    [RelayCommand]
    private async Task ChooseOptionAAsync(SelectableLookupItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.SelectedOption = item.SelectedOption == item.OptionA ? null : item.OptionA;
        item.IsSelected = !string.IsNullOrEmpty(item.SelectedOption);
        await SaveAsync();
    }

    [RelayCommand]
    private async Task ChooseOptionBAsync(SelectableLookupItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.SelectedOption = item.SelectedOption == item.OptionB ? null : item.OptionB;
        item.IsSelected = !string.IsNullOrEmpty(item.SelectedOption);
        await SaveAsync();
    }

    [RelayCommand]
    private async Task SaveTextAsync()
    {
        await SaveAsync();
    }

    [RelayCommand]
    private async Task TogglePrintAsync(SelectableLookupItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.Print = !item.Print;
        if (item.Print || item.Digital)
        {
            item.IsSelected = true;
        }

        await SaveAsync();
    }

    [RelayCommand]
    private async Task ToggleDigitalAsync(SelectableLookupItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.Digital = !item.Digital;
        if (item.Print || item.Digital)
        {
            item.IsSelected = true;
        }

        await SaveAsync();
    }

    private void LoadFromLead()
    {
        if (Lead is null)
        {
            return;
        }

        FirstName = Lead.FirstName;
        LastName = Lead.LastName;
        Company = Lead.Company;
        JobTitle = Lead.JobTitle;
        Email = Lead.Email;
        Phone = Lead.Phone;
        Mobile = Lead.Mobile;
        Website = Lead.Website;

        Street = Lead.Street;
        ZipCode = Lead.ZipCode;
        City = Lead.City;
        Country = string.IsNullOrWhiteSpace(Lead.Country) ? "Deutschland" : Lead.Country;

        VisitDay = Lead.VisitDay;
        WantsNewsletter = Lead.WantsNewsletter;
        Notes = Lead.Notes;
        FollowUpNotes = Lead.FollowUpNotes;
        TradeFairHeaderText = string.IsNullOrWhiteSpace(Lead.TradeFair)
            ? "MesseLeads"
            : $"MesseLeads · {Lead.TradeFair}";
    }

    private async Task LoadLookupItemsAsync()
    {
        _itemsByGroup.Clear();

        foreach (var group in _lookupGroups)
        {
            var items = new ObservableCollection<SelectableLookupItem>();
            await AddLookupGroupAsync(group.Key, items);
            _itemsByGroup[group.Key] = items;
        }

        UpdateCurrentLookupGroup();
        UpdateLookupDownloadPrompt();
    }

    private void UpdateCurrentLookupGroup()
    {
        var step = StepIndex >= 0 && StepIndex < _steps.Count
            ? _steps[StepIndex]
            : null;

        var groupKey = step?.Kind == LeadWizardStepKind.LookupGroup
            ? step.GroupKey
            : null;

        CurrentLookupItems = groupKey is not null && _itemsByGroup.TryGetValue(groupKey, out var items)
            ? items
            : [];

        CurrentGroupUsesPrintDigital = groupKey == MaterialGroupKey;
        ShowFollowUpNotes = groupKey == FollowUpGroupKey;
    }

    private async Task EnsureCurrentStepDataLoadedAsync()
    {
        if (_steps.Count == 0 || StepIndex < 0 || StepIndex >= _steps.Count)
        {
            return;
        }

        var kind = _steps[StepIndex].Kind;

        if (RequiresLookupItems(kind))
        {
            await EnsureLookupItemsLoadedAsync();
        }

        if (kind == LeadWizardStepKind.AnalyzeImage)
        {
            await EnsureAiDiagnosticsLoadedAsync();
        }
    }

    private async Task EnsureLookupItemsLoadedAsync()
    {
        if (_lookupItemsLoaded)
        {
            return;
        }

        await LoadLookupItemsAsync();
        LoadSelectionsFromLead();
        _lookupItemsLoaded = true;
        UpdateLookupDownloadPrompt();
    }

    [RelayCommand]
    private async Task DownloadLookupsAsync()
    {
        if (IsDownloadingLookups)
        {
            return;
        }

        try
        {
            IsDownloadingLookups = true;
            LookupDownloadText = "Stammdaten werden synchronisiert...";

            var count = await _lookupSyncService.DownloadAsync();
            _lookupGroups = await _lookupSyncService.GetGroupsAsync(Lead?.TradeFairKey);
            RebuildStepsPreservingPosition();
            _lookupItemsLoaded = false;
            await EnsureLookupItemsLoadedAsync();

            LookupDownloadText = count == 0
                ? "Es wurden keine Stammdaten vom Server geliefert."
                : $"{count} Stammdaten wurden heruntergeladen.";
        }
        catch (Exception ex)
        {
            LookupDownloadText = "Stammdaten konnten nicht geladen werden: " + ex.Message;
        }
        finally
        {
            IsDownloadingLookups = false;
            UpdateLookupDownloadPrompt();
        }
    }

    private async Task EnsureAiDiagnosticsLoadedAsync()
    {
        if (_aiDiagnosticsLoaded)
        {
            return;
        }

        await RefreshAiDiagnosticsAsync();
        _aiDiagnosticsLoaded = true;
    }

    private static bool RequiresLookupItems(LeadWizardStepKind kind)
    {
        return kind == LeadWizardStepKind.LookupGroup;
    }

    private async Task AddLookupGroupAsync(string group, ObservableCollection<SelectableLookupItem> target)
    {
        var items = await _lookupSyncService.GetByGroupAsync(group, Lead?.TradeFairKey);

        foreach (var item in items)
        {
            target.Add(new SelectableLookupItem
            {
                Group = item.Group,
                Key = item.Key,
                Label = item.Label,
                FieldType = item.FieldType,
                OptionA = item.OptionA,
                OptionB = item.OptionB,
                UsesPrintDigital = item.FieldType == 0 && item.Group == MaterialGroupKey
            });
        }
    }

    private void LoadSelectionsFromLead()
    {
        if (Lead is null || string.IsNullOrWhiteSpace(Lead.SelectionsJson))
        {
            return;
        }

        List<LocalSelection>? selections;

        try
        {
            selections = JsonSerializer.Deserialize<List<LocalSelection>>(Lead.SelectionsJson);
        }
        catch
        {
            selections = [];
        }

        if (selections is null)
        {
            return;
        }

        foreach (var items in _itemsByGroup.Values)
        {
            ApplySelections(items, selections);
        }
    }

    private static void ApplySelections(
        IEnumerable<SelectableLookupItem> items,
        IEnumerable<LocalSelection> selections)
    {
        foreach (var item in items)
        {
            var selection = selections.FirstOrDefault(x =>
                x.Group == item.Group &&
                x.Key == item.Key);

            if (selection is null)
            {
                continue;
            }

            switch (item.FieldType)
            {
                case 1: // Entweder-oder
                    item.SelectedOption = selection.Value;
                    item.IsSelected = !string.IsNullOrEmpty(selection.Value);
                    break;

                case 2: // Textfeld
                    item.TextValue = selection.Value;
                    item.IsSelected = !string.IsNullOrEmpty(selection.Value);
                    break;

                default: // Auswahl
                    item.IsSelected = true;
                    item.Print = selection.Print;
                    item.Digital = selection.Digital;
                    break;
            }
        }
    }

    private string BuildSelectionsJson()
    {
        var selections = new List<LocalSelection>();

        foreach (var items in _itemsByGroup.Values)
        {
            AddSelected(items, selections);
        }

        return JsonSerializer.Serialize(selections);
    }

    private static void AddSelected(
        IEnumerable<SelectableLookupItem> source,
        ICollection<LocalSelection> target)
    {
        foreach (var item in source)
        {
            switch (item.FieldType)
            {
                case 1: // Entweder-oder
                    if (!string.IsNullOrWhiteSpace(item.SelectedOption))
                    {
                        target.Add(new LocalSelection
                        {
                            Group = item.Group,
                            Key = item.Key,
                            Value = item.SelectedOption
                        });
                    }
                    break;

                case 2: // Textfeld
                    if (!string.IsNullOrWhiteSpace(item.TextValue))
                    {
                        target.Add(new LocalSelection
                        {
                            Group = item.Group,
                            Key = item.Key,
                            Value = item.TextValue.Trim()
                        });
                    }
                    break;

                default: // Auswahl
                    if (item.IsSelected || item.Print || item.Digital)
                    {
                        target.Add(new LocalSelection
                        {
                            Group = item.Group,
                            Key = item.Key,
                            Value = item.Label,
                            Print = item.Print,
                            Digital = item.Digital
                        });
                    }
                    break;
            }
        }
    }

    private void UpdateStep()
    {
        if (_steps.Count == 0)
        {
            BuildSteps();
        }

        if (_steps.Count == 0)
        {
            StepTitle = "Lead erfassen";
            ProgressText = "";
            return;
        }

        if (StepIndex < 0)
        {
            StepIndex = 0;
        }

        if (StepIndex > _steps.Count - 1)
        {
            StepIndex = _steps.Count - 1;
        }

        var currentStep = _steps[StepIndex];

        CurrentStepKind = currentStep.Kind;
        StepTitle = currentStep.Title;
        ProgressText = $"Schritt {StepIndex + 1} von {_steps.Count}";

        StepDescription = currentStep.Description;

        ShowBusinessCardStep = currentStep.Kind == LeadWizardStepKind.BusinessCardPhoto;
        ShowCropImageStep = currentStep.Kind == LeadWizardStepKind.CropImage;
        ShowBusinessCardAnalysisStep = currentStep.Kind == LeadWizardStepKind.BusinessCardAnalysis;
        ShowOcrStep = currentStep.Kind == LeadWizardStepKind.AnalyzeImage;
        ShowContactStep = currentStep.Kind == LeadWizardStepKind.Contact;

        ShowAddressStep = currentStep.Kind == LeadWizardStepKind.Address;
        UpdateCurrentLookupGroup();
        ShowSummaryStep = currentStep.Kind == LeadWizardStepKind.Summary;
        ShowQrScanStep = currentStep.Kind == LeadWizardStepKind.QrScan;
        ShowQrReviewStep = currentStep.Kind == LeadWizardStepKind.QrReview;
        IsQrScannerActive = ShowQrScanStep && !QrScanCompleted;
        ShowCameraPhotoAction = currentStep.Kind == LeadWizardStepKind.BusinessCardPhoto &&
            _startMode != LeadStartMode.Gallery;
        ShowGalleryPhotoAction = currentStep.Kind == LeadWizardStepKind.BusinessCardPhoto &&
            _startMode == LeadStartMode.Gallery;

        var isAutomaticAnalysisStep = currentStep.Kind == LeadWizardStepKind.BusinessCardAnalysis;
        var isCropStep = currentStep.Kind == LeadWizardStepKind.CropImage;
        CanGoPrevious = StepIndex > 0 && !IsAnalyzingBusinessCard && !isAutomaticAnalysisStep;
        CanGoNext = StepIndex < _steps.Count - 1 && !IsAnalyzingBusinessCard && !isAutomaticAnalysisStep && !isCropStep;
        CanFinish = StepIndex == _steps.Count - 1 && !IsAnalyzingBusinessCard;
        UpdateLookupDownloadPrompt();
    }

    private void MovePastCompletedBusinessCardFlowForExistingLead()
    {
        if (!_openedExistingLead ||
            _startMode is not (LeadStartMode.Photo or LeadStartMode.Gallery) ||
            !HasBusinessCardImage ||
            !HasAnyExtractedContactData())
        {
            return;
        }

        var contactIndex = _steps.FindIndex(x => x.Kind == LeadWizardStepKind.Contact);
        if (contactIndex >= 0)
        {
            StepIndex = contactIndex;
        }
    }

    private bool HasAnyExtractedContactData()
    {
        return !string.IsNullOrWhiteSpace(FirstName) ||
            !string.IsNullOrWhiteSpace(LastName) ||
            !string.IsNullOrWhiteSpace(Company) ||
            !string.IsNullOrWhiteSpace(JobTitle) ||
            !string.IsNullOrWhiteSpace(Email) ||
            !string.IsNullOrWhiteSpace(Phone) ||
            !string.IsNullOrWhiteSpace(Mobile) ||
            !string.IsNullOrWhiteSpace(Website) ||
            !string.IsNullOrWhiteSpace(Street) ||
            !string.IsNullOrWhiteSpace(ZipCode) ||
            !string.IsNullOrWhiteSpace(City);
    }

    private void UpdateLookupDownloadPrompt()
    {
        if (CurrentStepKind is not { } kind)
        {
            ShowLookupDownloadPrompt = false;
            return;
        }

        ShowLookupDownloadPrompt = kind == LeadWizardStepKind.LookupGroup &&
            CurrentLookupItems.Count == 0 &&
            _lookupItemsLoaded;

        if (ShowLookupDownloadPrompt &&
            !IsDownloadingLookups &&
            string.IsNullOrWhiteSpace(LookupDownloadText))
        {
            LookupDownloadText =
                "Es sind noch keine Stammdaten auf dem Gerät. Lade sie jetzt herunter, um diese Auswahl zu füllen.";
        }
    }

    private void UpdateLeadTitle()
    {
        if (!string.IsNullOrWhiteSpace(Company))
        {
            LeadTitle = Company;
            return;
        }

        var name = $"{FirstName} {LastName}".Trim();

        LeadTitle = string.IsNullOrWhiteSpace(name)
            ? "Unbenannter Lead"
            : name;
    }
}
