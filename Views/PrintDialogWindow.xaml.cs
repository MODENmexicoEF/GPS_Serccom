using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MahApps.Metro.Views
{
    /// <summary>
    /// Interaction logic for PrintDialogWindow.xaml
    /// </summary>
    public partial class PrintDialogWindow
    {
        public int NumeroCopias { get; set; } = 1;
        public bool SeImprime { get; private set; } = false;
        public string ImpresoraDestino { get; set; }

        public PrintDialogWindow(string impresora)
        {
            InitializeComponent();
            ImpresoraDestino = impresora;
            DataContext = this;
        }

        private void Imprimir_Click(object sender, RoutedEventArgs e)
        {
            SeImprime = true;
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            SeImprime = false;
            Close();
        }
    }
}
