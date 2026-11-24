using SimuRackon.Core;

namespace SimuRackon.Desktop
{
    public partial class TorreForm : Form
    {
        #region Variables y controles

        // Yo creo el mapa por código y dejo su contenedor en el diseñador para poder moverlo.
        private readonly MapaAeropuerto mapa;
        // Yo guardo las etiquetas de ocupación en el mismo orden que las pistas y terminales.
        private readonly Label[] disponibilidad;
        private readonly Button[] tarjetas;
        private readonly Button[] botonesPista;
        private readonly Control[] controlesLlegada;
        private Simulacion simulacion = null!;
        private bool resultadoMostrado;
        private bool confirmacionPendiente;
        private Vuelo? elegido;
        private Pista? pistaElegida;
        // Yo uso esta bandera para evitar que el ComboBox se actualice a sí mismo.
        private bool actualizandoTerminales;

        #endregion

        #region Constructor

        public TorreForm()
        {
            InitializeComponent();
            AjustarSelectorDeTerminales();
            AplicarPaletaPastel();
            // Creo el mapa después de InitializeComponent para que el contenedor ya exista.
            mapa = new MapaAeropuerto
            {
                Name = "mapa",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                TabStop = false
            };
            // Agrupo los controles para actualizarlos con ciclos en vez de repetir código.
            disponibilidad = new Label[] { estadoA, estadoB, estadoC, estadoT1, estadoT2, estadoT3 };
            tarjetas = new Button[] { tarjeta1, tarjeta2, tarjeta3, tarjeta4, tarjeta5, tarjeta6 };
            botonesPista = new Button[] { botonPistaA, botonPistaB, botonPistaC };
            controlesLlegada = new Control[] { tituloPistas, pistas, tituloTerminal, terminales };
            if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
            {
                contenedorMapa.Controls.Add(mapa);
                mapa.VueloSeleccionado += SeleccionarVuelo;
                panelResultados.Visible = false;
                panelConfirmacion.Visible = false;
                // Conecto estos eventos al final para que no se ejecuten con controles sin crear.
                panelAutorizacion.ClientSizeChanged += PanelAutorizacion_ClientSizeChanged;
                Shown += TorreForm_Shown;
            }
        }

        #endregion

        #region Ajustes visuales

        // Yo compacto el ComboBox para que no abra una lista enorme cuando hay pocas terminales.
        private void AjustarSelectorDeTerminales()
        {
            terminales.IntegralHeight = false;
            terminales.MaxDropDownItems = 3;
            terminales.DropDownHeight = 78;
        }

        // Yo aplico colores pastel suaves a los botones sin quitar el tema oscuro del resto de la pantalla.
        private void AplicarPaletaPastel()
        {
            reiniciar.BackColor = Paleta.Lavanda;
            reiniciar.ForeColor = Paleta.Fondo;

            autorizar.BackColor = Paleta.Cielo;
            autorizar.ForeColor = Paleta.Fondo;

            otraSimulacion.BackColor = Paleta.Acento;
            otraSimulacion.ForeColor = Paleta.Fondo;

            verBitacora.BackColor = Paleta.Lavanda;
            verBitacora.ForeColor = Paleta.Fondo;

            cancelarReinicio.BackColor = Paleta.Rosa;
            cancelarReinicio.ForeColor = Paleta.Fondo;

            confirmarReinicio.BackColor = Paleta.Alerta;
            confirmarReinicio.ForeColor = Paleta.Fondo;
        }

        // Yo le doy un color base a cada pista para distinguirla rápido sin leer demasiado.
        private static Color ColorDePista(Pista pista)
        {
            switch (pista)
            {
                case Pista.A: return Paleta.Acento;
                case Pista.B: return Paleta.Lavanda;
                default: return Paleta.Alerta;
            }
        }

        // Yo oscurezco un poco el color cuando la pista está elegida para que se note el enfoque.
        private static Color ColorSeleccionado(Color color)
        {
            return Color.FromArgb(
                Math.Max(0, color.R - 42),
                Math.Max(0, color.G - 42),
                Math.Max(0, color.B - 42));
        }

        #endregion

        #region Eventos de Windows Forms

        private void TorreForm_Shown(object? sender, EventArgs e)
        {
            PanelAutorizacion_ClientSizeChanged(sender, e);
            IniciarRonda();
        }
        private void RelojClima_Tick(object? sender, EventArgs e)
        {
            // Consulto el reloj del simulador para saber si ya toca cambiar el clima.
            if (simulacion == null || RevisarFin()) return;
            simulacion.ActualizarClima();
            // Repinto el círculo de cada pista y actualizo sus segundos sin reiniciar el vuelo.
            ActualizarCuentasDeDespegue();
        }
        // Actualizo el tiempo y reviso el tráfico sin detener la interfaz.
        private void RelojRonda_Tick(object? sender, EventArgs e)
        {
            if (simulacion is null) return;
            ActualizarTiempo();
            if (RevisarFin()) return;
            simulacion.ActualizarTrafico();
            ActualizarCuentaDeReemplazos();
        }
        private void Reiniciar_Click(object? sender, EventArgs e) => SolicitarNuevaRonda();
        // Busco qué tarjeta pulsé y recupero el vuelo de esa posición.
        private void Tarjeta_Click(object? sender, EventArgs e)
        {
            Button? boton = sender as Button;
            if (boton == null || simulacion == null) return;
            int i = Array.IndexOf(tarjetas, boton);
            if (i >= 0) SeleccionarVuelo(simulacion.Vuelos[i]);
        }
        // Uso el índice del botón para saber qué pista escogí.
        private void Pista_Click(object? sender, EventArgs e)
        {
            Button? boton = sender as Button;
            if (boton == null || simulacion == null) return;
            int i = Array.IndexOf(botonesPista, boton);
            if (i >= 0) SeleccionarPista(Aeropuerto.Pistas[i]);
        }
        // Refresco la autorización cuando cambio la terminal elegida.
        private void Terminales_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (simulacion is not null && !actualizandoTerminales) ActualizarAutorizacion();
        }
        private void Autorizar_Click(object? sender, EventArgs e) => AutorizarOperacion();
        // Ajusto los mensajes al ancho del panel para que no se corten.
        private void PanelAutorizacion_ClientSizeChanged(object? sender, EventArgs e)
        {
            if (panelAutorizacion is null || formulario is null) return;
            int ancho = Math.Max(100, panelAutorizacion.ClientSize.Width - formulario.Padding.Horizontal - 4);
            foreach (Label etiqueta in new[] { indicacion, resumen, respuesta, resultadoVuelos, resultadoBalance, tituloConfirmacion, detalleConfirmacion })
                if (etiqueta is not null) etiqueta.MaximumSize = new Size(ancho, 0);
        }
        private void CancelarReinicio_Click(object? sender, EventArgs e)
        {
            if (!confirmacionPendiente) return;
            CerrarConfirmacion();
            if (!RevisarFin()) ActualizarVista();
        }
        private void ConfirmarReinicio_Click(object? sender, EventArgs e)
        {
            if (!confirmacionPendiente) return;
            CerrarConfirmacion();
            if (!RevisarFin()) IniciarRonda();
        }
        private void OtraSimulacion_Click(object? sender, EventArgs e) => IniciarRonda();
        private void VerBitacora_Click(object? sender, EventArgs e)
        {
            panelResultados.Visible = false;
            formulario.Visible = true;
            AcceptButton = CancelButton = null;
            bitacora.Focus();
        }

        #endregion

        #region Reinicio y resultados

        private void SolicitarNuevaRonda()
        {
            if (simulacion is null || confirmacionPendiente) return;
            if (!resultadoMostrado)
            {
                if (RevisarFin()) return;
                if (simulacion.Aciertos + simulacion.Errores > 0 || HayDespeguesEnCurso())
                {
                    confirmacionPendiente = true;
                    formulario.Visible = false;
                    panelResultados.Visible = false;
                    panelConfirmacion.Visible = true;
                    lista.Enabled = mapa.Enabled = reiniciar.Enabled = false;
                    AcceptButton = CancelButton = cancelarReinicio;
                    panelAutorizacion.AutoScrollPosition = Point.Empty;
                    cancelarReinicio.Focus();
                    return;
                }
            }
            IniciarRonda();
        }

        private void CerrarConfirmacion()
        {
            confirmacionPendiente = false;
            panelConfirmacion.Visible = false;
            formulario.Visible = true;
            lista.Enabled = mapa.Enabled = reiniciar.Enabled = true;
            AcceptButton = CancelButton = null;
        }

        private void MostrarResultados(string causa)
        {
            resultadoMotivo.Text = causa;
            resultadoPuntos.Text = $"{simulacion.Puntos} pts";
            resultadoVuelos.Text = $"Vuelos atendidos: {simulacion.Completados} de {simulacion.TotalGenerados}";
            resultadoBalance.Text = simulacion.Porcentaje is int porcentaje
                ? $"{simulacion.Aciertos} aciertos · {simulacion.Errores} errores · {porcentaje} % de aciertos"
                : "Sin intentos";
            formulario.Visible = false;
            panelConfirmacion.Visible = false;
            panelResultados.Visible = true;
            AcceptButton = otraSimulacion;
            CancelButton = verBitacora;
            panelAutorizacion.AutoScrollPosition = Point.Empty;
            otraSimulacion.Focus();
        }

        #endregion

        #region Inicio y tráfico

        private void IniciarRonda()
        {
            // Detengo los relojes y desconecto la ronda anterior antes de crear otra.
            relojRonda.Stop();
            relojClima.Stop();
            if (simulacion != null)
            {
                simulacion.VuelosIncorporados -= IncorporarVuelos;
                simulacion.ClimaCambiado -= RegistrarCambioClima;
                simulacion.DespegueCompletado -= RegistrarDespegueCompletado;
            }
            // Yo creo una simulación nueva para arrancar la ronda desde un estado limpio.
            simulacion = new Simulacion();
            simulacion.VuelosIncorporados += IncorporarVuelos;
            simulacion.ClimaCambiado += RegistrarCambioClima;
            simulacion.DespegueCompletado += RegistrarDespegueCompletado;
            resultadoMostrado = false;
            CerrarConfirmacion();
            panelResultados.Visible = false;
            elegido = simulacion.Vuelos[0];
            pistaElegida = null;
            respuesta.Text = "";
            resumen.Text = "";
            bitacora.Clear();
            LlenarTerminales();
            AgregarEvento("Inicio de ronda", "Autoriza los despegues para liberar las pistas.", Paleta.Lavanda);
            ActualizarTiempo();
            ActualizarVista();
            relojClima.Start();
            relojRonda.Start();
            panelAutorizacion.AutoScrollPosition = Point.Empty;
        }

        // Cuando llega tráfico nuevo, registro cada vuelo y actualizo las opciones.
        private void IncorporarVuelos(IReadOnlyList<Vuelo> nuevos)
        {
            foreach (var vuelo in nuevos)
            {
                string ubicacion = vuelo.Operacion == TipoOperacion.Salida
                    ? $"Espera para despegar en la pista {vuelo.Pista}."
                    : "En aproximación, esperando pista.";
                AgregarEvento("Nuevo vuelo · " + vuelo.Identificador, ubicacion, Paleta.Lavanda);
            }

            if (elegido == null) elegido = nuevos[0];
            // Actualizo las terminales por si una llegada nueva dejó libre alguna.
            LlenarTerminales(conservarSeleccion: true);
            ActualizarVista();
        }

        // Muestro cuánto falta para que vuelva a estar disponible cada tarjeta atendida.
        private void ActualizarCuentaDeReemplazos()
        {
            for (int i = 0; i < tarjetas.Length; i++)
            {
                var vuelo = simulacion.Vuelos[i];
                int? segundos = simulacion.SegundosParaReemplazo(i);
                if (!segundos.HasValue) continue;
                string texto = $"{vuelo.Identificador} · Atendido{Environment.NewLine}Nuevo en {segundos} s";
                if (tarjetas[i].Text != texto) tarjetas[i].Text = texto;
            }
        }

        #endregion

        #region Selección de vuelo, pista y terminal

        // Al elegir otro avión, limpio la selección anterior para evitar confusiones.
        private void SeleccionarVuelo(Vuelo vuelo)
        {
            if (simulacion is null || confirmacionPendiente || RevisarFin() || vuelo.Completado || vuelo == elegido) return;
            elegido = vuelo;
            pistaElegida = null;
            respuesta.Text = "";
            LlenarTerminales();
            ActualizarVista();
        }

        // Al cambiar de pista, vuelvo a cargar únicamente sus terminales compatibles.
        private void SeleccionarPista(Pista pista)
        {
            if (simulacion is null || confirmacionPendiente || RevisarFin() || elegido?.Operacion != TipoOperacion.Llegada || pistaElegida == pista) return;
            pistaElegida = pista;
            LlenarTerminales();
            ActualizarAutorizacion();
        }

        private void LlenarTerminales(bool conservarSeleccion = false)
        {
            Terminal? anterior = conservarSeleccion ? TerminalElegida : null;
            // Evito que el ComboBox dispare su evento mientras cambio las opciones.
            actualizandoTerminales = true;
            terminales.BeginUpdate();
            terminales.Items.Clear();
            if (pistaElegida.HasValue)
            {
                var opciones = Aeropuerto.TerminalesDe(pistaElegida.Value);
                foreach (var terminal in opciones)
                {
                    terminales.Items.Add(new OpcionTerminal(terminal, simulacion.Ocupante(terminal) is not null));
                }
                int seleccion = opciones.Count == 1 ? 0 : -1;
                for (int i = 0; i < opciones.Count; i++)
                    if (opciones[i] == anterior) seleccion = i;
                terminales.SelectedIndex = seleccion;
            }
            terminales.EndUpdate();
            actualizandoTerminales = false;
        }

        #endregion

        #region Cambios de clima

        // Registro el cambio automático y actualizo las advertencias del formulario.
        private void RegistrarCambioClima()
        {
            string efecto = simulacion.AterrizajesSuspendidos ? " Aterrizajes y despegues suspendidos."
                : $" Despegues nuevos: {simulacion.SegundosDeDespegue} s.";
            AgregarEvento("Cambio de clima · " + simulacion.Clima,
                "Visibilidad " + simulacion.Visibilidad.ToLowerInvariant() + "." + efecto,
                simulacion.AterrizajesSuspendidos ? Paleta.Alerta : Paleta.Lavanda);
            ActualizarVista();
        }

        #endregion

        #region Autorizaciones

        // Envío la decisión al simulador; aquí solo muestro el resultado que me devuelve.
        private void AutorizarOperacion()
        {
            if (simulacion is null || confirmacionPendiente || RevisarFin() || elegido is null) return;
            var resultado = simulacion.Autorizar(elegido, pistaElegida, TerminalElegida);
            if (resultado is null)
            {
                RevisarFin();
                return;
            }
            Color color = resultado.Aceptada ? Paleta.Acento : Paleta.Alerta;
            string titulo = resultado.Titulo;
            if (!resultado.EnProceso)
            {
                string puntos = resultado.CambioPuntos > 0 ? "+" + resultado.CambioPuntos : "−" + Math.Abs(resultado.CambioPuntos);
                titulo += " · " + puntos + " pts";
            }
            respuesta.ForeColor = color;
            respuesta.Text = titulo + Environment.NewLine + resultado.Explicacion;
            AgregarEvento(titulo, resultado.Explicacion, color);
            // Si el avión ya está despegando, puedo seleccionar otro sin perder su cuenta.
            if (resultado.Aceptada) ElegirSiguienteVuelo();
            if (!RevisarFin()) ActualizarVista();
        }

        // Busco primero un vuelo que aún necesite autorización; si todos están en proceso, muestro uno de ellos.
        private void ElegirSiguienteVuelo()
        {
            elegido = null;
            foreach (Vuelo vuelo in simulacion.Vuelos)
            {
                if (vuelo.Completado) continue;
                if (elegido == null) elegido = vuelo;
                if (vuelo.Estado != EstadoVuelo.Despegando)
                {
                    elegido = vuelo;
                    break;
                }
            }
            pistaElegida = null;
            LlenarTerminales();
        }

        private bool HayDespeguesEnCurso()
        {
            foreach (Vuelo vuelo in simulacion.Vuelos)
                if (vuelo.Estado == EstadoVuelo.Despegando) return true;
            return false;
        }

        // Registro el acierto cuando el núcleo me avisa que el avión ya terminó de despegar.
        private void RegistrarDespegueCompletado(Vuelo vuelo)
        {
            AgregarEvento("Despegue completado · +20 pts",
                $"{vuelo.Identificador} liberó la pista {vuelo.Pista}.", Paleta.Acento);
            if (elegido == vuelo) ElegirSiguienteVuelo();
            ActualizarVista();
        }

        #endregion

        #region Tiempo y cierre de ronda

        // Convierto los segundos restantes al formato minutos:segundos.
        private void ActualizarTiempo()
        {
            int segundos = (int)Math.Ceiling(simulacion.TiempoRestante.TotalSeconds);
            tiempoValor.Text = $"{segundos / 60:00}:{segundos % 60:00}";
            tiempoValor.ForeColor = segundos <= 30 ? Paleta.Alerta : Paleta.Acento;
        }

        private bool RevisarFin()
        {
            if (!simulacion.Finalizada) return false;
            if (resultadoMostrado) return true;
            // Cuento los despegues que terminaron antes de las 00:00 aunque el último tick se haya retrasado.
            simulacion.ActualizarClima();
            if (confirmacionPendiente) CerrarConfirmacion();
            // Marco el cierre para no guardar dos veces la misma ronda.
            resultadoMostrado = true;
            relojRonda.Stop();
            relojClima.Stop();
            elegido = null;
            pistaElegida = null;
            LlenarTerminales();
            const string causa = "Se agotó el tiempo.";
            resumen.Text = $"{causa} Puntos finales: {simulacion.Puntos}.";
            AgregarEvento("Fin de la ronda", resumen.Text, Paleta.Acento);
            ActualizarTiempo();
            ActualizarVista();
            try
            {
                RegistroSesiones.Guardar(simulacion);
                AgregarEvento("Resultado guardado", "Historial en " + RegistroSesiones.Ruta, Paleta.Acento);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                AgregarEvento("Historial sin guardar", "No se pudo escribir en " + RegistroSesiones.Ruta, Paleta.Alerta);
            }
            MostrarResultados(causa);
            return true;
        }

        #endregion

        #region Actualización de la pantalla

        private Terminal? TerminalElegida => (terminales.SelectedItem as OpcionTerminal)?.Id;

        private void ActualizarVista()
        {
            // Refresco los indicadores, las tarjetas y el estado de las pistas y terminales.
            climaValor.Text = simulacion.Clima.ToString();
            switch (simulacion.Clima)
            {
                case CondicionClimatica.Soleado:
                    climaValor.ForeColor = Paleta.SoleadoClaro;
                    break;
                case CondicionClimatica.Nublado:
                    climaValor.ForeColor = Paleta.Secundario;
                    break;
                case CondicionClimatica.Lluvioso:
                    climaValor.ForeColor = Paleta.Lavanda;
                    break;
                default:
                    climaValor.ForeColor = Paleta.Alerta;
                    break;
            }
            climaDetalle.Text = simulacion.DespeguesSuspendidos ? "Operaciones bloqueadas"
                : $"Visibilidad {simulacion.Visibilidad.ToLowerInvariant()} · Salida {simulacion.SegundosDeDespegue} s";
            puntosValor.Text = simulacion.Puntos + " pts";
            avanceValor.Text = simulacion.Completados.ToString();
            precisionValor.Text = simulacion.Porcentaje is int porcentaje ? porcentaje + " %" : "—";
            precisionDetalle.Text = simulacion.Porcentaje.HasValue
                ? $"{simulacion.Aciertos} correctas · {simulacion.Errores} erróneas" : "Sin intentos";
            for (int i = 0; i < tarjetas.Length; i++)
            {
                var vuelo = simulacion.Vuelos[i];
                string estado;
                switch (vuelo.Estado)
                {
                    case EstadoVuelo.EsperandoDespegue: estado = $"En pista {vuelo.Pista}"; break;
                    case EstadoVuelo.Despegando: estado = TextoDespegue(vuelo); break;
                    case EstadoVuelo.Aterrizado: estado = $"En terminal {vuelo.Terminal}"; break;
                    case EstadoVuelo.Despegado: estado = "En vuelo"; break;
                    default: estado = vuelo.EstadoVisible; break;
                }
                // Ya tengo encabezados por columna, así que no repito Llegada o Salida.
                tarjetas[i].Text = $"{vuelo.Identificador}{Environment.NewLine}{estado}";
                tarjetas[i].BackColor = vuelo == elegido ? Paleta.Seleccion : Paleta.Control;
                tarjetas[i].FlatAppearance.BorderColor = vuelo == elegido ? Paleta.Acento : Paleta.Borde;
                tarjetas[i].AccessibleName = vuelo.Identificador + " · " + vuelo.Operacion;
                tarjetas[i].AccessibleDescription = (vuelo == elegido ? "Seleccionado. " : "") + vuelo.EstadoVisible;
                tarjetas[i].Enabled = !resultadoMostrado && !vuelo.Completado;
            }
            for (int i = 0; i < Aeropuerto.Pistas.Count; i++)
            {
                var pista = Aeropuerto.Pistas[i];
                MostrarOcupacion(disponibilidad[i], pista.ToString(), simulacion.Ocupante(pista) is not null);
                MostrarEstadoDespegue(i, pista);
            }
            for (int i = 0; i < Aeropuerto.Terminales.Count; i++)
            {
                var terminal = Aeropuerto.Terminales[i];
                MostrarOcupacion(disponibilidad[i + Aeropuerto.Pistas.Count], terminal.ToString(),
                    simulacion.Ocupante(terminal) is not null);
            }

            // Yo refresco primero la decisión actual y luego, si hace falta, la reposición de vuelos.
            ActualizarAutorizacion();
            if (!simulacion.Finalizada) ActualizarCuentaDeReemplazos();
        }

        // Yo muestro únicamente las decisiones que necesita tomar el vuelo seleccionado.
        private void ActualizarAutorizacion()
        {
            var vuelo = elegido;
            bool hayVuelo = vuelo is not null;
            bool llegada = vuelo?.Operacion == TipoOperacion.Llegada;
            bool esperando = !hayVuelo && !simulacion.Finalizada;
            vueloActual.Text = hayVuelo ? "Vuelo: " + vuelo!.Identificador
                : esperando ? "Esperando tráfico" : "Resultado de la ronda";
            // Oculto los controles que no necesito para el tipo de vuelo elegido.
            foreach (var control in controlesLlegada) control.Visible = llegada;
            indicacion.Visible = hayVuelo || esperando;
            if (esperando)
            {
                indicacion.ForeColor = Paleta.Secundario;
                indicacion.Text = "Esperando nuevos vuelos.";
            }
            autorizar.Visible = hayVuelo;
            resumen.Visible = simulacion.Finalizada;
            respuesta.Visible = respuesta.Text.Length > 0;

            if (vuelo is not null)
            {
                // Muestro primero las restricciones del clima y luego qué debe hacer el jugador.
                if (vuelo.Estado == EstadoVuelo.Despegando)
                    indicacion.Text = TextoDespegue(vuelo) + " · Pista " + vuelo.Pista;
                else if (simulacion.AterrizajesSuspendidos)
                    indicacion.Text = "Tormenta: aterrizajes y despegues suspendidos.";
                else if (llegada)
                    indicacion.Text = "Elige una pista y una terminal.";
                else
                    indicacion.Text = $"Pista {vuelo.Pista} · Despegue en {simulacion.SegundosDeDespegue} s al autorizar.";
                // Conservo las distancias de las salidas como texto, sin mostrar botones que no usan.
                if (!llegada)
                    indicacion.Text += Environment.NewLine + $"A: {vuelo.Distancias[Pista.A]} km  ·  B: {vuelo.Distancias[Pista.B]} km  ·  C: {vuelo.Distancias[Pista.C]} km";
                indicacion.ForeColor = simulacion.AterrizajesSuspendidos || (!llegada && simulacion.DespeguesSuspendidos)
                    ? Paleta.Alerta : Paleta.Secundario;
                // Junto distancia y disponibilidad en el mismo botón para comparar de un vistazo.
                for (int i = 0; i < botonesPista.Length; i++)
                {
                    Pista pista = Aeropuerto.Pistas[i];
                    bool ocupada = simulacion.Ocupante(pista) is not null;
                    Color baseColor = ColorDePista(pista);
                    botonesPista[i].Text = $"{pista} · {(ocupada ? "Ocupada" : "Libre")}{Environment.NewLine}{vuelo.Distancias[pista]} km";
                    botonesPista[i].AccessibleName = "Pista " + botonesPista[i].Text;
                    botonesPista[i].BackColor = pista == pistaElegida ? ColorSeleccionado(baseColor) : baseColor;
                    botonesPista[i].FlatAppearance.BorderColor = pista == pistaElegida ? Paleta.Texto : Paleta.Borde;
                    botonesPista[i].ForeColor = Paleta.Fondo;
                }
                terminales.Enabled = pistaElegida.HasValue;
                autorizar.Text = llegada ? "Autorizar aterrizaje" : "Autorizar despegue";
                if (simulacion.AterrizajesSuspendidos) autorizar.Text = "Operaciones suspendidas";
                else if (vuelo.Estado == EstadoVuelo.Despegando) autorizar.Text = "Despegue en curso";
                autorizar.Enabled = !simulacion.AterrizajesSuspendidos && vuelo.Estado != EstadoVuelo.Despegando
                    && (!llegada || TerminalElegida.HasValue);
                autorizar.BackColor = autorizar.Enabled ? Paleta.Cielo : Paleta.Control;
                autorizar.ForeColor = autorizar.Enabled ? Paleta.Fondo : Paleta.Secundario;
            }
            mapa.Actualizar(simulacion, vuelo, pistaElegida, TerminalElegida);
        }

        // Consulto los segundos en el núcleo; no creo un reloj distinto por tarjeta.
        private string TextoDespegue(Vuelo vuelo)
        {
            if (!vuelo.Pista.HasValue) return "Despegando";
            double? restante = simulacion.SegundosRestantesDespegue(vuelo.Pista.Value);
            if (!restante.HasValue) return "Despegue pendiente";
            int segundos = (int)Math.Ceiling(restante.Value);
            return simulacion.DespeguesSuspendidos ? $"Pausa · {segundos} s" : $"Despegando · {segundos} s";
        }

        private void MostrarEstadoDespegue(int indice, Pista pista)
        {
            Vuelo? vuelo = simulacion.Ocupante(pista);
            if (vuelo == null || vuelo.Estado != EstadoVuelo.Despegando) return;
            disponibilidad[indice].Text = pista + " · " + (simulacion.DespeguesSuspendidos ? "En pausa" : "Despegando");
            disponibilidad[indice].ForeColor = simulacion.DespeguesSuspendidos ? Paleta.Alerta : Paleta.Cielo;
        }

        // Solo refresco las cuentas activas y el mapa para no interrumpir el selector de terminales.
        private void ActualizarCuentasDeDespegue()
        {
            if (!HayDespeguesEnCurso()) return;
            for (int i = 0; i < tarjetas.Length; i++)
            {
                Vuelo vuelo = simulacion.Vuelos[i];
                if (vuelo.Estado == EstadoVuelo.Despegando)
                    tarjetas[i].Text = vuelo.Identificador + Environment.NewLine + TextoDespegue(vuelo);
            }
            for (int i = 0; i < Aeropuerto.Pistas.Count; i++)
                MostrarEstadoDespegue(i, Aeropuerto.Pistas[i]);
            if (elegido != null && elegido.Estado == EstadoVuelo.Despegando) ActualizarAutorizacion();
            mapa.Invalidate();
        }

        #endregion

        #region Bitácora y ocupación

        // Yo inserto el evento más reciente arriba para encontrarlo sin desplazarme.
        private void AgregarEvento(string titulo, string texto, Color color)
        {
            bitacora.Select(0, 0);
            bitacora.SelectionColor = color;
            bitacora.SelectedText = titulo + Environment.NewLine;
            bitacora.SelectionColor = Paleta.Secundario;
            bitacora.SelectedText = texto + Environment.NewLine + Environment.NewLine;
            bitacora.Select(0, 0);
            bitacora.ScrollToCaret();
        }

        // Yo acompaño el color con texto para distinguir los estados sin memorizar colores.
        private static void MostrarOcupacion(Label etiqueta, string nombre, bool ocupada)
        {
            etiqueta.Text = nombre + (ocupada ? " · Ocupada" : " · Libre");
            etiqueta.ForeColor = ocupada ? Paleta.Alerta : Paleta.Secundario;
        }

        #endregion

        #region Opciones del selector

        // Yo guardo la terminal y su estado para mostrar ambos datos en el ComboBox.
        private sealed class OpcionTerminal
        {
            public Terminal Id { get; }
            public bool Ocupada { get; }

            public OpcionTerminal(Terminal id, bool ocupada)
            {
                Id = id;
                Ocupada = ocupada;
            }

            public override string ToString()
            {
                return $"Terminal {Id} · {(Ocupada ? "Ocupada" : "Libre")}";
            }
        }

        #endregion
    }
}
