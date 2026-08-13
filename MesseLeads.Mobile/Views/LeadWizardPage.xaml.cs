using System.ComponentModel;
using MesseLeads.Mobile.Controls.Wizard;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;
using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class LeadWizardPage : ContentPage
{
    private readonly LeadWizardViewModel _viewModel;
    private readonly Dictionary<LeadWizardStepKind, View> _stepViews = [];
    private bool _routeInitialized;

    public LeadWizardPage(LeadWizardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_routeInitialized)
        {
            if (LeadWizardRouteState.LocalLeadId.HasValue)
            {
                _viewModel.SetLeadId(LeadWizardRouteState.LocalLeadId.Value);
                LeadWizardRouteState.LocalLeadId = null;
            }

            _viewModel.SetStartMode(LeadWizardRouteState.StartMode);
            _routeInitialized = true;
        }

        await _viewModel.LoadAsync();
        UpdateStepContent();
        await _viewModel.StartInitialPhotoSourceIfNeededAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        if (!_viewModel.IsFinishing)
        {
            await _viewModel.SaveAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeadWizardViewModel.CurrentStepKind))
        {
            UpdateStepContent();
        }
    }

    private void UpdateStepContent()
    {
        if (_viewModel.CurrentStepKind is not { } kind)
        {
            StepContentHost.Content = null;
            return;
        }

        StepContentHost.Content = GetOrCreateStepView(kind);
    }

    private View GetOrCreateStepView(LeadWizardStepKind kind)
    {
        if (_stepViews.TryGetValue(kind, out var view))
        {
            return view;
        }

        view = kind switch
        {
            LeadWizardStepKind.BusinessCardPhoto => new PhotoStepView(),
            LeadWizardStepKind.CropImage => new CropImageStepView(),
            LeadWizardStepKind.BusinessCardAnalysis => new BusinessCardAnalysisStepView(),
            LeadWizardStepKind.AnalyzeImage => new AnalyzeStepView(),
            LeadWizardStepKind.QrScan => new QrScanStepView(),
            LeadWizardStepKind.QrReview => new QrReviewStepView(),
            LeadWizardStepKind.Contact => new ContactStepView(),
            LeadWizardStepKind.Address => new AddressStepView(),
            LeadWizardStepKind.LookupGroup => new LookupGroupStepView(),
            LeadWizardStepKind.Summary => new SummaryStepView(),
            _ => new ContentView()
        };

        view.BindingContext = _viewModel;
        _stepViews[kind] = view;
        return view;
    }
}
