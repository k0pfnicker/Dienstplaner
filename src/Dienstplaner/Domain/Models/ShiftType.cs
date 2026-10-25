namespace Dienstplaner.Domain.Models;

public enum ShiftType
{
    None,
    S,
    N,
    T,
    FT,
    U,
    K,
    Ignored
}

public static class ShiftDefinition
{
    public static readonly TimeSpan S_Start = new TimeSpan(0, 0, 0);
    public static readonly TimeSpan S_End = new TimeSpan(8, 15, 0);
    public static readonly TimeSpan S_Duration = S_End - S_Start; // 08:15

    public static readonly TimeSpan N_Start = new TimeSpan(16, 0, 0);
    public static readonly TimeSpan N_End = new TimeSpan(0, 0, 0);
    public static readonly TimeSpan N_Duration = TimeSpan.FromHours(8); // 08:00

    public static readonly TimeSpan T_Start = new TimeSpan(7, 15, 0);
    public static readonly TimeSpan T_End = new TimeSpan(16, 0, 0);
    public static readonly TimeSpan T_Duration = T_End - T_Start; // 08:45

    public static ShiftType ParseCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return ShiftType.None;
        var clean = code.Trim().ToUpperInvariant();

        return clean switch
        {
            "S" => ShiftType.S,
            "N" => ShiftType.N,
            "T" => ShiftType.T,
            "FT" => ShiftType.FT,
            "U" => ShiftType.U,
            "K" => ShiftType.K,
            "STR" or "SO" or "TS" or "F" or "NS" or "NS2" => ShiftType.Ignored,
            _ => ShiftType.None
        };
    }

    public static TimeSpan GetDuration(ShiftType type) => type switch
    {
        ShiftType.S => S_Duration,
        ShiftType.N => N_Duration,
        ShiftType.T => T_Duration,
        _ => TimeSpan.Zero
    };

    public static string GetTimeRangeStr(ShiftType type) => type switch
    {
        ShiftType.S => "00:00 - 08:15",
        ShiftType.N => "16:00 - 00:00",
        ShiftType.T => "07:15 - 16:00",
        ShiftType.FT => "Freier Tag",
        ShiftType.U => "Urlaub",
        ShiftType.K => "Krank",
        _ => ""
    };

    public static string GetColor(ShiftType type) => type switch
    {
        ShiftType.S => "#1E88E5", // Blau
        ShiftType.N => "#8E24AA", // Violett
        ShiftType.T => "#FB8C00", // Orange
        ShiftType.FT => "#757575", // Grau
        ShiftType.U => "#43A047", // Grün
        ShiftType.K => "#E53935", // Rot
        _ => "Transparent"
    };

    public static string GetBgColor(ShiftType type) => type switch
    {
        ShiftType.S => "#E3F2FD",
        ShiftType.N => "#F3E5F5",
        ShiftType.T => "#FFF3E0",
        ShiftType.FT => "#F5F5F5",
        ShiftType.U => "#E8F5E9",
        ShiftType.K => "#FFEBEE",
        _ => "White"
    };
}
