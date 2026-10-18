using System;
internal static class Program
{
    private static int Main() {
        try { CommandBoundaryChecks.Run(); RelayChecks.Run(); WireContractChecks.Run(); AdversarialChecks.Run(); PrivacyAnnouncementChecks.Run(); DeliveryFaultChecks.Run(); ReconnectChecks.Run(); Console.WriteLine("Gameplay integration checks passed."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
