using System.Drawing.Drawing2D;
using SimuRackon.Core;

namespace SimuRackon.Desktop;

public sealed class MapaAeropuerto : Control
{
    #region Coordenadas del mapa

    // Trabajo con una medida fija y después escalo el dibujo al panel de la ventana.
    private const float Ancho = 780;
    private const float Alto = 430;
    private const float InicioPista = 138;
    private const float FinPista = 534;
    private const float TerminalX = 654;
    // Coloco las tres pistas horizontales, con la misma longitud y separación.
    private static readonly float[] Filas = { 96, 215, 334 };

    #endregion

    #region Datos y selección

    private Simulacion? simulacion;
    private Simulacion? vistaDiseno;
    private Vuelo? vueloElegido;
    private Pista? pistaElegida;
    private Terminal? terminalElegida;
    public event Action<Vuelo>? VueloSeleccionado;

    #endregion

    #region Constructor y actualización

    public MapaAeropuerto()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Paleta.Fondo;
        AccessibleName = "Mapa esquemático del aeropuerto";
        AccessibleDescription = "Los vuelos y el estado de pistas y terminales también están disponibles como texto.";
        TabStop = false;
    }

    public void Actualizar(Simulacion escenario, Vuelo? vuelo, Pista? pista, Terminal? terminal)
    {
        simulacion = escenario;
        vueloElegido = vuelo;
        pistaElegida = pista;
        terminalElegida = terminal;
        Invalidate();
    }

    #endregion

    #region Escala y selección con el mouse

    private float Escala => Math.Min(ClientSize.Width / Ancho, ClientSize.Height / Alto);
    private PointF Origen(float escala) =>
        new((ClientSize.Width - escala * Ancho) / 2, (ClientSize.Height - escala * Alto) / 2);

    private Vuelo? EncontrarVuelo(Point punto)
    {
        float escala = Escala;
        if (simulacion is null || simulacion.Finalizada || escala <= 0) return null;
        var origen = Origen(escala);
        // Convierto el clic a coordenadas del mapa para localizar el vuelo.
        var posicion = new PointF((punto.X - origen.X) / escala, (punto.Y - origen.Y) / escala);
        for (int i = 0; i < simulacion.Vuelos.Count; i++)
        {
            var vuelo = simulacion.Vuelos[i];
            if (!vuelo.Completado && EtiquetaVuelo(vuelo, i).Contains(posicion)) return vuelo;
        }
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = EncontrarVuelo(e.Location) is null ? Cursors.Default : Cursors.Hand;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && EncontrarVuelo(e.Location) is Vuelo vuelo)
            VueloSeleccionado?.Invoke(vuelo);
    }

    #endregion

    #region Dibujo general

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var escenario = simulacion;
        if (escenario is null && (DesignMode ||
            System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime))
            escenario = vistaDiseno ??= new Simulacion(new Random(1));
        float escala = Escala;
        if (escenario is null || escala <= 0) return;
        Graphics g = e.Graphics;
        var origen = Origen(escala);
        // Ajusto el dibujo al tamaño del panel sin deformar sus proporciones.
        var estado = g.Save();
        g.TranslateTransform(origen.X, origen.Y);
        g.ScaleTransform(escala, escala);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        DibujarTerreno(g, escenario.Clima);
        // Primero dibujo las conexiones para que queden debajo de las pistas y etiquetas.
        DibujarConexiones(g);
        foreach (Pista pista in Aeropuerto.Pistas) DibujarPista(g, escenario, pista);
        foreach (Terminal terminal in Aeropuerto.Terminales) DibujarTerminal(g, escenario, terminal);
        DibujarAproximacion(g, escenario);
        foreach (Pista pista in Aeropuerto.Pistas) DibujarProgreso(g, escenario, pista);
        // Uso las mismas coordenadas para dibujar los vuelos y detectar clics.
        for (int i = 0; i < escenario.Vuelos.Count; i++)
        {
            if (!escenario.Vuelos[i].Completado) DibujarVuelo(g, escenario.Vuelos[i], i);
        }
        using var fuente = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
        using var tinta = new SolidBrush(Paleta.Secundario);
        g.DrawString("Llegadas", fuente, tinta, 24, 16);
        g.DrawString("Pistas", fuente, tinta, 322, 16);
        g.DrawString("Terminales", fuente, tinta, 596, 16);
        g.DrawString("Salida", fuente, tinta, 719, 16);
        g.DrawString("Mapa sin escala", fuente, tinta, 12, 398);
        g.Restore(estado);
    }

    #endregion

    #region Terreno y calles de rodaje

    private static void DibujarTerreno(Graphics g, CondicionClimatica clima)
    {
        // Yo cambio el fondo según el clima, pero mantengo un estilo limpio para que no distraiga.
        Color fondo;
        switch (clima)
        {
            case CondicionClimatica.Tormenta: fondo = Color.FromArgb(52, 34, 50); break;
            case CondicionClimatica.Lluvioso: fondo = Color.FromArgb(43, 57, 79); break;
            case CondicionClimatica.Nublado: fondo = Color.FromArgb(47, 51, 66); break;
            default: fondo = Paleta.Soleado; break;
        }
        using var relleno = new SolidBrush(fondo);
        g.FillRectangle(relleno, 0, 0, Ancho, Alto);
        // Yo encierro pistas y terminales en un rectángulo para separar el aeropuerto del tráfico entrante.
        using var borde = new Pen(Paleta.Borde, 1.5f);
        g.DrawRectangle(borde, 116, 46, 560, 332);
    }

    private void DibujarConexiones(Graphics g)
    {
        // Yo solo resalto las conexiones de la pista que estoy consultando.
        Pista? pista = pistaElegida;
        if (!pista.HasValue && vueloElegido != null) pista = vueloElegido.Pista;
        if (!pista.HasValue) return;
        float y = Filas[(int)pista.Value];
        foreach (Terminal terminal in Aeropuerto.TerminalesDe(pista.Value))
        {
            float destinoY = Filas[(int)terminal];
            Color color = Paleta.Lavanda;
            if (terminalElegida == terminal) color = Paleta.Acento;
            using var linea = new Pen(color, 2);
            // Trazo calles rectas, con giros de 90 grados en lugar de diagonales.
            PointF[] puntos = { new PointF(FinPista, y), new PointF(584, y),
                new PointF(584, destinoY), new PointF(TerminalX - 46, destinoY) };
            g.DrawLines(linea, puntos);
        }
    }

    #endregion

    #region Pistas y terminales

    private void DibujarPista(Graphics g, Simulacion escenario, Pista pista)
    {
        float y = Filas[(int)pista];
        bool ocupada = escenario.Ocupante(pista) != null;
        bool elegida = pistaElegida == pista || (vueloElegido != null && vueloElegido.Pista == pista);
        // Yo cambio también el fondo de la pista cuando está soleado para mantener la misma paleta.
        Color color = ocupada ? Paleta.Alerta : Paleta.Secundario;
        Color bordeColor = elegida ? Paleta.Acento : Paleta.Borde;
        Color fondoPista = escenario.Clima == CondicionClimatica.Soleado ? Paleta.SoleadoOscuro : Paleta.Control;
        if (elegida && escenario.Clima == CondicionClimatica.Soleado) fondoPista = Paleta.SoleadoClaro;
        else if (elegida) fondoPista = Paleta.Seleccion;
        using var relleno = new SolidBrush(fondoPista);
        using var borde = new Pen(bordeColor, elegida ? 2.5f : 1.5f);
        using var centro = new Pen(Paleta.Secundario, 1.5f) { DashStyle = DashStyle.Dash };
        using var tinta = new SolidBrush(color);
        using var fuente = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel);
        g.FillRectangle(relleno, InicioPista, y - 21, FinPista - InicioPista, 42);
        g.DrawRectangle(borde, InicioPista, y - 21, FinPista - InicioPista, 42);
        g.DrawLine(centro, InicioPista + 15, y, FinPista - 15, y);
        string estado = ocupada ? "Ocupada" : "Libre";
        if (escenario.Ocupante(pista)?.Estado == EstadoVuelo.Despegando)
            estado = escenario.DespeguesSuspendidos ? "En pausa" : "Despegando";
        g.DrawString("Pista " + pista + " · " + estado, fuente, tinta, InicioPista + 4, y - 45);
    }

    private void DibujarTerminal(Graphics g, Simulacion escenario, Terminal terminal)
    {
        // Alineo las terminales con las pistas y mantengo su estado de ocupación visible.
        bool ocupada = escenario.Ocupante(terminal) != null;
        Color color = ocupada ? Paleta.Alerta : Paleta.Secundario;
        if (terminalElegida == terminal) color = Paleta.Acento;
        float y = Filas[(int)terminal];
        string texto = terminal + "\n" + (ocupada ? "Ocupada" : "Libre");
        DibujarEtiqueta(g, new RectangleF(TerminalX - 48, y - 27, 96, 54), texto, Paleta.Control, color, 15);
    }

    // Dibujo un círculo por pista, en el espacio a la derecha de las terminales.
    // El arco indica cuánto ha avanzado la salida y el número muestra los segundos restantes.
    private void DibujarProgreso(Graphics g, Simulacion escenario, Pista pista)
    {
        double? restante = escenario.SegundosRestantesDespegue(pista);
        bool tormenta = escenario.DespeguesSuspendidos && !escenario.Finalizada;
        var circulo = new RectangleF(718, Filas[(int)pista] - 22, 44, 44);
        using var fondo = new SolidBrush(Paleta.Control);
        using var borde = new Pen(Paleta.Borde, 3);
        g.FillEllipse(fondo, circulo);
        g.DrawEllipse(borde, circulo);
        string texto = "—";
        Color color = Paleta.Secundario;
        if (restante.HasValue)
        {
            color = tormenta ? Paleta.Alerta : Paleta.Cielo;
            float angulo = (float)(360 * escenario.ProgresoDespegue(pista));
            using var arco = new Pen(color, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (angulo > 0) g.DrawArc(arco, circulo, -90, Math.Min(359.9f, angulo));
            texto = (int)Math.Ceiling(restante.Value) + " s";
        }
        else if (tormenta)
        {
            color = Paleta.Alerta;
            texto = "×";
        }
        else
        {
            // Dejo una marca de salida completada hasta que llegue el reemplazo de ese vuelo.
            foreach (Vuelo vuelo in escenario.Vuelos)
            {
                if (vuelo.Estado == EstadoVuelo.Despegado && vuelo.Pista == pista)
                {
                    texto = "✓";
                    color = Paleta.Acento;
                    break;
                }
            }
        }
        if (tormenta)
        {
            using var alerta = new Pen(Paleta.Alerta, 2) { DashStyle = DashStyle.Dot };
            g.DrawEllipse(alerta, circulo);
        }
        using var fuente = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
        using var tinta = new SolidBrush(color);
        using var alineacion = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(texto, fuente, tinta, circulo, alineacion);
    }

    #endregion

    #region Vuelos y etiquetas

    private void DibujarAproximacion(Graphics g, Simulacion escenario)
    {
        // Yo no lleno el mapa de líneas: dibujo la ruta solo cuando elijo una llegada y una pista.
        if (vueloElegido == null || vueloElegido.Operacion != TipoOperacion.Llegada ||
            vueloElegido.Completado || !pistaElegida.HasValue) return;
        int indice = -1;
        for (int i = 0; i < escenario.Vuelos.Count; i++)
            if (escenario.Vuelos[i] == vueloElegido) { indice = i; break; }
        if (indice < 0) return;
        RectangleF etiqueta = EtiquetaVuelo(vueloElegido, indice);
        float y = etiqueta.Y + etiqueta.Height / 2;
        float destinoY = Filas[(int)pistaElegida.Value];
        using var linea = new Pen(Paleta.Acento, 2) { DashStyle = DashStyle.Dash };
        PointF[] puntos = { new PointF(etiqueta.Right, y), new PointF(108, y),
            new PointF(108, destinoY), new PointF(InicioPista, destinoY) };
        g.DrawLines(linea, puntos);
    }

    private void DibujarVuelo(Graphics g, Vuelo vuelo, int indice)
    {
        // Yo resalto solo el vuelo que estoy atendiendo. Los demás siguen siendo seleccionables.
        RectangleF rectangulo = EtiquetaVuelo(vuelo, indice);
        bool elegido = vuelo == vueloElegido;
        DibujarEtiqueta(g, rectangulo, vuelo.Identificador, elegido ? Paleta.Acento : Paleta.Control,
            elegido ? Paleta.Fondo : Paleta.Lavanda, 17);
    }

    private static RectangleF EtiquetaVuelo(Vuelo vuelo, int indice)
    {
        // Yo dejo las salidas sobre su pista real y ordeno las llegadas a la izquierda.
        PointF centro;
        if (vuelo.Pista.HasValue)
            centro = new PointF(336, Filas[(int)vuelo.Pista.Value]);
        else
            centro = new PointF(60, Filas[indice / 2]);
        return new RectangleF(centro.X - 49, centro.Y - 16, 98, 32);
    }

    private static void DibujarEtiqueta(Graphics g, RectangleF rectangulo, string texto, Color fondo,
        Color frente, float tamano)
    {
        using var relleno = new SolidBrush(fondo);
        using var tinta = new SolidBrush(frente);
        using var borde = new Pen(frente, 1.2f);
        using var fuente = new Font("Consolas", tamano, FontStyle.Bold, GraphicsUnit.Pixel);
        using var alineacion = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.FillRectangle(relleno, rectangulo);
        g.DrawRectangle(borde, rectangulo.X, rectangulo.Y, rectangulo.Width, rectangulo.Height);
        g.DrawString(texto, fuente, tinta, rectangulo, alineacion);
    }

    #endregion
}
