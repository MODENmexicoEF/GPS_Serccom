using MahApps.Metro.Models;
using MahApps.Metro.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MahApps.Metro.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly WmiService _wmiService;
        private readonly EmailService _emailService;
        private readonly DatabaseService _databaseService;

        public ObservableCollection<ProcesoAgrupado> ProcesosPadre { get; } = new();

        // Colección para la UI de Configuración (Destinatarios)
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

        //public ICommand AgregarCorreoCommand { get; }

        public MainViewModel()
        {
            var settings = EmailSettings.Load();

            var dbOptions = new DbContextOptionsBuilder<EmailDbContext>()
                .UseSqlServer(
                    @"Server=(localdb)\MSSQLLocalDB;
                      Database=GpsSerccomEmail;
                      Trusted_Connection=True;
                      TrustServerCertificate=True;")
                .Options;
            AgregarCorreoCommand = new RelayCommand(async _ => await AgregarCorreoAsync());
            EliminarCorreoCommand = new RelayCommand(async param => await EliminarCorreoAsync(param));
            CambiarEstadoCorreoCommand = new RelayCommand(async param => await CambiarEstadoCorreoAsync(param));
            // Inicializamos el servicio de base de datos
            _databaseService = new DatabaseService(dbOptions);
            _emailService = new EmailService(settings, _databaseService);

            // Cargamos los destinatarios iniciales en segundo plano de manera segura
            _ = CargarDestinatariosAsync();

            AgregarCorreoCommand = new RelayCommand(async _ => await AgregarCorreoAsync());

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

            _wmiService = new WmiService(procesosObjetivo);
            _wmiService.OnProcesoEvento += WmiService_OnProcesoEvento;
            _wmiService.Iniciar();
        }

        private async Task CargarDestinatariosAsync()
        {
            var lista = await _databaseService.ObtenerDestinatariosActivosAsync();
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

        private void WmiService_OnProcesoEvento(ProcesoInfo info)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                var padre = ProcesosPadre.FirstOrDefault(p => p.Nombre.Equals(info.Nombre, System.StringComparison.OrdinalIgnoreCase));

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
                            // Intentamos enviar el correo primero
                            await _emailService.SendProcessAlertAsync(
                                padre.Nombre,
                                info.ProcessId,
                                padre.EstadoGeneral.ToString());

                            // SI Y SOLO SI EL CORREO SE ENVIÓ CON ÉXITO, REGISTRAMOS EN LA DB
                            await _databaseService.RegistrarLogAsync(
                                padre.Nombre,
                                info.ProcessId,
                                padre.EstadoGeneral.ToString(),
                                "Alerta por detención enviada y registrada exitosamente.");
                        }
                        catch (System.Exception ex)
                        {
                            // Si el correo falla (ej. sin internet, credenciales incorrectas, etc.), 
                            // NO se guarda en la DB para mantener la congruencia de que el mensaje nunca llegó.
                            System.Diagnostics.Debug.WriteLine($"Fallo al enviar correo. No se registró en DB. Error: {ex.Message}");
                        }
                    }
                }
            });
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

        public void LimpiarServicios()
        {
            _wmiService.Detener();
        }
    }

    // Clase auxiliar sencilla de RelayCommand para WPF si no la tienes implementada
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