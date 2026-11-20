using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Challenge.Caching;
using Challenge.Controllers;
using Challenge.Controllers.Interfaces;
using Challenge.Models;
using Challenge.Services;
using Challenge.Services.Interfaces;
using Challenge.ViewModels;

namespace Challenge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // WPF bindings format numbers and dates as en-US unless told otherwise. Use the user's culture
            // so that what is displayed matches how the amount is parsed.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            // Composition root: the only place that knows which implementations are used.
            IConversionController controller = CreateController();
            IExchangeRateService service = new ExchangeRateService(controller);
            var viewModel = new MainViewModel(service);

            var window = new MainWindow(viewModel);
            window.Show();

            // Start the first load once the window is up. InitializeAsync reports its own errors in the
            // status line, so nothing needs to be awaited here.
            _ = viewModel.InitializeAsync();
        }

        /// <summary>The HTTP controller, wrapped in a cache that is configured in App.config.</summary>
        private static IConversionController CreateController()
        {
            var settings = CacheSettings.Load(ConfigurationManager.AppSettings);
            var cache = new QueryCache<HistoricRatesQuery, IReadOnlyList<DailyRate>>(settings.CreateEvictionStrategy());
            return new CachedConversionController(new ConversionController(), cache);
        }
    }
}
