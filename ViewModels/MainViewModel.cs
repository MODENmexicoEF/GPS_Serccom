using MahApps.Metro.Models;
using MahApps.Metro.Services;
using Microsoft.EntityFrameworkCore;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MahApps.Metro.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        // ================= SERVICIOS =================
        private readonly WmiService _wmiService;
        private readonly EmailService _emailService;
        private readonly DatabaseService _databaseService;
        private FileSystemWatcher? _fileSystemWatcher;

        // ================= MONITOR DE PROCESOS =================
        public ObservableCollection<ProcesoAgrupado> ProcesosPadre { get; } = new();

        // ================= CORREOS =================
        public ObservableCollection<EmailRecipient> Destinatarios { get; } = new();
        public ICommand AgregarCorreoCommand { get; }
        public ICommand EliminarCorreoCommand { get; }
        public ICommand CambiarEstadoCorreoCommand { get; }

        private string _nuevoCorreoInput = "";
        public string NuevoCorreoInput
        {
            get => _nuevoCorreoInput;
            set { _nuevoCorreoInput = value; OnPropertyChanged(); }
        }

        // ================= VISOR DE FACTURAS =================
        private const int PreviewDpi = 200;
        private int _versionPreview;

        public ObservableCollection<FacturaItemModel> ListaFacturas { get; set; } = new();
        public ObservableCollection<ImpresoraConfigModel> Impresoras { get; } = new();

        public string[] ModosEscala { get; } =
        {
            "Ajustar a la hoja",
            "Tamaño real (100%)",
            "Mitad de hoja",
            "Ancho completo (ticket / rollo)"
        };

        private int _modoEscalaIndex;
        public int ModoEscalaIndex
        {
            get => _modoEscalaIndex;
            set { _modoEscalaIndex = value; OnPropertyChanged(); }
        }

        private FacturaItemModel? _facturaSeleccionada;
        public FacturaItemModel? FacturaSeleccionada
        {
            get => _facturaSeleccionada;
            set
            {
                _facturaSeleccionada = value;
                OnPropertyChanged();
                _paginaActual = 1;
                OnPropertyChanged(nameof(PaginaActual));
                _ = CargarVistaPreviaAsync();
            }
        }

        private BitmapSource? _vistaPrevia;
        public BitmapSource? VistaPrevia
        {
            get => _vistaPrevia;
            set
            {
                _vistaPrevia = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewAncho));
                OnPropertyChanged(nameof(PreviewAlto));
            }
        }

        private int _paginaActual = 1;
        public int PaginaActual
        {
            get => _paginaActual;
            set
            {
                int nuevo = Math.Clamp(value, 1, Math.Max(1, TotalPaginas));
                if (nuevo == _paginaActual) { OnPropertyChanged(); return; }
                _paginaActual = nuevo;
                OnPropertyChanged();
                _ = CargarVistaPreviaAsync();
            }
        }

        private int _totalPaginas;
        public int TotalPaginas
        {
            get => _totalPaginas;
            set { _totalPaginas = value; OnPropertyChanged(); }
        }

        private double _zoomPct = 100;
        public double ZoomPct
        {
            get => _zoomPct;
            set
            {
                _zoomPct = Math.Clamp(value, 25, 300);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewAncho));
                OnPropertyChanged(nameof(PreviewAlto));
            }
        }

        public double PreviewAncho => VistaPrevia == null ? 0 : VistaPrevia.PixelWidth * 96.0 / PreviewDpi * ZoomPct / 100.0;
        public double PreviewAlto => VistaPrevia == null ? 0 : VistaPrevia.PixelHeight * 96.0 / PreviewDpi * ZoomPct / 100.0;

        private bool _imprimiendo;
        public bool Imprimiendo
        {
            get => _imprimiendo;
            set { _imprimiendo = value; OnPropertyChanged(); OnPropertyChanged(nameof(PuedeImprimir)); }
        }
        public bool PuedeImprimir => !Imprimiendo;

        private string _estadoImpresion = "";
        public string EstadoImpresion
        {
            get => _estadoImpresion;
            set { _estadoImpresion = value; OnPropertyChanged(); }
        }

        public ICommand ImprimirFacturaCommand { get; }
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }
        public ICommand ZoomMasCommand { get; }
        public ICommand ZoomMenosCommand { get; }
        public ICommand ZoomRestablecerCommand { get; }

        // ================= CONSTRUCTOR =================
        public MainViewModel()
        {
            var settings = EmailSettings.Load();

            var dbOptions = new DbContextOptionsBuilder<EmailDbContext>()
                .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=GpsSerccomEmail;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            // 1) PRIMERO los servicios
            _databaseService = new DatabaseService(dbOptions);
            _emailService = new EmailService(settings, _databaseService);

            // 2) DESPUÉS los comandos
            AgregarCorreoCommand = new RelayCommand(async _ => await EjecutarSeguroAsync(AgregarCorreoAsync));
            EliminarCorreoCommand = new RelayCommand(async p => await EjecutarSeguroAsync(() => EliminarCorreoAsync(p)));
            CambiarEstadoCorreoCommand = new RelayCommand(async p => await EjecutarSeguroAsync(() => CambiarEstadoCorreoAsync(p)));

            ImprimirFacturaCommand = new RelayCommand(async _ => await ImprimirAsync());
            PaginaAnteriorCommand = new RelayCommand(_ => PaginaActual--);
            PaginaSiguienteCommand = new RelayCommand(_ => PaginaActual++);
            ZoomMasCommand = new RelayCommand(_ => ZoomPct += 25);
            ZoomMenosCommand = new RelayCommand(_ => ZoomPct -= 25);
            ZoomRestablecerCommand = new RelayCommand(_ => ZoomPct = 100);

            // 3) Carga inicial de destinatarios (ya con el servicio creado)
            _ = EjecutarSeguroAsync(CargarDestinatariosAsync);

            string[] procesosObjetivo = { "Spotify.exe", "Chrome.exe", "git-bash.exe" };

            foreach (var proc in procesosObjetivo)
            {
                ProcesosPadre.Add(new ProcesoAgrupado
                {
                    Nombre = proc,
                    EstadoGeneral = EstadoProceso.Detenido,
                    CantidadActivos = 0
                });
            }

            // Impresoras del sistema (la predeterminada viene marcada)
            string predeterminada = new PrinterSettings().PrinterName;
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                Impresoras.Add(new ImpresoraConfigModel
                {
                    Nombre = printer,
                    EstaSeleccionada = printer == predeterminada
                });
            }

            CargarConfiguracionYFacturas();
            IniciarMonitoreoCarpetaFacturas();

            _wmiService = new WmiService(procesosObjetivo);
            _wmiService.OnProcesoEvento += WmiService_OnProcesoEvento;
            _wmiService.Iniciar();
        }

        // ================= FACTURAS: CARGA Y MONITOREO =================
        private void CargarConfiguracionYFacturas()
        {
            try
            {
                string archivoConfig = "appsettings.json";
                if (File.Exists(archivoConfig))
                {
                    string jsonContent = File.ReadAllText(archivoConfig);
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);

                    if (config != null && !string.IsNullOrEmpty(config.RutaCarpetaFacturas) && Directory.Exists(config.RutaCarpetaFacturas))
                    {
                        var archivosPdf = Directory.GetFiles(config.RutaCarpetaFacturas, "*.pdf");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ListaFacturas.Clear();

                            foreach (var archivo in archivosPdf)
                            {
                                var info = new FileInfo(archivo);
                                ListaFacturas.Add(new FacturaItemModel
                                {
                                    NombreArchivo = info.Name,
                                    RutaCompleta = info.FullName,
                                    TamanoBytes = info.Length,
                                    FechaCreacion = info.CreationTime
                                });
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar facturas: {ex.Message}");
            }
        }

        private void IniciarMonitoreoCarpetaFacturas()
        {
            try
            {
                string archivoConfig = "appsettings.json";
                if (!File.Exists(archivoConfig)) return;

                string jsonContent = File.ReadAllText(archivoConfig);
                var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);

                if (config != null && !string.IsNullOrEmpty(config.RutaCarpetaFacturas) && Directory.Exists(config.RutaCarpetaFacturas))
                {
                    _fileSystemWatcher = new FileSystemWatcher(config.RutaCarpetaFacturas, "*.pdf")
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
                    };

                    _fileSystemWatcher.Created += (s, e) => CargarConfiguracionYFacturas();
                    _fileSystemWatcher.Deleted += (s, e) => CargarConfiguracionYFacturas();
                    _fileSystemWatcher.Renamed += (s, e) => CargarConfiguracionYFacturas();

                    _fileSystemWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error configurando el monitor de carpetas: {ex.Message}");
            }
        }

        // ================= FACTURAS: VISTA PREVIA =================
        private async Task CargarVistaPreviaAsync()
        {
            var factura = FacturaSeleccionada;
            int pagina = _paginaActual - 1;
            int version = ++_versionPreview;

            if (factura == null || !File.Exists(factura.RutaCompleta))
            {
                VistaPrevia = null;
                TotalPaginas = 0;
                return;
            }

            try
            {
                var (bmp, total) = await Task.Run(() => RenderizarPagina(factura.RutaCompleta, pagina));
                if (version != _versionPreview) return; // llegó una selección más reciente
                TotalPaginas = total;
                VistaPrevia = bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error renderizando PDF: {ex.Message}");
                VistaPrevia = null;
                TotalPaginas = 0;
            }
        }

        private static (BitmapSource, int) RenderizarPagina(string ruta, int pagina)
        {
            using var pdf = PdfDocument.Load(ruta);
            int total = pdf.PageCount;
            pagina = Math.Clamp(pagina, 0, total - 1);

            var size = pdf.PageSizes[pagina];
            int w = (int)(size.Width / 72f * PreviewDpi);
            int h = (int)(size.Height / 72f * PreviewDpi);

            using var img = pdf.Render(pagina, w, h, PreviewDpi, PreviewDpi, PdfRenderFlags.Annotations);
            using var ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return (bi, total);
        }

        // ================= FACTURAS: IMPRESIÓN =================
        private async Task ImprimirAsync()
        {
            if (FacturaSeleccionada == null)
            {
                MessageBox.Show("Seleccione una factura de la lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var seleccionadas = Impresoras.Where(i => i.EstaSeleccionada).ToList();
            if (seleccionadas.Count == 0)
            {
                MessageBox.Show("Marque al menos una impresora.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ruta = FacturaSeleccionada.RutaCompleta;
            var modo = (ModoEscala)ModoEscalaIndex;

            // Validar todo antes de enviar nada
            var trabajos = new List<(ImpresoraConfigModel Cfg, List<int> Paginas)>();
            foreach (var cfg in seleccionadas)
            {
                var paginas = PdfPrintService.ResolverPaginas(cfg, TotalPaginas, _paginaActual - 1, out var error);
                if (error != null)
                {
                    MessageBox.Show($"{cfg.Nombre}: {error}", "Páginas inválidas", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                trabajos.Add((cfg, paginas));
            }

            Imprimiendo = true;
            var errores = new List<string>();

            foreach (var (cfg, paginas) in trabajos)
            {
                EstadoImpresion = $"Enviando a {cfg.Nombre}...";
                try
                {
                    await Task.Run(() => PdfPrintService.Imprimir(ruta, cfg, modo, paginas));
                }
                catch (Exception ex)
                {
                    errores.Add($"{cfg.Nombre}: {ex.Message}");
                }
            }

            Imprimiendo = false;
            EstadoImpresion = errores.Count == 0
                ? $"Enviado a {trabajos.Count} impresora(s)."
                : "Terminó con errores.";

            if (errores.Count > 0)
                MessageBox.Show(string.Join("\n\n", errores), "Error de impresión", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ================= CORREOS / BASE DE DATOS =================
        private async Task CargarDestinatariosAsync()
        {
            var lista = await _databaseService.ObtenerTodosLosDestinatariosAsync();
            Destinatarios.Clear();
            foreach (var item in lista)
            {
                Destinatarios.Add(item);
            }
        }

        private async Task AgregarCorreoAsync()
        {
            if (string.IsNullOrWhiteSpace(NuevoCorreoInput)) return;

            await _databaseService.AgregarDestinatarioAsync(NuevoCorreoInput);
            NuevoCorreoInput = string.Empty;
            await CargarDestinatariosAsync();
        }

        private async Task EliminarCorreoAsync(object? parameter)
        {
            if (parameter is int id)
            {
                await _databaseService.EliminarDestinatarioAsync(id);
                await CargarDestinatariosAsync();
            }
            else if (parameter is EmailRecipient recipient)
            {
                await _databaseService.EliminarDestinatarioAsync(recipient.Id);
                await CargarDestinatariosAsync();
            }
        }

        private async Task CambiarEstadoCorreoAsync(object? parameter)
        {
            if (parameter is EmailRecipient recipient)
            {
                await _databaseService.CambiarEstadoDestinatarioAsync(recipient.Id, recipient.IsActive);
            }
        }

        private async Task EjecutarSeguroAsync(Func<Task> accion)
        {
            try
            {
                await accion();
            }
            catch (Exception ex)
            {
                var detalle = ex.InnerException?.Message ?? ex.Message;
                System.Diagnostics.Debug.WriteLine($"Error BD: {ex}");
                MessageBox.Show($"Error de base de datos:\n\n{detalle}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= EVENTOS WMI =================
        private void WmiService_OnProcesoEvento(ProcesoInfo info)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                var padre = ProcesosPadre.FirstOrDefault(p => p.Nombre.Equals(info.Nombre, StringComparison.OrdinalIgnoreCase));

                if (padre != null)
                {
                    var estadoAnterior = padre.EstadoGeneral;

                    if (info.Estado == EstadoProceso.Iniciado)
                    {
                        padre.EstadoGeneral = EstadoProceso.Iniciado;
                        padre.SubProcesos.Insert(0, new SubProcesoItem
                        {
                            ProcessId = info.ProcessId,
                            ParentId = info.ParentId,
                            TipoProceso = info.TipoProceso,
                            HoraEvento = info.HoraEvento,
                            Estado = EstadoProceso.Iniciado
                        });
                    }
                    else if (info.Estado == EstadoProceso.Detenido)
                    {
                        var subExistente = padre.SubProcesos.FirstOrDefault(s => s.ProcessId == info.ProcessId);
                        if (subExistente != null)
                        {
                            padre.SubProcesos.Remove(subExistente);
                        }

                        if (padre.SubProcesos.Count == 0)
                        {
                            padre.EstadoGeneral = EstadoProceso.Detenido;
                        }
                    }

                    padre.CantidadActivos = padre.SubProcesos.Count;
                    bool esElMomentoDeAlerta = (estadoAnterior == EstadoProceso.Iniciado && padre.EstadoGeneral == EstadoProceso.Detenido);

                    if (esElMomentoDeAlerta && padre.NotificacionesHabilitadas)
                    {
                        try
                        {
                            await _emailService.SendProcessAlertAsync(
                                padre.Nombre,
                                info.ProcessId,
                                padre.EstadoGeneral.ToString());

                            await _databaseService.RegistrarLogAsync(
                                padre.Nombre,
                                info.ProcessId,
                                padre.EstadoGeneral.ToString(),
                                "Alerta por detención enviada y registrada exitosamente.");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Fallo al enviar correo. No se registró en DB. Error: {ex.Message}");
                        }
                    }
                }
            });
        }

        // ================= LIMPIEZA =================
        public void LimpiarServicios()
        {
            _wmiService.Detener();
            _fileSystemWatcher?.Dispose();
        }
    }

    // Clase auxiliar sencilla de RelayCommand para WPF
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
    }
}