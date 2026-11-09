using MahApps.Metro.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace MahApps.Metro
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(
            object? sender,
            CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.LimpiarServicios();
            }
        }
    }
}