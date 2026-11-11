using System;
using System.Collections.Generic;
using System.Text;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class FacturaItemModel : INotifyPropertyChanged
{
    private string _nombreArchivo;
    public string NombreArchivo
    {
        get => _nombreArchivo;
        set { _nombreArchivo = value; OnPropertyChanged(); }
    }

    private string _rutaCompleta;
    public string RutaCompleta
    {
        get => _rutaCompleta;
        set { _rutaCompleta = value; OnPropertyChanged(); }
    }

    private long _tamanoBytes;
    public long TamanoBytes
    {
        get => _tamanoBytes;
        set { _tamanoBytes = value; OnPropertyChanged(); TamanoFormatted = $"{_tamanoBytes / 1024} KB"; }
    }

    private string _tamanoFormatted;
    public string TamanoFormatted
    {
        get => _tamanoFormatted;
        set { _tamanoFormatted = value; OnPropertyChanged(); }
    }

    private DateTime _fechaCreacion;
    public DateTime FechaCreacion
    {
        get => _fechaCreacion;
        set { _fechaCreacion = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class ImpresoraItemModel : INotifyPropertyChanged
{
    public string Nombre { get; set; }

    private bool _estaSeleccionada;
    public bool EstaSeleccionada
    {
        get => _estaSeleccionada;
        set { _estaSeleccionada = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}