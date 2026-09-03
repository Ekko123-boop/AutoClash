using System;
using System.Windows.Input;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Action _closeAction;

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public MatrixTabViewModel MatrixTab { get; }
        public DistillerTabViewModel DistillerTab { get; }
        public ViewpointsTabViewModel ViewpointsTab { get; }

        public ICommand CancelCommand { get; }

        public MainViewModel(
            Action closeAction,
            int initialTabIndex = 0,
            IModelDiscoveryService modelDiscovery = null,
            ISearchSetService searchSets = null,
            IClashExecutionService clashExecution = null,
            IClashDistillerService distiller = null,
            IDialogService dialogService = null,
            ILoggerService logger = null)
        {
            _closeAction = closeAction;
            _selectedTabIndex = initialTabIndex;

            var loggerSvc = logger ?? LoggerService.Instance;
            var dialogSvc = dialogService ?? DialogService.Instance;
            var discoverySvc = modelDiscovery ?? ModelDiscoveryService.Instance;
            var searchSetSvc = searchSets ?? SearchSetService.Instance;
            var executionSvc = clashExecution ?? ClashExecutionService.Instance;
            var distillerSvc = distiller ?? ClashDistillerService.Instance;

            MatrixTab = new MatrixTabViewModel(discoverySvc, searchSetSvc, executionSvc, dialogSvc, loggerSvc);
            DistillerTab = new DistillerTabViewModel(distillerSvc, dialogSvc, loggerSvc);
            ViewpointsTab = new ViewpointsTabViewModel(distillerSvc, dialogSvc, loggerSvc);

            CancelCommand = new RelayCommand(_ => _closeAction?.Invoke());

            // Background remote deactivation monitoring
            var uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            System.Threading.Tasks.Task.Run(() =>
            {
                var result = LicenseService.Validate();
                if (!result.IsAllowed && result.IsRevoked)
                {
                    uiDispatcher.BeginInvoke(new Action(() =>
                    {
                        string msg = !string.IsNullOrWhiteSpace(result.Message)
                            ? result.Message
                            : "Cypher Tools is temporarily unavailable. Please contact administrator.";
                        DialogService.Instance.ShowWarning(msg, "Cypher Tools");
                        _closeAction?.Invoke();
                    }));
                }
            });
        }
    }
}
