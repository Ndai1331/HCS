using System;

namespace HC.Workflows;

/// <summary>
/// Business-day arithmetic (Monday–Friday). Weekends (Saturday/Sunday) are skipped.
/// Public holidays are not excluded yet.
/// </summary>
public static class BusinessDayCalculator
{
    public static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    /// <summary>
    /// Adds the given number of business days to <paramref name="start"/>, preserving time-of-day.
    /// </summary>
    public static DateTime AddBusinessDays(DateTime start, int businessDays)
    {
        if (businessDays <= 0)
        {
            return start;
        }

        var result = start;
        var remaining = businessDays;
        while (remaining > 0)
        {
            result = result.AddDays(1);
            if (!IsWeekend(result))
            {
                remaining--;
            }
        }

        return result;
    }

    public static DateTime AddBusinessDaysFrom(DateTime from, int businessDays)
    {
        return AddBusinessDays(from, businessDays);
    }

    /// <summary>
    /// Returns the instant when a workflow in OVERDUE status will be auto-cancelled (1 business day after overdue).
    /// </summary>
    public static DateTime GetOverdueGraceCancelAt(DateTime overdueAt)
    {
        return AddBusinessDays(overdueAt, 1);
    }

    /// <summary>
    /// Counts whole business days between two instants (exclusive of weekends in the span).
    /// </summary>
    public static int GetBusinessDaysBetween(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            return 0;
        }

        var count = 0;
        var cursor = start.Date;
        var endDate = end.Date;
        while (cursor < endDate)
        {
            cursor = cursor.AddDays(1);
            if (!IsWeekend(cursor))
            {
                count++;
            }
        }

        return count;
    }
}
