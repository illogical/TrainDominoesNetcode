using System;
internal static class Program
{
    private static int Main() {
        try { CommandBoundaryChecks.Run(); Console.WriteLine("Gameplay integration checks passed."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
