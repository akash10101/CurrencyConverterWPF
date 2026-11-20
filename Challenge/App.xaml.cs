using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Challenge.Caching;
using Challenge.Controllers;
using Challenge.Controllers.Interfaces;
using Challenge.Logging;
using Challenge.Models;
using Challenge.Services;
using Challenge.Services.Interfaces;
using Challenge.ViewModels;

namespace Challenge
{
    public partial class App : Application
    {
        private const string MinimumLogLevelKey = "Logging.MinimumLevel";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logger = CreateLogger();
            new CrashLogger(logger).Attach(this);
            logger.Info("Application started, version " + typeof(App).Assembly.GetName().Version);

            // WPF bindings format numbers and dates as en-US unless told otherwise. Use the user's culture
            // so that what is displayed matches how the amount is parsed.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            IConversionController controller = CreateController(logger);
            IExchangeRateService service = new ExchangeRateService(controller);
            var viewModel = new MainViewModel(service, logger: logger);

            var window = new MainWindow(viewModel);
            window.Show();

            _ = viewModel.InitializeAsync();
        }

        /// <summary>
        /// Daily log files in %LOCALAPPDATA%\CurrencyConverter\Logs; the level is set in App.config.
        /// </summary>
        private static ILogger CreateLogger()
        {
            LogLevel minimumLevel;
            if (!Enum.TryParse(ConfigurationManager.AppSettings[MinimumLogLevelKey], true, out minimumLevel))
            {
                minimumLevel = LogLevel.Info;
            }

            return new FileLogger(FileLogger.DefaultDirectory, minimumLevel);
        }

        /// <summary>
        /// The HTTP controller, wrapped in a cache that is configured in App.config.
        /// </summary>
        private static IConversionController CreateController(ILogger logger)
        {
            var settings = CacheSettings.Load(ConfigurationManager.AppSettings, logger);
            logger.Info("Cache settings in use: " + settings);
            var cache = new QueryCache<HistoricRatesQuery, IReadOnlyList<DailyRate>>(settings.CreateEvictionStrategy());
            return new CachedConversionController(new ConversionController(logger), cache);
        }
    }
}
