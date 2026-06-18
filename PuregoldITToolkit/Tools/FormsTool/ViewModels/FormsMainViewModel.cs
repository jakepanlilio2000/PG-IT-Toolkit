using PuregoldITToolkit.Core.Base;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FormsTool.ViewModels
{
    public class FormsMainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;

        // Form ViewModels
        private readonly InfViewModel _infViewModel;
        private readonly ObViewModel _obViewModel;
        private readonly SsrfViewModel _ssrfViewModel;
        private readonly TsrfViewModel _tsrfViewModel;

        // UI State Toggles for RadioButtons
        private bool _isInfSelected;
        private bool _isObSelected;
        private bool _isSsrfSelected;
        private bool _isTsrfSelected;

        public ViewModelBase CurrentViewModel { get => _currentViewModel; set => SetProperty(ref _currentViewModel, value); }

        public bool IsInfSelected
        {
            get => _isInfSelected;
            set { if (SetProperty(ref _isInfSelected, value) && value) CurrentViewModel = _infViewModel; }
        }
        public bool IsObSelected
        {
            get => _isObSelected;
            set { if (SetProperty(ref _isObSelected, value) && value) CurrentViewModel = _obViewModel; }
        }
        public bool IsSsrfSelected
        {
            get => _isSsrfSelected;
            set { if (SetProperty(ref _isSsrfSelected, value) && value) CurrentViewModel = _ssrfViewModel; }
        }
        public bool IsTsrfSelected
        {
            get => _isTsrfSelected;
            set { if (SetProperty(ref _isTsrfSelected, value) && value) CurrentViewModel = _tsrfViewModel; }
        }

        public FormsMainViewModel(
            InfViewModel infViewModel,
            ObViewModel obViewModel,
            SsrfViewModel ssrfViewModel,
            TsrfViewModel tsrfViewModel)
        {
            _infViewModel = infViewModel;
            _obViewModel = obViewModel;
            _ssrfViewModel = ssrfViewModel;
            _tsrfViewModel = tsrfViewModel;

            // Set default view
            IsInfSelected = true;
        }
    }
}