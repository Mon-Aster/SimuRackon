using SimuRackon.Core;

static void Verificar(bool condicion, string caso)
{
    if (!condicion) throw new InvalidOperationException(caso);
}

var reloj = new RelojDePrueba();
var ronda = new Simulacion(new Random(17), reloj);
var primero = ronda.Vuelos[0];
var segundo = ronda.Vuelos[2];
var pista = primero.Pista!.Value;
var terminal = Aeropuerto.TerminalesDe(pista)[0];
var llegada = ronda.Vuelos[1];
int notificados = 0;
ronda.VuelosIncorporados += nuevos => notificados += nuevos.Count;

Verificar(ronda.Autorizar(llegada, pista, terminal)?.Aceptada == false, "Rechazar una pista ocupada");
Verificar(ronda.SegundosParaReemplazo(1) == null, "Un rechazo no programa otro vuelo");
Verificar(ronda.Autorizar(primero)?.Aceptada == true, "Primer despegue");
Verificar(ronda.Autorizar(llegada, pista, terminal)?.Aceptada == true, "Aterrizaje simultáneo");
Verificar(ronda.Autorizar(primero) is null, "Evitar doble puntuación");
reloj.Avanzar(3000);
Verificar(ronda.Autorizar(segundo)?.Aceptada == true, "Segundo despegue a los tres segundos");
reloj.Avanzar(6999);
ronda.ActualizarTrafico();
Verificar(ronda.TotalGenerados == 6 && ronda.Ocupante(terminal) == llegada, "Antes de diez segundos");
reloj.Avanzar(1);
ronda.ActualizarTrafico();
Verificar(ronda.TotalGenerados == 8 && ronda.Completados == 3 && ronda.Puntos == 150, "Dos reemplazos simultáneos y marcador acumulado");
Verificar(ronda.Vuelos[0] != primero && ronda.Ocupante(pista) == ronda.Vuelos[0], "Nueva salida en la pista liberada");
Verificar(ronda.Vuelos[1] != llegada && ronda.Ocupante(terminal) is null, "Liberar la terminal al retirar la llegada");
Verificar(ronda.Vuelos[2] == segundo && ronda.SegundosParaReemplazo(2) == 3, "Independencia entre plazos");
reloj.Avanzar(3000);
ronda.ActualizarTrafico();
ronda.ActualizarTrafico();
Verificar(ronda.TotalGenerados == 9 && notificados == 3 && ronda.Vuelos[2] != segundo, "Reposición única al segundo trece");
Verificar(ronda.Autorizar(primero) is null, "Un vuelo sustituido no se puede volver a autorizar");

var otroReloj = new RelojDePrueba();
var todos = new Simulacion(new Random(22), otroReloj);
foreach (var v in todos.Vuelos.Where(v => v.Operacion == TipoOperacion.Salida).ToArray())
    Verificar(todos.Autorizar(v)?.Aceptada == true, "Liberar las tres pistas");
for (int par = 0; par < 3; par++)
    Verificar(todos.Autorizar(todos.Vuelos[2 * par + 1], Aeropuerto.Pistas[par], Aeropuerto.Terminales[par])?.Aceptada == true, "Aterrizaje válido");
Verificar(todos.Completados == 6 && !todos.Finalizada, "Seguir tras completar los primeros seis");
otroReloj.Avanzar(10000);
todos.ActualizarTrafico();
Verificar(todos.TotalGenerados == 12 && todos.Vuelos.All(v => !v.Completado), "Seis nuevos vuelos");
Verificar(todos.Autorizar(todos.Vuelos[0])?.Aceptada == true && todos.Completados == 7, "Más de seis aciertos");

var relojLimite = new RelojDePrueba();
var limite = new Simulacion(new Random(4), relojLimite);
relojLimite.Avanzar(170000);
limite.ActualizarClima();
ForzarClima(limite, CondicionClimatica.Soleado);
limite.Autorizar(limite.Vuelos[0]);
relojLimite.Avanzar(10000);
limite.ActualizarTrafico();
Verificar(limite.TotalGenerados == 6 && limite.Finalizada && limite.TiempoRestante == TimeSpan.Zero,
    "Un reemplazo que coincide con 00:00 no se incorpora");
Verificar(limite.Autorizar(limite.Vuelos[2]) is null && limite.Completados == 1, "Mantener la puntuación al cierre");
var reinicio = new Simulacion(new Random(5), relojLimite);
reinicio.ActualizarTrafico();
Verificar(reinicio.TotalGenerados == 6 && reinicio.Completados == 0 && reinicio.Puntos == 100 &&
    reinicio.TiempoRestante == TimeSpan.FromMinutes(3), "Reinicio limpio");
Console.WriteLine("Pruebas del tráfico continuo completadas.");

// Uso el reloj simulado para verificar muchas rondas sin esperar los tiempos reales.
var transiciones = new HashSet<(CondicionClimatica, CondicionClimatica)>();
for (int semilla = 0; semilla < 100; semilla++)
{
    var relojClima = new RelojDePrueba();
    var clima = new Simulacion(new Random(semilla), relojClima);
    int cambios = 0;
    clima.ClimaCambiado += () => cambios++;
    Verificar(clima.Clima == CondicionClimatica.Soleado, "Inicio soleado");
    while (!clima.Finalizada)
    {
        CondicionClimatica anterior = clima.Clima;
        (int minimo, int maximo) = anterior switch
        {
            CondicionClimatica.Soleado => (45, 65),
            CondicionClimatica.Nublado => (25, 35),
            CondicionClimatica.Lluvioso => (15, 25),
            _ => (4, 7)
        };
        int plazo = (int)clima.TiempoParaCambioClima.TotalMilliseconds;
        Verificar(plazo >= minimo * 1000 && plazo <= maximo * 1000, "Duración de " + anterior);
        if (clima.TiempoRestante.TotalMilliseconds <= plazo) break;
        int anteriores = cambios;
        relojClima.Avanzar(plazo - 1);
        clima.ActualizarClima();
        Verificar(clima.Clima == anterior && cambios == anteriores, "Sin cambio anticipado");
        relojClima.Avanzar(1);
        clima.ActualizarClima();
        clima.ActualizarClima();
        Verificar(cambios == anteriores + 1 && clima.Clima != anterior, "Cambio único y distinto");
        Verificar(clima.Clima != CondicionClimatica.Tormenta || anterior == CondicionClimatica.Lluvioso,
            "Solo la lluvia puede preceder a la tormenta");
        transiciones.Add((anterior, clima.Clima));
    }
    relojClima.Avanzar(180000);
    clima.ActualizarClima();
    int finales = cambios;
    clima.ActualizarClima();
    clima.CambiarClima();
    Verificar(cambios == finales && clima.Finalizada, "No cambiar clima después del cierre");
}
Verificar(transiciones.Contains((CondicionClimatica.Soleado, CondicionClimatica.Nublado)) &&
    transiciones.Contains((CondicionClimatica.Soleado, CondicionClimatica.Lluvioso)) &&
    transiciones.Contains((CondicionClimatica.Lluvioso, CondicionClimatica.Tormenta)) &&
    transiciones.Contains((CondicionClimatica.Lluvioso, CondicionClimatica.Soleado)), "Transiciones aleatorias");

// Compruebo los tiempos de salida, la ocupación, los puntos y el reemplazo después de completar.
foreach (var (estado, segundos) in new[] { (CondicionClimatica.Nublado, 1), (CondicionClimatica.Lluvioso, 4) })
{
    var tiempo = new RelojDePrueba();
    var prueba = new Simulacion(new Random(12), tiempo);
    ForzarClima(prueba, estado);
    var vuelo = prueba.Vuelos[0];
    var otraSalida = prueba.Vuelos[2];
    var pistaSalida = vuelo.Pista!.Value;
    var terminalSalida = Aeropuerto.TerminalesDe(pistaSalida)[0];
    int terminados = 0;
    prueba.DespegueCompletado += _ => terminados++;
    var resultado = prueba.Autorizar(vuelo);
    Verificar(resultado?.EnProceso == true && resultado.Aceptada && resultado.CambioPuntos == 0,
        "Autorizar con espera bajo " + estado);
    Verificar(vuelo.Estado == EstadoVuelo.Despegando && prueba.Ocupante(pistaSalida) == vuelo && prueba.Puntos == 100 &&
        prueba.SegundosParaReemplazo(0) == null && prueba.SegundosRestantesDespegue(pistaSalida) == segundos,
        "La pista sigue ocupada y no hay puntos ni reposición anticipados");
    Verificar(prueba.Autorizar(vuelo) is null, "No autorizar dos veces un vuelo en curso");
    Verificar(prueba.Autorizar(prueba.Vuelos[1], pistaSalida, terminalSalida)?.CambioPuntos == -10, "No aterrizar en pista en uso");
    tiempo.Avanzar(500);
    Verificar(prueba.Autorizar(otraSalida)?.EnProceso == true, "Otro despegue simultáneo");
    tiempo.Avanzar(segundos * 1000 - 501);
    prueba.ActualizarClima();
    Verificar(!vuelo.Completado && prueba.ProgresoDespegue(pistaSalida) > 0, "No liberar antes del plazo");
    tiempo.Avanzar(1);
    prueba.ActualizarClima();
    Verificar(vuelo.Completado && !otraSalida.Completado && prueba.Puntos == 110 && terminados == 1 &&
        prueba.Ocupante(pistaSalida) == null && prueba.SegundosParaReemplazo(0) == 10, "Completar y puntuar una sola pista");
    tiempo.Avanzar(500);
    prueba.ActualizarClima();
    prueba.ActualizarClima();
    Verificar(otraSalida.Completado && prueba.Puntos == 130 && terminados == 2, "Completar el segundo despegue sin duplicar puntos");
    Verificar(prueba.Autorizar(prueba.Vuelos[1], pistaSalida, terminalSalida)?.Aceptada == true,
        "Aterrizaje permitido después de liberar la pista");
    tiempo.Avanzar(9500);
    prueba.ActualizarTrafico();
    Verificar(prueba.Vuelos[0] != vuelo && prueba.Vuelos[2] == otraSalida, "Reposición desde el fin real del despegue");
}

// Busco una semilla con lluvia seguida de tormenta para probar la pausa de un despegue real.
int semillaTormenta = -1;
int comienzoLluvia = 0, duracionLluvia = 0;
for (int semilla = 0; semilla < 100 && semillaTormenta < 0; semilla++)
{
    var relojBusqueda = new RelojDePrueba();
    var busqueda = new Simulacion(new Random(semilla), relojBusqueda);
    int transcurrido = 0;
    while (transcurrido < 130000)
    {
        var anterior = busqueda.Clima;
        int plazo = (int)busqueda.TiempoParaCambioClima.TotalMilliseconds;
        if (transcurrido + plazo >= 160000) break;
        relojBusqueda.Avanzar(plazo);
        busqueda.ActualizarClima();
        if (anterior == CondicionClimatica.Lluvioso && busqueda.Clima == CondicionClimatica.Tormenta)
        {
            semillaTormenta = semilla;
            comienzoLluvia = transcurrido;
            duracionLluvia = plazo;
            break;
        }
        transcurrido += plazo;
    }
}
Verificar(semillaTormenta >= 0, "Encontrar una transición automática lluvia-tormenta");
var relojPausa = new RelojDePrueba();
var pausa = new Simulacion(new Random(semillaTormenta), relojPausa);
relojPausa.Avanzar(comienzoLluvia + duracionLluvia - 2000);
pausa.ActualizarClima();
Verificar(pausa.Clima == CondicionClimatica.Lluvioso, "Lluvia antes de la tormenta");
var pendiente = pausa.Vuelos[0];
var pistaPendiente = pendiente.Pista!.Value;
Verificar(pausa.Autorizar(pendiente)?.EnProceso == true, "Iniciar salida antes de la tormenta");
relojPausa.Avanzar(2000);
pausa.ActualizarClima();
Verificar(pausa.Clima == CondicionClimatica.Tormenta && pausa.DespeguesSuspendidos && pausa.AterrizajesSuspendidos &&
    pausa.SegundosRestantesDespegue(pistaPendiente) == 2, "Pausa automática a mitad del despegue");
Verificar(pausa.Autorizar(pausa.Vuelos[2])?.CambioPuntos == -20, "Bloqueo de salida nueva en tormenta");
Verificar(pausa.Autorizar(pausa.Vuelos[1], pistaPendiente, Aeropuerto.TerminalesDe(pistaPendiente)[0])?.CambioPuntos == -20,
    "Bloqueo de aterrizaje en tormenta");
int duraTormenta = (int)pausa.TiempoParaCambioClima.TotalMilliseconds;
relojPausa.Avanzar(duraTormenta - 1);
pausa.ActualizarClima();
Verificar(pausa.SegundosRestantesDespegue(pistaPendiente) == 2 && !pendiente.Completado, "Cuenta congelada toda la tormenta");
relojPausa.Avanzar(1);
pausa.ActualizarClima();
Verificar(!pausa.DespeguesSuspendidos && pausa.SegundosRestantesDespegue(pistaPendiente) == 2, "Reanudar tiempo pendiente");
relojPausa.Avanzar(2000);
pausa.ActualizarClima();
Verificar(pendiente.Completado && pausa.Puntos == 80 && pausa.Completados == 1, "Completar después de la pausa sin doble puntuación");

// Compruebo el cierre con una salida todavía en curso.
var relojCierre = new RelojDePrueba();
var cierre = new Simulacion(new Random(8), relojCierre);
relojCierre.Avanzar(178000);
cierre.ActualizarClima();
ForzarClima(cierre, CondicionClimatica.Lluvioso);
Verificar(cierre.Autorizar(cierre.Vuelos[0])?.EnProceso == true, "Salida iniciada cerca del cierre");
relojCierre.Avanzar(3000);
cierre.ActualizarClima();
Verificar(cierre.Finalizada && cierre.Completados == 0 && cierre.Puntos == 100, "No puntuar una salida que terminaría después del cierre");

// Un temporizador retrasado debe llegar al mismo clima y al mismo plazo.
var relojPuntual = new RelojDePrueba();
var relojRetrasado = new RelojDePrueba();
var puntual = new Simulacion(new Random(41), relojPuntual);
var retrasado = new Simulacion(new Random(41), relojRetrasado);
for (int i = 0; i < 140; i++)
{
    relojPuntual.Avanzar(1000);
    puntual.ActualizarClima();
}
relojRetrasado.Avanzar(140000);
retrasado.ActualizarClima();
Verificar(puntual.Clima == retrasado.Clima && puntual.TiempoParaCambioClima == retrasado.TiempoParaCambioClima,
    "Recuperación de plazos tras retraso de interfaz");
Console.WriteLine("Pruebas de clima aleatorio y despegues completadas.");

// Uso el cambio del núcleo solo para preparar escenarios de prueba, nunca desde la interfaz.
static void ForzarClima(Simulacion ronda, CondicionClimatica objetivo)
{
    for (int i = 0; i < 100 && ronda.Clima != objetivo; i++) ronda.CambiarClima();
    Verificar(ronda.Clima == objetivo, "Preparar clima " + objetivo);
}

sealed class RelojDePrueba : TimeProvider
{
    private long milisegundos;
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => milisegundos;
    public void Avanzar(int intervaloMs) => milisegundos += intervaloMs;
}
