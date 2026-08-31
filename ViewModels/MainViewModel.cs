using System;
using System.Windows.Input;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Action _closeAction;

        public MatrixTabViewModel MatrixTab { get; }
        public DistillerTabViewModel DistillerTab { get; }
        public ViewpointsTabViewModel ViewpointsTab { get; }

        public ICommand CancelCommand { get; }

        public MainViewModel(
            Action closeAction,
            IModelDiscoveryService modelDiscovery = null,
            ISearchSetService searchSets = null,
            IClashExecutionService clashExecution = null,
            IClashDistillerService distiller = null,
            IDialogService dialogService = null,
            ILoggerService logger = null)
        {
            _closeAction = closeAction;

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

            // Secondary background validation check (anti-tamper defense)
            System.Threading.Tasks.Task.Run(() =>
            {
                if (!LicenseService.QuickValidate())
                {
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        DialogService.Instance.ShowWarning("License authorization expired or invalidated.", "Automated Clash Runner");
                        _closeAction?.Invoke();
                    }));
                }
            });
        }
    }
}
