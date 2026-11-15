using System.Runtime.InteropServices;

namespace Bronxamp.Sistem;

/// <summary>Keeps the computer from going to sleep while a day is on air. The screen may still turn off.</summary>
public sealed class BudanRacunar
{
    private const uint Stalno = 0x80000000;
    private const uint Sistem = 0x00000001;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint stanje);

    public void Ukljuci() => SetThreadExecutionState(Stalno | Sistem);

    public void Iskljuci() => SetThreadExecutionState(Stalno);
}
