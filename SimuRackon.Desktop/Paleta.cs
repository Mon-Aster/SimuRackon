namespace SimuRackon.Desktop;

internal static class Paleta
{
    // Yo concentro aquí todos los colores para cambiar el estilo sin ir control por control.
    public static readonly Color Fondo = Color.FromArgb(10, 10, 10);
    public static readonly Color Panel = Color.FromArgb(24, 24, 24);
    public static readonly Color Borde = Color.FromArgb(78, 78, 78);
    public static readonly Color Control = Color.FromArgb(36, 36, 36);
    public static readonly Color Texto = Color.FromArgb(246, 245, 255);
    public static readonly Color Secundario = Color.FromArgb(188, 196, 218);

    // Yo uso esta paleta como base del clima soleado y de los estados agradables.
    public static readonly Color Soleado = Color.FromArgb(50, 125, 114);
    public static readonly Color SoleadoClaro = Color.FromArgb(92, 171, 158);
    public static readonly Color SoleadoOscuro = Color.FromArgb(36, 92, 83);

    // Mantengo una paleta pastel para resaltar acciones y decisiones sobre el fondo oscuro.
    public static readonly Color Acento = Color.FromArgb(190, 232, 210);     // Verde menta
    public static readonly Color Lavanda = Color.FromArgb(207, 196, 247);    // Lila suave
    public static readonly Color Alerta = Color.FromArgb(247, 203, 181);     // Durazno
    public static readonly Color Cielo = Color.FromArgb(175, 223, 255);      // Azul claro
    public static readonly Color Rosa = Color.FromArgb(241, 197, 220);       // Rosa pastel
    public static readonly Color Seleccion = Color.FromArgb(61, 74, 71);
}
