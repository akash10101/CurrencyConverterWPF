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
        }
    }
}
