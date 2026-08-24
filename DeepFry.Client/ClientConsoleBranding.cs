namespace DeepFry.Client;

internal static class ClientConsoleBranding
{
    public static void Print()
    {
        if (!Environment.UserInteractive)
            return;

        try
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(@" ██████╗ ███████╗███████╗██████╗     ███████╗██████╗ ██╗   ██╗");
            Console.WriteLine(@" ██╔══██╗██╔════╝██╔════╝██╔══██╗    ██╔════╝██╔══██╗╚██╗ ██╔╝");
            Console.WriteLine(@" ██║  ██║█████╗  █████╗  ██████╔╝    █████╗  ██████╔╝ ╚████╔╝ ");
            Console.WriteLine(@" ██║  ██║██╔══╝  ██╔══╝  ██╔═══╝     ██╔══╝  ██╔══██╗  ╚██╔╝  ");
            Console.WriteLine(@" ██████╔╝███████╗███████╗██║         ██║     ██║  ██║   ██║   ");
            Console.WriteLine(@" ╚═════╝ ╚══════╝╚══════╝╚═╝         ╚═╝     ╚═╝  ╚═╝   ╚═╝   ");
            Console.WriteLine();
            Console.WriteLine(" [ Project ] Deep Fry UWF Monitoring & Control Client");
            Console.WriteLine(" [ Author  ] Gusti Padaka (22.11.5020)");
            Console.WriteLine(" [ For     ] UPT Laboratorium Amikom Yogyakarta");
            Console.WriteLine($" [ Version ] Deep Fry v{ClientVersion.Display}");
            Console.WriteLine(" ----------------------------------------------------------------");
        }
        catch (IOException)
        {
        }
        finally
        {
            try
            {
                Console.ResetColor();
            }
            catch (IOException)
            {
            }
        }
    }
}
