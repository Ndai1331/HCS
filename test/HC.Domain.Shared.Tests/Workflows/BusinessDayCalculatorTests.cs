using System;
using HC.Workflows;
using Xunit;

namespace HC.Domain.Shared.Tests.Workflows;

public class BusinessDayCalculatorTests
{
    [Fact]
    public void AddBusinessDays_FridayPlusOne_SkipsWeekend_LandsMonday()
    {
        var friday = new DateTime(2026, 5, 22, 14, 30, 0); // Friday
        var result = BusinessDayCalculator.AddBusinessDays(friday, 1);
        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 5, 25), result.Date);
        Assert.Equal(14, result.Hour);
        Assert.Equal(30, result.Minute);
    }

    [Fact]
    public void AddBusinessDays_ThursdayPlusThree_SkipsWeekend()
    {
        var thursday = new DateTime(2026, 5, 21, 9, 0, 0);
        var result = BusinessDayCalculator.AddBusinessDays(thursday, 3);
        Assert.Equal(new DateTime(2026, 5, 26), result.Date); // Tue
    }

    [Fact]
    public void IsWeekend_SaturdayAndSunday_AreWeekend()
    {
        Assert.True(BusinessDayCalculator.IsWeekend(new DateTime(2026, 5, 23)));
        Assert.True(BusinessDayCalculator.IsWeekend(new DateTime(2026, 5, 24)));
        Assert.False(BusinessDayCalculator.IsWeekend(new DateTime(2026, 5, 25)));
    }

    [Fact]
    public void GetOverdueGraceCancelAt_FridayOverdue_CancelsMonday()
    {
        var fridayOverdue = new DateTime(2026, 5, 22, 20, 0, 0);
        var cancelAt = BusinessDayCalculator.GetOverdueGraceCancelAt(fridayOverdue);
        Assert.Equal(DayOfWeek.Monday, cancelAt.DayOfWeek);
        Assert.Equal(new DateTime(2026, 5, 25, 20, 0, 0), cancelAt);
    }
}
