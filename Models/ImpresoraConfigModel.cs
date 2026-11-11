using System.Collections.ObjectModel;
using System.Drawing.Printing;
using MahApps.Metro.ViewModels;

namespace MahApps.Metro.Models;

public enum ModoEscala { Ajustar, TamanoReal, MitadHoja, AnchoCompleto }

public class ImpresoraConfigModel : ObservableObject
{
    public string Nombre { get; init; } = "";

    private bool _estaSeleccionada;
    public bool EstaSeleccionada
    {
        get => _estaSeleccionada;
        set { _estaSeleccionada = value; OnPropertyChanged(); if (value) CargarCapacidades(); }
    }

    private int _copias = 1;
    public int Copias
    {
        get => _copias;
        set { _copias = Math.Clamp(value, 1, 999); OnPropertyChanged(); }
    }

    private bool _intercalar = true;
    public bool Intercalar { get => _intercalar; set { _intercalar = value; OnPropertyChanged(); } }

    // 0 = Todas, 1 = Página actual, 2 = Rango
    private int _modoIndex;
    public int ModoIndex
    {
        get => _modoIndex;
        set { _modoIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarRango)); }
    }
    public bool MostrarRango => ModoIndex == 2;

    private string _rangoTexto = "";
    public string RangoTexto { get => _rangoTexto; set { _rangoTexto = value; OnPropertyChanged(); } }

    private bool _color = true;
    public bool Color { get => _color; set { _color = value; OnPropertyChanged(); } }

    // 0 = Una cara, 1 = Borde largo, 2 = Borde corto
    private int _duplexIndex;
    public int DuplexIndex { get => _duplexIndex; set { _duplexIndex = value; OnPropertyChanged(); } }

    public ObservableCollection<string> TamanosPapel { get; } = new();

    private string? _papelSeleccionado;
    public string? PapelSeleccionado { get => _papelSeleccionado; set { _papelSeleccionado = value; OnPropertyChanged(); } }

    private bool _puedeDuplex = true;
    public bool PuedeDuplex { get => _puedeDuplex; private set { _puedeDuplex = value; OnPropertyChanged(); } }

    private bool _soportaColor = true;
    public bool SoportaColor { get => _soportaColor; private set { _soportaColor = value; OnPropertyChanged(); } }

    private bool _capacidadesCargadas;

    // Se consulta a la impresora solo cuando el usuario la marca (consultar todas al inicio puede ser lento)
    private void CargarCapacidades()
    {
        if (_capacidadesCargadas) return;
        _capacidadesCargadas = true;
        try
        {
            var ps = new PrinterSettings { PrinterName = Nombre };
            if (!ps.IsValid) return;

            PuedeDuplex = ps.CanDuplex;
            SoportaColor = ps.SupportsColor;
            if (!SoportaColor) Color = false;

            foreach (PaperSize p in ps.PaperSizes) TamanosPapel.Add(p.PaperName);
            PapelSeleccionado = ps.DefaultPageSettings.PaperSize.PaperName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudieron leer capacidades de {Nombre}: {ex.Message}");
        }
    }
}