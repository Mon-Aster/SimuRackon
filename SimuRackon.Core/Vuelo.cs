namespace SimuRackon.Core;

public enum TipoOperacion { Llegada, Salida }
public enum EstadoVuelo { EsperandoPista, EsperandoDespegue, Despegando, Aterrizado, Despegado }

public sealed class Vuelo
{
    #region Datos del vuelo

    public string Identificador { get; }
    public TipoOperacion Operacion { get; }
    public EstadoVuelo Estado { get; private set; }
    public Pista? Pista { get; private set; }
    public Terminal? Terminal { get; private set; }
    public IReadOnlyDictionary<Pista, int> Distancias { get; }
    public bool Completado => Estado == EstadoVuelo.Aterrizado || Estado == EstadoVuelo.Despegado;

    // Traduzco el estado del vuelo a un texto que puedo mostrar en la pantalla.
    public string EstadoVisible
    {
        get
        {
            switch (Estado)
            {
                case EstadoVuelo.EsperandoPista: return "Esperando pista";
                case EstadoVuelo.EsperandoDespegue: return "Esperando autorización para despegar";
                case EstadoVuelo.Despegando: return "Despegue en curso";
                case EstadoVuelo.Aterrizado: return "Aterrizó en la terminal " + Terminal;
                case EstadoVuelo.Despegado: return "Despegó de la pista " + Pista;
                default: throw new InvalidOperationException("Estado del vuelo desconocido.");
            }
        }
    }

    #endregion

    #region Constructor

    internal Vuelo(string identificador, TipoOperacion operacion,
        IReadOnlyDictionary<Pista, int> distancias, Pista? pista = null)
    {
        Identificador = identificador;
        Operacion = operacion;
        Distancias = distancias;
        Pista = pista;
        Estado = operacion == TipoOperacion.Llegada ? EstadoVuelo.EsperandoPista : EstadoVuelo.EsperandoDespegue;
    }

    #endregion

    #region Cambios de estado

    // Cuando autorizo una llegada, guardo la pista y la terminal que usó.
    internal void Aterrizar(Pista pista, Terminal terminal)
    {
        Pista = pista;
        Terminal = terminal;
        Estado = EstadoVuelo.Aterrizado;
    }

    // Marco el vuelo en proceso sin liberar la pista hasta que termine la espera.
    internal void IniciarDespegue()
    {
        Estado = EstadoVuelo.Despegando;
    }

    internal void Despegar()
    {
        Estado = EstadoVuelo.Despegado;
    }

    #endregion
}
