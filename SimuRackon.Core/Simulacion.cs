namespace SimuRackon.Core;

public enum CondicionClimatica { Soleado, Nublado, Lluvioso, Tormenta }
public enum FinDeRonda { VuelosCompletados, TiempoAgotado }

// Aquí guardo el resultado de cada autorización para mostrarlo en la torre.
public sealed class ResultadoAutorizacion
{
    public string Titulo { get; }
    public string Explicacion { get; }
    public int CambioPuntos { get; }
    public bool EnProceso { get; }

    public ResultadoAutorizacion(string titulo, string explicacion, int cambioPuntos, bool enProceso = false)
    {
        Titulo = titulo;
        Explicacion = explicacion;
        CambioPuntos = cambioPuntos;
        EnProceso = enProceso;
    }

    public bool Aceptada
    {
        get { return CambioPuntos > 0 || EnProceso; }
    }
}

public sealed class Simulacion
{
    #region Datos de la ronda

    public const int CantidadVuelos = 6;
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DemoraReemplazo = TimeSpan.FromSeconds(10);

    private static readonly string[] Prefijos = { "AMX", "VOI", "VIV", "MXA", "LNE", "RKN" };
    private readonly Vuelo[] vuelos = new Vuelo[CantidadVuelos];
    // Guardo el momento en que debo reemplazar cada vuelo. Null significa que todavía no lo atiendo.
    private readonly TimeSpan?[] entradas = new TimeSpan?[CantidadVuelos];
    private readonly Random aleatorio;
    // Uso el mismo reloj para toda la ronda; en las pruebas puedo adelantarlo sin esperar.
    private readonly TimeProvider reloj;
    private readonly long inicio;
    private readonly int primerPrefijo;
    private int siguienteNumero;
    private TimeSpan proximoCambioClima;
    // Guardo por separado el plazo y la duración de cada despegue; así puedo atender otras pistas.
    private readonly TimeSpan?[] finDespegue = new TimeSpan?[CantidadVuelos];
    private readonly TimeSpan[] duracionDespegue = new TimeSpan[CantidadVuelos];
    // Cuando hay tormenta, congelo los plazos hasta que vuelva a permitir operaciones.
    private TimeSpan? inicioPausa;

    #endregion

    #region Propiedades y eventos

    public IReadOnlyList<Vuelo> Vuelos { get; }
    public event Action<IReadOnlyList<Vuelo>>? VuelosIncorporados;
    public event Action? ClimaCambiado;
    public event Action<Vuelo>? DespegueCompletado;
    public CondicionClimatica Clima { get; private set; } = CondicionClimatica.Soleado;
    public int Puntos { get; private set; } = 100;
    public int Aciertos { get; private set; }
    public int Errores { get; private set; }
    public int Completados => Aciertos;
    public int TotalGenerados { get; private set; } = CantidadVuelos;
    // Calculo el porcentaje solo si ya hice al menos una autorización.
    public int? Porcentaje
    {
        get
        {
            int intentos = Aciertos + Errores;
            if (intentos == 0) return null;
            return (int)Math.Round(100.0 * Aciertos / intentos, MidpointRounding.AwayFromZero);
        }
    }

    // Resto el tiempo transcurrido y evito mostrar valores negativos.
    public TimeSpan TiempoRestante
    {
        get
        {
            TimeSpan restante = Duracion - reloj.GetElapsedTime(inicio);
            return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
        }
    }

    public bool Finalizada => TiempoRestante == TimeSpan.Zero;
    public FinDeRonda? MotivoDeFin => Finalizada ? FinDeRonda.TiempoAgotado : null;

    public bool DespeguesSuspendidos => Clima == CondicionClimatica.Tormenta;
    public bool AterrizajesSuspendidos => Clima == CondicionClimatica.Tormenta;

    // Consulto cuánto falta para el siguiente cambio sin alterar el reloj.
    public TimeSpan TiempoParaCambioClima
    {
        get
        {
            TimeSpan restante = proximoCambioClima - reloj.GetElapsedTime(inicio);
            return Finalizada || restante <= TimeSpan.Zero ? TimeSpan.Zero : restante;
        }
    }

    public string Visibilidad
    {
        get
        {
            switch (Clima)
            {
                case CondicionClimatica.Soleado: return "Buena";
                case CondicionClimatica.Nublado: return "Reducida";
                case CondicionClimatica.Lluvioso: return "Regular";
                case CondicionClimatica.Tormenta: return "Muy baja";
                default: throw new InvalidOperationException("Clima desconocido.");
            }
        }
    }

    #endregion

    #region Inicio de la simulación

    public Simulacion(Random? aleatorio = null, TimeProvider? reloj = null)
    {
        this.aleatorio = aleatorio ?? Random.Shared;
        this.reloj = reloj ?? TimeProvider.System;
        Pista[] pistas = { Pista.A, Pista.B, Pista.C };
        // Mezclo las pistas para que las salidas no empiecen siempre en el mismo orden.
        this.aleatorio.Shuffle(pistas);
        primerPrefijo = this.aleatorio.Next(Prefijos.Length);
        siguienteNumero = this.aleatorio.Next(120, 800);
        // Alterno una salida y una llegada hasta llenar las seis tarjetas.
        for (int i = 0; i < CantidadVuelos; i += 2)
        {
            vuelos[i] = CrearVuelo(i, pistas[i / 2]);
            vuelos[i + 1] = CrearVuelo(i + 1, null);
        }
        Vuelos = Array.AsReadOnly(vuelos);
        inicio = this.reloj.GetTimestamp();
        proximoCambioClima = DuracionClima(Clima);
    }

    #endregion

    #region Generación y reemplazo de vuelos

    // Mantengo el tipo de vuelo de cada tarjeta y le asigno un identificador nuevo.
    private Vuelo CrearVuelo(int tarjeta, Pista? pistaSalida)
    {
        string id = Prefijos[(primerPrefijo + tarjeta) % Prefijos.Length] + siguienteNumero++;
        if (pistaSalida.HasValue)
            return new Vuelo(id, TipoOperacion.Salida, Aeropuerto.DistanciasDesdePista(pistaSalida.Value), pistaSalida.Value);

        // Para las llegadas genero una posición según la zona de aproximación.
        int x, y;
        switch (tarjeta / 2)
        {
            case 0:
                x = aleatorio.Next(-12, -3);
                y = aleatorio.Next(-12, 13);
                break;
            case 1:
                x = aleatorio.Next(34, 44);
                y = aleatorio.Next(7, 25);
                break;
            default:
                x = aleatorio.Next(-16, -5);
                y = aleatorio.Next(17, 29);
                break;
        }
        return new Vuelo(id, TipoOperacion.Llegada, Aeropuerto.DistanciasDesde(x, y));
    }

    // Si la tarjeta está pendiente de reemplazo, redondeo los segundos para mostrar la cuenta.
    public int? SegundosParaReemplazo(int tarjeta)
    {
        TimeSpan ahora = reloj.GetElapsedTime(inicio);
        if (!entradas[tarjeta].HasValue) return null;
        TimeSpan prevista = entradas[tarjeta].Value;
        if (prevista >= Duracion || ahora >= Duracion) return null;
        return (int)Math.Ceiling(Math.Max(0, (prevista - ahora).TotalSeconds));
    }

    public void ActualizarTrafico()
    {
        TimeSpan ahora = reloj.GetElapsedTime(inicio);
        ActualizarClima(ahora);
        ActualizarTrafico(ahora);
    }

    // Reviso cada tarjeta por separado para que sus tiempos no dependan de las otras.
    private void ActualizarTrafico(TimeSpan ahora)
    {
        if (ahora >= Duracion) return;
        List<Vuelo> nuevos = new List<Vuelo>();
        for (int i = 0; i < vuelos.Length; i++)
        {
            if (!entradas[i].HasValue || ahora < entradas[i].Value) continue;
            var anterior = vuelos[i];
            var nuevo = CrearVuelo(i, anterior.Operacion == TipoOperacion.Salida ? anterior.Pista : null);
            // Al reemplazar una llegada, dejo libre su terminal.
            vuelos[i] = nuevo;
            entradas[i] = null;
            TotalGenerados++;
            nuevos.Add(nuevo);
        }
        // Aviso al formulario una sola vez, aunque haya varios vuelos nuevos.
        if (nuevos.Count > 0) VuelosIncorporados?.Invoke(nuevos);
    }

    #endregion

    #region Clima

    // Le doy más tiempo al sol y menos a la tormenta. Next no incluye el último número.
    private TimeSpan DuracionClima(CondicionClimatica clima)
    {
        int segundos;
        switch (clima)
        {
            case CondicionClimatica.Soleado: segundos = aleatorio.Next(45, 66); break;
            case CondicionClimatica.Nublado: segundos = aleatorio.Next(25, 36); break;
            case CondicionClimatica.Lluvioso: segundos = aleatorio.Next(15, 26); break;
            case CondicionClimatica.Tormenta: segundos = aleatorio.Next(4, 8); break;
            default: throw new InvalidOperationException("Clima desconocido.");
        }
        return TimeSpan.FromSeconds(segundos);
    }

    // Elijo un clima distinto al actual. Solo incluyo la tormenta entre las opciones de lluvia.
    private void AvanzarClimaDesde(TimeSpan momento)
    {
        CondicionClimatica anterior = Clima;
        switch (Clima)
        {
            case CondicionClimatica.Soleado:
                Clima = aleatorio.Next(2) == 0 ? CondicionClimatica.Nublado : CondicionClimatica.Lluvioso;
                break;
            case CondicionClimatica.Nublado:
                Clima = aleatorio.Next(2) == 0 ? CondicionClimatica.Soleado : CondicionClimatica.Lluvioso;
                break;
            case CondicionClimatica.Lluvioso:
                int opcion = aleatorio.Next(3);
                if (opcion == 0) Clima = CondicionClimatica.Soleado;
                else if (opcion == 1) Clima = CondicionClimatica.Nublado;
                else Clima = CondicionClimatica.Tormenta;
                break;
            default:
                Clima = aleatorio.Next(2) == 0 ? CondicionClimatica.Soleado : CondicionClimatica.Nublado;
                break;
        }
        // Al entrar en tormenta detengo los despegues. Al salir, recupero exactamente el tiempo pendiente.
        if (Clima == CondicionClimatica.Tormenta) inicioPausa = momento;
        else if (anterior == CondicionClimatica.Tormenta && inicioPausa.HasValue)
        {
            TimeSpan pausa = momento - inicioPausa.Value;
            for (int i = 0; i < finDespegue.Length; i++)
                if (finDespegue[i].HasValue) finDespegue[i] = finDespegue[i].Value + pausa;
            inicioPausa = null;
        }
        proximoCambioClima = momento + DuracionClima(Clima);
    }

    public void ActualizarClima() => ActualizarClima(reloj.GetElapsedTime(inicio));

    private void ActualizarClima(TimeSpan ahora)
    {
        // Recorro los cambios por orden de tiempo, incluso si la ventana se retrasa.
        // No cuento operaciones que terminen exactamente a las 00:00 o después.
        TimeSpan limite = ahora < Duracion ? ahora : Duracion;
        bool cambio = false;
        while (proximoCambioClima <= limite && proximoCambioClima < Duracion)
        {
            CompletarDespegues(proximoCambioClima);
            AvanzarClimaDesde(proximoCambioClima);
            cambio = true;
        }
        CompletarDespegues(limite);
        if (cambio) ClimaCambiado?.Invoke();
    }

    // Conservo el método para pruebas del núcleo, pero no lo conecto a ningún botón.
    public void CambiarClima()
    {
        TimeSpan ahora = reloj.GetElapsedTime(inicio);
        if (ahora >= Duracion) return;
        ActualizarClima(ahora);
        AvanzarClimaDesde(ahora);
        ClimaCambiado?.Invoke();
    }

    #endregion

    #region Tiempos de despegue

    // Consulto el tiempo de despegue que corresponde al clima al autorizar.
    public int SegundosDeDespegue
    {
        get
        {
            switch (Clima)
            {
                case CondicionClimatica.Soleado: return 0;
                case CondicionClimatica.Nublado: return 1;
                case CondicionClimatica.Lluvioso: return 4;
                default: return 0; // En tormenta no autorizo ninguna salida.
            }
        }
    }

    // Uso el mismo plazo para el mapa y las tarjetas, sin modificar el estado al consultarlo.
    public double? SegundosRestantesDespegue(Pista pista)
    {
        Vuelo? vuelo = Ocupante(pista);
        if (vuelo == null || vuelo.Estado != EstadoVuelo.Despegando || Finalizada) return null;
        int i = Array.IndexOf(vuelos, vuelo);
        if (!finDespegue[i].HasValue) return null;
        TimeSpan momento = inicioPausa ?? reloj.GetElapsedTime(inicio);
        return Math.Max(0, (finDespegue[i].Value - momento).TotalSeconds);
    }

    // Calculo una fracción de 0 a 1 para dibujar el círculo sin usar otro temporizador.
    public double ProgresoDespegue(Pista pista)
    {
        double? restante = SegundosRestantesDespegue(pista);
        if (!restante.HasValue) return 0;
        int i = Array.IndexOf(vuelos, Ocupante(pista));
        return Math.Clamp(1 - restante.Value / duracionDespegue[i].TotalSeconds, 0, 1);
    }

    private void CompletarDespegues(TimeSpan ahora)
    {
        if (inicioPausa.HasValue) return;
        for (int i = 0; i < vuelos.Length; i++)
        {
            if (!finDespegue[i].HasValue) continue;
            TimeSpan fin = finDespegue[i].Value;
            if (fin > ahora || fin >= Duracion) continue;
            // Libero la pista y cuento el acierto solo cuando termina el despegue.
            Vuelo vuelo = vuelos[i];
            vuelo.Despegar();
            finDespegue[i] = null;
            Puntos += 20;
            Aciertos++;
            entradas[i] = fin + DemoraReemplazo;
            DespegueCompletado?.Invoke(vuelo);
        }
    }

    #endregion

    #region Ocupación de pistas y terminales

    // Recorro los vuelos para saber si alguien está usando la pista o la terminal.
    public Vuelo? Ocupante(Pista pista)
    {
        foreach (Vuelo vuelo in Vuelos)
        {
            if (!vuelo.Completado && vuelo.Pista == pista) return vuelo;
        }
        return null;
    }

    public Vuelo? Ocupante(Terminal terminal)
    {
        foreach (Vuelo vuelo in Vuelos)
        {
            if (vuelo.Terminal == terminal) return vuelo;
        }
        return null;
    }

    #endregion

    #region Autorizaciones y puntos

    // Antes de evaluar una decisión sincronizo el tráfico y el clima con el reloj.
    public ResultadoAutorizacion? Autorizar(Vuelo vuelo, Pista? pista = null, Terminal? terminal = null)
    {
        TimeSpan ahora = reloj.GetElapsedTime(inicio);
        if (ahora >= Duracion) return null;
        // Antes de autorizar, actualizo el clima y los vuelos para no usar datos atrasados.
        ActualizarClima(ahora);
        ActualizarTrafico(ahora);
        int tarjeta = Array.IndexOf(vuelos, vuelo);
        if (tarjeta < 0 || vuelo.Completado || vuelo.Estado == EstadoVuelo.Despegando) return null;
        ResultadoAutorizacion? resultado;
        if (vuelo.Operacion == TipoOperacion.Salida)
            resultado = EvaluarDespegue(vuelo, tarjeta, ahora);
        else
            resultado = EvaluarAterrizaje(vuelo, pista, terminal);
        if (resultado is null || resultado.EnProceso) return resultado;
        // Solo programo un reemplazo si la operación fue correcta.
        Puntos += resultado.CambioPuntos;
        if (resultado.Aceptada)
        {
            Aciertos++;
            entradas[tarjeta] = ahora + DemoraReemplazo;
        }
        else Errores++;
        return resultado;
    }

    // Compruebo las restricciones antes de liberar la pista del vuelo.
    private ResultadoAutorizacion EvaluarDespegue(Vuelo vuelo, int tarjeta, TimeSpan ahora)
    {
        if (DespeguesSuspendidos)
            return new ResultadoAutorizacion("Despegue rechazado",
                $"La tormenta suspende las operaciones. {vuelo.Identificador} permanece en la pista {vuelo.Pista}.", -20);
        int segundos = SegundosDeDespegue;
        if (segundos > 0)
        {
            // Aparto la pista mientras pasa el tiempo; no uso Sleep porque congelaría la ventana.
            vuelo.IniciarDespegue();
            duracionDespegue[tarjeta] = TimeSpan.FromSeconds(segundos);
            finDespegue[tarjeta] = ahora + duracionDespegue[tarjeta];
            return new ResultadoAutorizacion("Despegue en curso",
                $"{vuelo.Identificador} despegará en {segundos} s por la pista {vuelo.Pista}.", 0, true);
        }
        vuelo.Despegar();
        return new ResultadoAutorizacion("Despegue autorizado",
            $"{vuelo.Identificador} despegó y liberó la pista {vuelo.Pista}.", 20);
    }

    // Compruebo pista, terminal y clima antes de aceptar una llegada.
    private ResultadoAutorizacion? EvaluarAterrizaje(Vuelo vuelo, Pista? pista, Terminal? terminal)
    {
        // Primero compruebo que la pista y la terminal elegidas sean compatibles.
        if (!pista.HasValue || !terminal.HasValue) return null;
        Pista destino = pista.Value;
        Terminal estacion = terminal.Value;
        if (!Aeropuerto.Pistas.Contains(destino) || !Aeropuerto.TerminalesDe(destino).Contains(estacion))
            return null;
        if (AterrizajesSuspendidos)
            return new ResultadoAutorizacion("Aterrizaje rechazado",
                $"La tormenta suspende las operaciones. {vuelo.Identificador} sigue esperando pista.", -20);
        Vuelo? enPista = Ocupante(destino);
        if (enPista != null)
            return new ResultadoAutorizacion("Aterrizaje rechazado",
                $"La pista {destino} está ocupada por {enPista.Identificador}. {vuelo.Identificador} sigue esperando pista.", -10);
        Vuelo? enTerminal = Ocupante(estacion);
        if (enTerminal != null)
            return new ResultadoAutorizacion("Aterrizaje rechazado",
                $"La terminal {estacion} está ocupada por {enTerminal.Identificador}. {vuelo.Identificador} sigue esperando pista.", -10);
        vuelo.Aterrizar(destino, estacion);
        return new ResultadoAutorizacion("Aterrizaje autorizado",
            $"{vuelo.Identificador} aterrizó por la pista {destino} y ocupa la terminal {estacion}.", 20);
    }

    #endregion
}
