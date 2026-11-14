namespace DigitalDashboard.Models;

public class SmartPlugModel
{
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonRequired]
    public bool IsOn { get; set; }
}
