using Dienstplanner.Domain.Models;
using Dienstplanner.Domain.Services;
using System;
using Xunit;

namespace Dienstplanner.Tests;

public class ShiftCodeInterpreterTests
{
    private readonly ShiftCodeInterpreter _sut = new();

    [Fact]
    public void Interpret_EmptyCode_ReturnsFree()
    {
        var result = _sut.Interpret("", new DateTime(2026, 4, 1), false);
        Assert.Equal(ShiftType.Frei, result.Type);
        Assert.Null(result.StartTime);
        Assert.Null(result.EndTime);
    }

    [Theory]
    [InlineData("T", 16, 0, 21, 0)]
    [InlineData("T1", 7, 30, 14, 0)]
    [InlineData("T2", 14, 0, 21, 0)]
    public void Interpret_BasicShifts_ReturnsCorrectTimesAndType(string code, int startH, int startM, int endH, int endM)
    {
        var date = new DateTime(2026, 4, 1); // Wednesday
        var result = _sut.Interpret(code, date, false);

        Assert.Equal(ShiftType.Arbeit, result.Type);
        Assert.Equal(new TimeSpan(startH, startM, 0), result.StartTime);
        Assert.Equal(new TimeSpan(endH, endM, 0), result.EndTime);
    }

    [Fact]
    public void Interpret_NightShift_NormalDay_Ends830()
    {
        var date = new DateTime(2026, 4, 1); // Wednesday
        var result = _sut.Interpret("N", date, false);

        Assert.Equal(new TimeSpan(21, 0, 0), result.StartTime);
        Assert.Equal(new TimeSpan(8, 30, 0), result.EndTime);
    }

    [Fact]
    public void Interpret_NightShift_Friday_Ends830()
    {
        var date = new DateTime(2026, 4, 3); // Friday
        var result = _sut.Interpret("N", date, false);

        Assert.Equal(new TimeSpan(21, 0, 0), result.StartTime);
        Assert.Equal(new TimeSpan(8, 30, 0), result.EndTime);
    }

    [Fact]
    public void Interpret_NightShift_Sunday_Ends815()
    {
        var date = new DateTime(2026, 4, 5); // Sunday
        var result = _sut.Interpret("N", date, false, false); // Not a holiday tomorrow

        Assert.Equal(new TimeSpan(21, 0, 0), result.StartTime);
        Assert.Equal(new TimeSpan(8, 15, 0), result.EndTime);
    }

    [Fact]
    public void Interpret_NightShift_Sunday_BeforeHoliday_Ends830()
    {
        var date = new DateTime(2026, 4, 5); // Sunday
        var result = _sut.Interpret("N", date, false, true); // Tomorrow is a holiday!

        Assert.Equal(new TimeSpan(21, 0, 0), result.StartTime);
        Assert.Equal(new TimeSpan(8, 30, 0), result.EndTime);
    }

    [Fact]
    public void Interpret_Vacation_ReturnsVacationType()
    {
        var result = _sut.Interpret("U", new DateTime(2026, 4, 1), false);
        Assert.Equal(ShiftType.Urlaub, result.Type);
    }
    
    [Fact]
    public void Interpret_Sick_ReturnsSickType()
    {
        var result = _sut.Interpret("K", new DateTime(2026, 4, 1), false);
        Assert.Equal(ShiftType.Krank, result.Type);
    }
    
    [Fact]
    public void Interpret_SpecialX_ReturnsFreiAntragType()
    {
        var result = _sut.Interpret("X", new DateTime(2026, 4, 1), false);
        Assert.Equal(ShiftType.FreiAntrag, result.Type);
    }

    [Theory]
    [InlineData("H")]
    [InlineData("UNKNOWN_CODE")]
    public void Interpret_UnknownOrSpecial_ReturnsUnknownType(string code)
    {
        var result = _sut.Interpret(code, new DateTime(2026, 4, 1), false);
        Assert.Equal(ShiftType.Unbekannt, result.Type);
    }
}
