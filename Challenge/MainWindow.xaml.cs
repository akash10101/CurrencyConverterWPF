using System;
using System.Windows;
using Challenge.ViewModels;

namespace Challenge
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            // All behaviour lives in the view model; the window only binds to it.
            DataContext = viewModel;

            // Pure view concerns from here on.
            // The window sizes its height to its content, so showing the history makes it grow downwards.
            // Keep it on screen when that happens.
            SizeChanged += (sender, args) => KeepInsideWorkArea();

            // WPF switches SizeToContent off for good once the user resizes, snaps or maximizes and restores
            // the window. Switch it back on whenever the history is shown or hidden, so the window always fits.
            HistoryCard.IsVisibleChanged += (sender, args) => FitHeightToContent();
        }

        private void FitHeightToContent()
        {
            if (WindowState == WindowState.Normal)
            {
                SizeToContent = SizeToContent.Height;
            }
        }

        private void KeepInsideWorkArea()
        {
            var workArea = SystemParameters.WorkArea;
            if (Top + ActualHeight > workArea.Bottom)
            {
                Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight);
            }
        }
    }
}
