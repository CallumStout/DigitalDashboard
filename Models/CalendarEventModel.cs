using System;

namespace DigitalDashboard.Models;

public class CalendarEventModel
{
    public string Title { get; set; } = string.Empty;

    public DateTime Start {  get; set; }

    public DateTime? End { get; set; }

    public bool IsAllDay { get; set; }
}
