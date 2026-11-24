namespace SimuRackon.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Inicio Windows Forms y abro la única ventana del simulador.
        ApplicationConfiguration.Initialize();
        Application.Run(new TorreForm());
    }
}
