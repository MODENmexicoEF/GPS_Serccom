using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using MahApps.Metro.Models;
using PdfiumViewer;

namespace MahApps.Metro.Services;

public static class PdfPrintService
{
    /// Devuelve la lista de páginas (base 0) según la configuración de la impresora.
    public static List<int> ResolverPaginas(ImpresoraConfigModel cfg, int total, int paginaActual0, out string? error)
    {
        error = null;
        if (total <= 0) { error = "No hay páginas para imprimir."; return new(); }

        if (cfg.ModoIndex == 0) return Enumerable.Range(0, total).ToList();
        if (cfg.ModoIndex == 1) return new List<int> { Math.Clamp(paginaActual0, 0, total - 1) };

        var paginas = new List<int>();
        foreach (var token in cfg.RangoTexto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = token.Split('-', StringSplitOptions.TrimEntries);
            bool ok = partes.Length switch
            {
                1 => int.TryParse(partes[0], out _),
                2 => int.TryParse(partes[0], out _) && int.TryParse(partes[1], out _),
                _ => false
            };
            if (!ok) { error = $"Rango inválido: '{token}'. Use el formato 1,3,5-7."; return new(); }

            int desde = int.Parse(partes[0]);
            int hasta = partes.Length == 2 ? int.Parse(partes[1]) : desde;
            if (desde < 1 || hasta < desde || hasta > total)
            {
                error = $"El rango '{token}' está fuera de 1-{total}.";
                return new();
            }
            for (int p = desde; p <= hasta; p++) paginas.Add(p - 1);
        }

        if (paginas.Count == 0) error = "Escriba un rango de páginas (ej. 1,3,5-7).";
        return paginas;
    }

    /// Imprime (bloqueante). Llamar desde un hilo de fondo.
    public static void Imprimir(string rutaPdf, ImpresoraConfigModel cfg, ModoEscala modo, List<int> paginas)
    {
        using var pdf = PdfDocument.Load(rutaPdf);
        using var doc = new PrintDocument();

        var ps = doc.PrinterSettings;
        ps.PrinterName = cfg.Nombre;
        if (!ps.IsValid)
            throw new InvalidOperationException($"La impresora '{cfg.Nombre}' no está disponible.");

        ps.Copies = (short)cfg.Copias;
        ps.Collate = cfg.Intercalar;
        if (ps.CanDuplex)
        {
            ps.Duplex = cfg.DuplexIndex switch
            {
                1 => Duplex.Vertical,
                2 => Duplex.Horizontal,
                _ => Duplex.Simplex
            };
        }

        doc.DocumentName = Path.GetFileName(rutaPdf);
        doc.DefaultPageSettings.Color = cfg.Color && ps.SupportsColor;
        doc.DefaultPageSettings.Margins = new Margins(25, 25, 25, 25); // 1/4 de pulgada

        if (!string.IsNullOrEmpty(cfg.PapelSeleccionado))
        {
            var papel = ps.PaperSizes.Cast<PaperSize>().FirstOrDefault(p => p.PaperName == cfg.PapelSeleccionado);
            if (papel != null) doc.DefaultPageSettings.PaperSize = papel;
        }

        int idx = 0;

        // Orientación automática por página (solo en modos que usan la hoja completa)
        doc.QueryPageSettings += (_, e) =>
        {
            var size = pdf.PageSizes[paginas[idx]];
            bool usaHojaCompleta = modo == ModoEscala.Ajustar || modo == ModoEscala.TamanoReal;
            e.PageSettings.Landscape = usaHojaCompleta && size.Width > size.Height;
        };

        doc.PrintPage += (_, e) =>
        {
            int pagina = paginas[idx];
            var size = pdf.PageSizes[pagina];           // en puntos (1/72 pulg)
            float pw = size.Width / 72f * 100f;         // a centésimas de pulgada
            float ph = size.Height / 72f * 100f;

            RectangleF area = e.MarginBounds;
            if (modo == ModoEscala.MitadHoja)
                area = new RectangleF(area.X, area.Y, area.Width, area.Height / 2f);

            float escala = modo switch
            {
                ModoEscala.TamanoReal => 1f,
                ModoEscala.AnchoCompleto => area.Width / pw,
                _ => Math.Min(area.Width / pw, area.Height / ph)
            };

            float w = pw * escala, h = ph * escala;
            float x = modo == ModoEscala.TamanoReal ? area.X : area.X + (area.Width - w) / 2f;
            float y = area.Y;

            const int dpi = 200;
            int pxW = Math.Max(1, (int)(w / 100f * dpi));
            int pxH = Math.Max(1, (int)(h / 100f * dpi));

            using var bmp = pdf.Render(pagina, pxW, pxH, dpi, dpi,
                PdfRenderFlags.ForPrinting | PdfRenderFlags.Annotations);

            e.Graphics!.SetClip(area);
            e.Graphics.DrawImage(bmp, x, y, w, h);

            idx++;
            e.HasMorePages = idx < paginas.Count;
        };

        doc.Print();
    }
}