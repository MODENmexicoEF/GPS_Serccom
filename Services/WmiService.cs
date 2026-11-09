using MahApps.Metro.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;

namespace MahApps.Metro.Services
{
    public class WmiService
    {
        private readonly string[] _procesosObjetivos;
        private ManagementEventWatcher? _watcherInicio;
        private ManagementEventWatcher? _watcherCierre;

        public event Action<ProcesoInfo>? OnProcesoEvento;
        public WmiService(string[] procesosObjetivos)
        {
            _procesosObjetivos = procesosObjetivos;
        }

        public void Iniciar()
        {
            string condicion = string.Join(" OR ", _procesosObjetivos.Select(p => $"TargetInstance.Name = '{p}'"));

            var queryInicio = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), $"TargetInstance ISA 'Win32_Process' AND ({condicion})");
            var queryCierre = new WqlEventQuery("__InstanceDeletionEvent", new TimeSpan(0, 0, 1), $"TargetInstance ISA 'Win32_Process' AND ({condicion})");

            _watcherInicio = new ManagementEventWatcher(queryInicio);
            _watcherCierre = new ManagementEventWatcher(queryCierre);

            _watcherInicio.EventArrived += (s, e) => ProcesarEvento(e, EstadoProceso.Iniciado);
            _watcherCierre.EventArrived += (s, e) => ProcesarEvento(e, EstadoProceso.Detenido);


            _watcherInicio.Start();
            _watcherCierre.Start();
        }
        private void ProcesarEvento(EventArrivedEventArgs e, EstadoProceso estado)
        {
            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            var info = new ProcesoInfo
            {
                Nombre = targetInstance["Name"]?.ToString() ?? "Desconocido",
                ProcessId = targetInstance["ProcessId"]?.ToString() ?? "0",
                ParentId = targetInstance["ParentProcessId"]?.ToString() ?? "0",
                HoraEvento = DateTime.Now,
                Estado = estado
            };

            string commandLine = targetInstance["CommandLine"]?.ToString() ?? "";
            info.TipoProceso = DeterminarTipoProceso(info.Nombre, commandLine, info.ProcessId, info.ParentId);

            OnProcesoEvento?.Invoke(info);
        }
        public void Detener()
        {
            _watcherInicio?.Stop();
            _watcherCierre?.Stop();
            _watcherInicio?.Dispose();
            _watcherCierre?.Dispose();
        }

        private static string DeterminarTipoProceso(string processName, string commandLine, string processId, string parentPid)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return "Desconocido";

            string nameLower = processName.ToLowerInvariant();
            string cmdLower = commandLine?.ToLowerInvariant() ?? string.Empty;

            // 1. Procesos de Sistema de Windows y Servicios
            if (nameLower == "svchost.exe") return "Servicio de Windows (Host)";
            if (nameLower == "csrss.exe" || nameLower == "smss.exe" || nameLower == "services.exe" || nameLower == "lsass.exe")
                return "Proceso Núcleo del Sistema";

            // 2. Clasificación detallada para aplicaciones Chromium / Electron / Edge / Chrome
            if (!string.IsNullOrWhiteSpace(cmdLower))
            {
                if (cmdLower.Contains("--type=renderer")) return "Subproceso UI / Renderizado";
                if (cmdLower.Contains("--type=gpu-process")) return "Subproceso Aceleración GPU";
                if (cmdLower.Contains("--type=utility")) return "Subproceso Utilidad (Red/Audio)";
                if (cmdLower.Contains("--type=crashpad-handler")) return "Subproceso Gestor de Errores";
                if (cmdLower.Contains("--type=extension")) return "Subproceso Extensión/Plugin";
                if (cmdLower.Contains("--type=zipped-story")) return "Subproceso Contenido";
                if (cmdLower.Contains("--type=broker")) return "Subproceso Permisos / Broker";
            }

            // 3. Inspección del proceso activo usando System.Diagnostics.Process
            try
            {
                if (int.TryParse(processId, out int pid))
                {
                    using var proc = Process.GetProcessById(pid);

                    // Si el proceso tiene una ventana activa y visible, es una App de Usuario
                    if (proc.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                    {
                        return "Aplicación de Usuario (Ventana Principal)";
                    }

                    // Si se ejecuta bajo 'svchost' u otro host como padre
                    if (int.TryParse(parentPid, out int ppid))
                    {
                        try
                        {
                            using var parentProc = Process.GetProcessById(ppid);
                            // Si el proceso padre tiene el mismo nombre, es un subproceso worker
                            if (parentProc.ProcessName.Equals(Path.GetFileNameWithoutExtension(processName), StringComparison.OrdinalIgnoreCase))
                            {
                                return "Subproceso Hijo Worker";
                            }
                        }
                        catch { /* El proceso padre ya pudo haber terminado */ }
                    }
                }
            }
            catch
            {
                // El proceso pudo haber terminado rápido antes de inspeccionar la ventana
            }

            // 4. Si tiene línea de comandos explícita pero sin bandera --type=
            if (!string.IsNullOrWhiteSpace(commandLine))
            {
                return "Proceso Principal con Parámetros";
            }

            // 5. Valor por defecto si no es subproceso ni servicio
            return "Proceso Background / Servicio";
        }
    }
}
