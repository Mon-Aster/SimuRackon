using System.Diagnostics;
using System.Reflection;
using SimuRackon.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        ApplicationConfiguration.Initialize();
        try
        {
            using var ventana = new TorreForm();
            ventana.Show();
            Application.DoEvents();
            T Control<T>(string nombre) where T : Control =>
                ventana.Controls.Find(nombre, true).OfType<T>().Single();
            void Verificar(bool ok, string caso)
            {
                if (!ok) throw new InvalidOperationException(caso);
                Console.WriteLine("OK: " + caso);
            }
            void Pulsar(string nombre)
            {
                var boton = Control<Button>(nombre);
                if (!boton.CanSelect) throw new InvalidOperationException("Botón no disponible: " + nombre);
                boton.PerformClick();
                Application.DoEvents();
            }
            string Texto(string nombre) => Control<Label>(nombre).Text;

            var mapa = Control<MapaAeropuerto>("mapa");
            Verificar(mapa.Parent == Control<Panel>("contenedorMapa") && mapa.Dock == DockStyle.Fill,
                "Mapa creado y alojado en el formulario");
            Verificar(ventana.Visible && mapa.Visible && mapa.Width > 0 && mapa.Height > 0 && Texto("puntosValor") == "100 pts",
                "Arranque, mapa visible y puntuación inicial");
            Verificar(Texto("tituloLista") == "Salidas" && Texto("tituloLlegadas") == "Llegadas", "Encabezados por columna");
            Verificar(ventana.Controls.Find("cambiarClima", true).Length == 0, "Sin botón de clima manual");
            var lista = Control<TableLayoutPanel>("lista");
            for (int i = 1; i <= 6; i++)
            {
                var tarjeta = Control<Button>("tarjeta" + i);
                Verificar(lista.GetColumn(tarjeta) == (i - 1) % 2 && !tarjeta.Text.Contains("Llegada") && !tarjeta.Text.Contains("Salida"),
                    "Tarjeta agrupada " + i);
            }
            // La primera llegada está centrada en (60,96) en el mapa de 780 x 430.
            float escala = Math.Min(mapa.ClientSize.Width / 780f, mapa.ClientSize.Height / 430f);
            int x = (int)Math.Round((mapa.ClientSize.Width - 780 * escala) / 2 + 60 * escala);
            int y = (int)Math.Round((mapa.ClientSize.Height - 430 * escala) / 2 + 96 * escala);
            var clicMapa = typeof(MapaAeropuerto).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            clicMapa.Invoke(mapa, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
            string llegada = Control<Button>("tarjeta2").Text.Split('\n')[0].Trim();
            Verificar(Texto("vueloActual").Contains(llegada), "Selección de un vuelo desde el mapa");

            var salida = Control<Button>("tarjeta1");
            string idSalida = salida.Text.Split('\n')[0].Trim();
            string pista = salida.Text.TrimEnd()[^1].ToString();
            Pulsar("tarjeta1");
            Pulsar("autorizar");
            Verificar(Texto("puntosValor") == "120 pts" && Texto("estado" + pista).Contains("Libre"),
                "Despegue autorizado y pista liberada");
            Pulsar("tarjeta2");
            Verificar(Control<Button>("botonPista" + pista).Text.Contains("km") && Control<Button>("botonPista" + pista).Text.Contains("Libre"), "Distancia y estado juntos");
            Pulsar("botonPista" + pista);
            var terminales = Control<ComboBox>("terminales");
            terminales.SelectedIndex = 0;
            Pulsar("autorizar");
            Verificar(Texto("puntosValor") == "140 pts" && Texto("avanceValor") == "2",
                "Aterrizaje autorizado y puntuación acumulada");
            Pulsar("reiniciar");
            Verificar(Control<TableLayoutPanel>("panelConfirmacion").Visible, "Confirmación de reinicio dentro del formulario");
            Pulsar("cancelarReinicio");
            Verificar(Texto("puntosValor") == "140 pts" && !Control<TableLayoutPanel>("panelConfirmacion").Visible,
                "Cancelar reinicio conserva la ronda");

            var espera = Stopwatch.StartNew();
            while (espera.Elapsed < TimeSpan.FromSeconds(12) && salida.Text.StartsWith(idSalida, StringComparison.Ordinal))
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            Verificar(!salida.Text.StartsWith(idSalida, StringComparison.Ordinal) && salida.Enabled, "Reposición de tráfico a los diez segundos");
            // Espero el primer cambio real: ahora puede ser nublado o lluvioso.
            var esperaClima = Stopwatch.StartNew();
            while (esperaClima.Elapsed < TimeSpan.FromSeconds(67) && Texto("climaValor") == "Soleado")
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            string clima = Texto("climaValor");
            Verificar(clima == "Nublado" || clima == "Lluvioso", "Primer cambio automático sin tormenta directa");
            Pulsar("tarjeta3");
            var salidaDemorada = Control<Button>("tarjeta3");
            string pistaDemorada = salidaDemorada.Text.TrimEnd()[^1].ToString();
            Pulsar("autorizar");
            Verificar(Texto("puntosValor") == "140 pts" && salidaDemorada.Text.Contains("Despegando") &&
                Texto("estado" + pistaDemorada).Contains("Despegando"), "Despegue en curso sin liberar pista ni puntuar");
            // Repinto el mapa en pleno despegue para comprobar que el indicador no da errores de dibujo.
            using (var imagen = new Bitmap(mapa.Width, mapa.Height)) mapa.DrawToBitmap(imagen, mapa.ClientRectangle);
            var esperaSalida = Stopwatch.StartNew();
            while (esperaSalida.Elapsed < TimeSpan.FromSeconds(6) && Texto("puntosValor") == "140 pts")
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            Verificar(Texto("puntosValor") == "160 pts" && Texto("estado" + pistaDemorada).Contains("Libre"), "Fin de despegue temporizado");
            Pulsar("reiniciar");
            Pulsar("confirmarReinicio");
            Verificar(Texto("puntosValor") == "100 pts" && Texto("avanceValor") == "0" && Texto("climaValor") == "Soleado",
                "Reinicio completo de la simulación");
            ventana.Close();
            Console.WriteLine("Pruebas de interfaz completadas.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
