using MahApps.Metro.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MahApps.Metro.Models
{ //(Representa PID, NOMBRE, ESTADO, COMMANDLINE)

    public enum EstadoProceso
    {
        Iniciado,
        Detenido
    }
    public class SubProcesoItem : ObservableObject
    {
        public string ProcessId { get; set; } = "";
        public string ParentId { get; set; } = "";
        public string TipoProceso { get; set; } = "";
        public DateTime HoraEvento { get; set; }
        public EstadoProceso Estado { get; set; }

        private bool _notificacionesHabilitadas = true;

        public bool NotificacionesHabilitadas
        {
            get => _notificacionesHabilitadas;
            set
            {
                if (_notificacionesHabilitadas != value)
                {
                    _notificacionesHabilitadas = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    public class ProcesoAgrupado : ObservableObject
    {
        private string _nombre = string.Empty;
        public string Nombre
        {
            get => _nombre;
            set { _nombre = value; OnPropertyChanged(); }
        }

        private EstadoProceso _estadoGeneral = EstadoProceso.Detenido;
        public EstadoProceso EstadoGeneral
        {
            get => _estadoGeneral;
            set { _estadoGeneral = value; OnPropertyChanged(); }
        }

        private int _cantidadActivos;
        public int CantidadActivos
        {
            get => _cantidadActivos;
            set { _cantidadActivos = value; OnPropertyChanged(); }
        }

        // NUEVO: Propiedad para activar/desactivar notificaciones del proceso padre
        private bool _notificacionesHabilitadas = true;
        public bool NotificacionesHabilitadas
        {
            get => _notificacionesHabilitadas;
            set { _notificacionesHabilitadas = value; OnPropertyChanged(); }
        }

        // Colección de los subprocesos/instancias de este padre específico
        public ObservableCollection<SubProcesoItem> SubProcesos { get; } = new();
    }
    public class ProcesoInfo
    {
        public string Nombre { get; set; } = string.Empty;
        public string ProcessId { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string TipoProceso { get; set; } = string.Empty;
        public DateTime HoraEvento { get; set; }
        public EstadoProceso Estado { get; set; }
    }
}
