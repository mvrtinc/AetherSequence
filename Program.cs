namespace AetherSequence;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
        {
            DevTools.SelfTest();
            return;
        }
        if (args.Length > 1 && args[0] == "--shots")
        {
            DevTools.Shots(args[1]);
            return;
        }
        if (args.Length > 0 && args[0] == "--audiotest")
        {
            AudioDiagnostics.Run();
            return;
        }
        if (args.Length > 0 && args[0] == "--perf")
        {
            PerfProbe.Run();
            return;
        }
        if (args.Length > 0 && args[0] == "--frame")
        {
            FrameProbe.Run();
            return;
        }
        if (args.Length > 0 && args[0] == "--duelshot")
        {
            DevTools.DuelShots(args.Length > 1 ? args[1] : ".");
            return;
        }
        if (args.Length > 0 && args[0] == "--themeshot")
        {
            DevTools.ThemeShots(args.Length > 1 ? args[1] : ".");
            return;
        }
        if (args.Length > 0 && args[0] == "--castshot")
        {
            DevTools.CastShots(args.Length > 1 ? args[1] : ".");
            return;
        }
        if (args.Length > 0 && args[0] == "--duelhead")
        {
            DevTools.DuelHeadless(args);
            return;
        }
        if (args.Length > 0 && args[0] == "--nettest")
        {
            DevTools.NetTest();
            return;
        }
        if (args.Length > 0 && args[0] == "--netdiag")
        {
            Net.NetDiag.Run(args.Length > 1 && int.TryParse(args[1], out int s) ? s : 6);
            return;
        }
        if (args.Length > 1 && args[0] == "--hudshot")
        {
            DevTools.HudShot(args[1]);
            return;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (Array.IndexOf(args, "--autoplay") >= 0)
        {
            AutoPilot.GodMode = Array.IndexOf(args, "--god") >= 0;
            AutoPilot.Reset(20260801u);            Game.Autopilot = true;
        }

        // Тренировка с ботом: сразу в дуэль, чтобы не искать пункт меню.
        if (Array.IndexOf(args, "--duelbot") >= 0)
        {
            Game.AutoStartDuelBot = true;
            AutoPilot.GodMode = Array.IndexOf(args, "--god") >= 0;
        }

        Application.Run(new Forms.GameForm());
    }
}
