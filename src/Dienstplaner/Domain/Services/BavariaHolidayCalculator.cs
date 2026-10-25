namespace Dienstplaner.Domain.Services;

public static class BavariaHolidayCalculator
{
    public static Dictionary<DateTime, string> GetHolidaysForYear(int year)
    {
        var holidays = new Dictionary<DateTime, string>();

        // Feste Feiertage
        holidays[new DateTime(year, 1, 1)] = "Neujahr";
        holidays[new DateTime(year, 1, 6)] = "Heilige Drei Könige";
        holidays[new DateTime(year, 5, 1)] = "Tag der Arbeit";
        holidays[new DateTime(year, 8, 15)] = "Mariä Himmelfahrt";
        holidays[new DateTime(year, 10, 3)] = "Tag der Deutschen Einheit";
        holidays[new DateTime(year, 11, 1)] = "Allerheiligen";
        holidays[new DateTime(year, 12, 25)] = "1. Weihnachtstag";
        holidays[new DateTime(year, 12, 26)] = "2. Weihnachtstag";

        // Bewegliche Feiertage via Ostersonntag (Meeus/Jones/Butcher Algorithmus)
        var easter = CalculateEasterSunday(year);
        holidays[easter.AddDays(-2)] = "Karfreitag";
        holidays[easter.AddDays(1)] = "Ostermontag";
        holidays[easter.AddDays(39)] = "Christi Himmelfahrt";
        holidays[easter.AddDays(50)] = "Pfingstmontag";
        holidays[easter.AddDays(60)] = "Fronleichnam";

        return holidays;
    }

    public static Dictionary<int, string> GetHolidaysForMonth(int year, int month)
    {
        var all = GetHolidaysForYear(year);
        var result = new Dictionary<int, string>();
        foreach (var (date, name) in all)
        {
            if (date.Month == month)
            {
                result[date.Day] = name;
            }
        }
        return result;
    }

    private static DateTime CalculateEasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateTime(year, month, day);
    }
}
