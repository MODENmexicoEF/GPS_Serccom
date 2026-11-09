using MahApps.Metro.Models;
using MahApps.Metro.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace MahApps.Metro.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly WmiService _wmiService;

        // Colección única fija para los procesos principales configurados
        public ObservableCollection<ProcesoAgrupado> ProcesosPadre { get; } = new();

        public MainViewModel()
        {
            string[] procesosObjetivo = { "Spotify.exe", "Chrome.exe" };

            // Inicializamos la UI con los procesos base configurados (evita lista infinita)
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

        private void WmiService_OnProcesoEvento(ProcesoInfo info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Buscamos si el evento pertenece a uno de nuestros procesos padres vigilados
                var padre = ProcesosPadre.FirstOrDefault(p => p.Nombre.Equals(info.Nombre, System.StringComparison.OrdinalIgnoreCase));

                if (padre != null)
                {
                    if (info.Estado == EstadoProceso.Iniciado)
                    {
                        padre.EstadoGeneral = EstadoProceso.Iniciado;

                        // Añadimos la instancia a los subprocesos
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
                        // Buscamos y removemos o actualizamos el subproceso por PID
                        var subExistente = padre.SubProcesos.FirstOrDefault(s => s.ProcessId == info.ProcessId);
                        if (subExistente != null)
                        {
                            padre.SubProcesos.Remove(subExistente);
                        }

                        // Si ya no quedan subprocesos activos, el padre pasa a estar Detenido
                        if (padre.SubProcesos.Count == 0)
                        {
                            padre.EstadoGeneral = EstadoProceso.Detenido;
                        }
                    }

                    padre.CantidadActivos = padre.SubProcesos.Count;
                }
            });
        }

        public void LimpiarServicios()
        {
            _wmiService.Detener();
        }
    }
}