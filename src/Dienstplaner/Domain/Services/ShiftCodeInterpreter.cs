namespace Dienstplaner.Domain.Services;

using Dienstplaner.Domain.Models;

public class ShiftCodeInterpreter
{
    public DayAssignment Interpret(string code, DateTime date, bool isHoliday, bool isTomorrowHoliday = false)
    {
        code = code?.Trim().ToUpperInvariant() ?? "";

        if (string.IsNullOrEmpty(code))
        {
            return new DayAssignment(date, string.Empty, null, null, ShiftType.Frei, isHoliday);
        }

        var dayOfWeek = date.DayOfWeek;
        var start = default(TimeSpan?);
        var end = default(TimeSpan?);
        var type = ShiftType.Arbeit;

        switch (code)
        {
            case "T":
                start = new TimeSpan(16, 0, 0);
                end = new TimeSpan(21, 0, 0);
                break;
            case "N":
                start = new TimeSpan(21, 0, 0);
                if (dayOfWeek == DayOfWeek.Sunday && !isTomorrowHoliday)
                {
                    end = new TimeSpan(8, 15, 0);
                }
                else
                {
                    end = new TimeSpan(8, 30, 0);
                }
                break;
            case "T1":
                start = new TimeSpan(7, 30, 0);
                end = new TimeSpan(14, 0, 0);
                break;
            case "T2":
                start = new TimeSpan(14, 0, 0);
                end = new TimeSpan(21, 0, 0);
                break;
            case "K":
                type = ShiftType.Krank;
                return new DayAssignment(date, code, null, null, type, isHoliday);
            case "U":
                type = ShiftType.Urlaub;
                return new DayAssignment(date, code, null, null, type, isHoliday);
            case "X":
                type = ShiftType.FreiAntrag;
                return new DayAssignment(date, code, null, null, type, isHoliday);
            default:
                type = ShiftType.Unbekannt;
                return new DayAssignment(date, code, null, null, type, isHoliday);
        }

        return new DayAssignment(date, code, start, end, type, isHoliday);
    }
}
