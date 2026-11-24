using System.Globalization;
using System.Text;
using SimuRackon.Core;

namespace SimuRackon.Desktop;

internal static class RegistroSesiones
{
    // Guardo el historial junto al ejecutable para no dejar archivos regados en AppData.
    public static string Ruta { get; } = Path.Combine(AppContext.BaseDirectory, "sesiones.csv");

    public static void Guardar(Simulacion simulacion)
    {
        string? carpeta = Path.GetDirectoryName(Ruta);
        if (!string.IsNullOrWhiteSpace(carpeta)) Directory.CreateDirectory(carpeta);
        using var archivo = new FileStream(Ruta, FileMode.Append, FileAccess.Write, FileShare.Read);
        // Escribo el encabezado solo si el archivo está vacío; después agrego una fila por ronda.
        bool escribirEncabezado = archivo.Length == 0;
        using var csv = new StreamWriter(archivo, new UTF8Encoding(true));
        if (escribirEncabezado) csv.WriteLine("Fecha;Puntos;Aciertos;Errores;Porcentaje;Vuelos completados");
        string fecha = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        csv.WriteLine(FormattableString.Invariant(
            $"{fecha};{simulacion.Puntos};{simulacion.Aciertos};{simulacion.Errores};{simulacion.Porcentaje};{simulacion.Completados}"));
    }
}
