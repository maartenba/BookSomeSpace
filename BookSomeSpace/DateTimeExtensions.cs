using System;
using System.Collections.Generic;

namespace BookSomeSpace;

public static class DateTimeExtensions
{
    public static DateTime WithYear(this DateTime current, int year) => 
        new(year, current.Month, current.Day, current.Hour, current.Minute, current.Second, current.Kind);
        
    public static DateTime WithMonth(this DateTime current, int month) => 
        new(current.Year, month, current.Day, current.Hour, current.Minute, current.Second, current.Kind);
        
    public static DateTime WithDay(this DateTime current, int day) => 
        new(current.Year, current.Month, day, current.Hour, current.Minute, current.Second, current.Kind);
        
    public static DateTime WithHour(this DateTime current, int hour) => 
        new(current.Year, current.Month, current.Day, hour, current.Minute, current.Second, current.Kind);
        
    public static DateTime WithMinute(this DateTime current, int minute) => 
        new(current.Year, current.Month, current.Day, current.Hour, minute, current.Second, current.Kind);
        
    public static DateTime WithSecond(this DateTime current, int second) => 
        new(current.Year, current.Month, current.Day, current.Hour, current.Minute, second, current.Kind);
            
    public static IEnumerable<DateTime> EachDayUntil(this DateTime current, DateTime until)
    {
        for (var day = current.Date; day.Date <= until.Date; day = day.AddDays(1))
        {
            yield return day;
        }
    }
}