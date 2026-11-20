using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Challenge.Commands;
using Challenge.Logging;
using Challenge.Models;
using Challenge.Services;
using Challenge.Services.Interfaces;
using Challenge.Utilities;
using Challenge.Validators;

namespace Challenge.ViewModels
{
    public sealed class MainViewModel : ViewModelBase, IDataErrorInfo
    {
        private const int DefaultHistoryDays = Constants.DefaultHistoryDays;
        private const string ShowHistoryText = "Show Historical data";
        private const string HideHistoryText = "Hide Historical data";

        private static readonly HistoricRateItem[] NoRates = new HistoricRateItem[0];

        private readonly IExchangeRateService _service;
        private readonly ExclusiveOperationRunner _runner;
        private readonly Func<DateTime> _today;
        private readonly ILogger _logger;

        private RateSnapshot _rates;
        private string _serviceError;
        private string _busyMessage = string.Empty;

        private string _amountText = "1";
        private IReadOnlyList<CurrencyInfo> _currencies = new CurrencyInfo[0];
        private CurrencyInfo _sourceCurrency;
        private CurrencyInfo _targetCurrency;
        private DateTime? _startDate;
        private DateTime? _endDate;

        private string _latestAmountText = string.Empty;
        private string _latestRateText = string.Empty;
        private string _latestDateText = string.Empty;
        private IReadOnlyList<HistoricRateItem> _historicRates = NoRates;
        private string _statusMessage = string.Empty;
        private bool _isStatusError;
        private bool _isBusy;
        private bool _isHistoryVisible;

        public MainViewModel(IExchangeRateService service, Func<DateTime> today = null, ILogger logger = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _today = today ?? (() => DateTime.Today);
            _logger = (logger ?? NullLogger.Instance).ForCategory(nameof(MainViewModel));
            _runner = new ExclusiveOperationRunner(_logger);

            _endDate = _today().Date;
            _startDate = _endDate.Value.AddDays(-DefaultHistoryDays);

            RefreshCommand = new RelayCommand(Refresh);
            ToggleHistoryCommand = new RelayCommand(ToggleHistory);
            SwapCurrenciesCommand = new RelayCommand(SwapCurrencies);
        }

        // ----- input -----

        public string AmountText
        {
            get => _amountText;
            set
            {
                if (SetProperty(ref _amountText, value))
                {
                    UpdateDisplay();
                }
            }
        }

        public IReadOnlyList<CurrencyInfo> Currencies
        {
            get => _currencies;
            private set => SetProperty(ref _currencies, value);
        }

        public CurrencyInfo SelectedSourceCurrency
        {
            get => _sourceCurrency;
            set
            {
                if (SetProperty(ref _sourceCurrency, value))
                {
                    _ = RefreshAsync();
                }
            }
        }

        public CurrencyInfo SelectedTargetCurrency
        {
            get => _targetCurrency;
            set
            {
                if (SetProperty(ref _targetCurrency, value))
                {
                    _ = RefreshAsync();
                }
            }
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    OnPropertyChanged(nameof(EndDate)); 
                    _ = RefreshAsync();
                }
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    OnPropertyChanged(nameof(StartDate));
                    _ = RefreshAsync();
                }
            }
        }

        // ----- output -----

        public string LatestAmountText
        {
            get => _latestAmountText;
            private set => SetProperty(ref _latestAmountText, value);
        }

        public string LatestRateText
        {
            get => _latestRateText;
            private set => SetProperty(ref _latestRateText, value);
        }

        public string LatestDateText
        {
            get => _latestDateText;
            private set => SetProperty(ref _latestDateText, value);
        }

        public IReadOnlyList<HistoricRateItem> HistoricRates
        {
            get => _historicRates;
            private set => SetProperty(ref _historicRates, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool IsStatusError
        {
            get => _isStatusError;
            private set => SetProperty(ref _isStatusError, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public ICommand RefreshCommand { get; }

        // ----- historical data toggle -----

        public bool IsHistoryVisible
        {
            get => _isHistoryVisible;
            private set
            {
                if (SetProperty(ref _isHistoryVisible, value))
                {
                    OnPropertyChanged(nameof(HistoryButtonText));
                }
            }
        }

        public string HistoryButtonText => _isHistoryVisible ? HideHistoryText : ShowHistoryText;

        public ICommand ToggleHistoryCommand { get; }

        public ICommand SwapCurrenciesCommand { get; }


        private void Refresh()
        {
            _logger.Info("Button clicked: Refresh (" + DescribeSelection() + ")");
            _ = _currencies.Count == 0 ? InitializeAsync() : RefreshAsync();
        }

        private void ToggleHistory()
        {
            _logger.Info("Button clicked: " + HistoryButtonText);
            IsHistoryVisible = !IsHistoryVisible;
        }

        private void SwapCurrencies()
        {
            _logger.Info("Button clicked: Swap currencies (" + CodeOf(_sourceCurrency) + " -> " + CodeOf(_targetCurrency)
                + " becomes " + CodeOf(_targetCurrency) + " -> " + CodeOf(_sourceCurrency) + ")");

            var source = _sourceCurrency;
            _sourceCurrency = _targetCurrency;
            _targetCurrency = source;
            OnPropertyChanged(nameof(SelectedSourceCurrency));
            OnPropertyChanged(nameof(SelectedTargetCurrency));

            _ = RefreshAsync();
        }

        private string DescribeSelection()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "amount '{0}', {1} -> {2}, {3:yyyy-MM-dd} to {4:yyyy-MM-dd}",
                _amountText, CodeOf(_sourceCurrency), CodeOf(_targetCurrency), _startDate, _endDate);
        }

        private static string CodeOf(CurrencyInfo currency) => currency?.Code ?? "none";

        // ----- operations -----
      
        public async Task InitializeAsync()
        {
            await RunAsync("Loading currencies...", _service.GetCurrenciesAsync, currencies =>
            {
                Currencies = currencies.All;

                _sourceCurrency = currencies.DefaultSource;
                _targetCurrency = currencies.DefaultTarget;
                OnPropertyChanged(nameof(SelectedSourceCurrency));
                OnPropertyChanged(nameof(SelectedTargetCurrency));
            });

            await RefreshAsync();
        }

        public Task RefreshAsync()
        {
            _rates = null;

            string source;
            string target;
            DateTime start;
            DateTime end;
            if (!TryGetRateQuery(out source, out target, out start, out end))
            {
                _runner.Cancel();
                IsBusy = false;
                UpdateDisplay();
                return Task.CompletedTask;
            }

            return RunAsync(
                Constants.ExchangeRateLoading,
                cancellationToken => _service.GetRatesAsync(source, target, start, end, cancellationToken),
                rates => _rates = rates);
        }

        private async Task RunAsync<T>(string busyMessage, Func<CancellationToken, Task<T>> operation, Action<T> onSuccess)
        {
            _serviceError = null;
            _busyMessage = busyMessage;
            IsBusy = true;
            UpdateDisplay();

            var outcome = await _runner.RunAsync(operation);
            if (outcome.IsSuperseded)
            {
                return;
            }

            if (outcome.Error == null)
            {
                onSuccess(outcome.Value);
            }
            else
            {
                _serviceError = outcome.Error;
            }

            IsBusy = false;
            UpdateDisplay();
        }

        // ----- display -----

        /// <summary>
        /// Recomputes everything the user sees from the current inputs, the loaded rates and the last error.
        /// </summary>
        private void UpdateDisplay()
        {
            var validationError = FirstValidationError();
            if (validationError != null)
            {
                ShowStatus(validationError, true);
                ClearResults();
                return;
            }

            if (_serviceError != null)
            {
                ShowStatus(_serviceError, true);
                ClearResults();
                return;
            }

            if (_rates == null)
            {
                ShowStatus(IsBusy ? _busyMessage : string.Empty, false);
                ClearResults();
                return;
            }

            decimal amount;
            InputValidator.TryParseAmount(_amountText, out amount, out _);

            var result = RateCalculator.Convert(_rates, amount);

            LatestAmountText = FormatAmount(result.LatestAmount);
            LatestRateText = string.Format(
                CultureInfo.CurrentCulture,
                "1 {0} = {1} {2}",
                _sourceCurrency.Code,
                result.LatestRate.ToString("#,##0.00####", CultureInfo.CurrentCulture),
                result.TargetCode);
            LatestDateText = string.Format(CultureInfo.CurrentCulture, "rate of {0:d}", result.LatestDate);

            HistoricRates = result.History;

            ShowStatus(
                HistoricRates.Count == 0 ? Constants.NoExchangeRateFoundMsg : string.Empty,
                false);
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusMessage = message;
            IsStatusError = isError;
        }

        private void ClearResults()
        {
            LatestAmountText = string.Empty;
            LatestRateText = string.Empty;
            LatestDateText = string.Empty;
            HistoricRates = NoRates;
        }

        private static string FormatAmount(decimal value)
        {
            return value.ToString("#,##0.00##", CultureInfo.CurrentCulture);
        }

        private static readonly string[] ValidatedProperties =
        {
            nameof(AmountText),
            nameof(SelectedSourceCurrency),
            nameof(SelectedTargetCurrency),
            nameof(StartDate),
            nameof(EndDate)
        };

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(AmountText): return InputValidator.ValidateAmount(_amountText);
                    case nameof(SelectedSourceCurrency): return InputValidator.ValidateSourceCurrency(_currencies, _sourceCurrency);
                    case nameof(SelectedTargetCurrency): return InputValidator.ValidateTargetCurrency(_currencies, _targetCurrency);
                    case nameof(StartDate): return InputValidator.ValidateStartDate(_startDate, _endDate, _today());
                    case nameof(EndDate): return InputValidator.ValidateEndDate(_startDate, _endDate, _today());
                    default: return null;
                }
            }
        }

        private string FirstValidationError()
        {
            return ValidatedProperties.Select(property => this[property]).FirstOrDefault(error => error != null);
        }

        private bool TryGetRateQuery(out string source, out string target, out DateTime start, out DateTime end)
        {
            source = _sourceCurrency?.Code;
            target = _targetCurrency?.Code;
            start = _startDate.GetValueOrDefault().Date;
            end = _endDate.GetValueOrDefault().Date;

            return source != null
                && target != null
                && this[nameof(SelectedTargetCurrency)] == null
                && this[nameof(StartDate)] == null
                && this[nameof(EndDate)] == null;
        }
    }
}
