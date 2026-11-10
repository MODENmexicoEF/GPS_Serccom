using System;

namespace MahApps.Metro.Models
{
    public class ProcesoLog
    {
        public int Id { get; set; }
        public string NombreProceso { get; set; } = "";
        public string ProcessId { get; set; } = "";
        public string Estado { get; set; } = ""; // "Iniciado" o "Detenido"
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string Detalle { get; set; } = "";
    }
}