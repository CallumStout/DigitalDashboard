namespace DigitalDashboard.Models;

public class SmartDeviceModel
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool? IsOn { get; set; }

    public bool Available { get; set; }

    public SmartDeviceCapabilities? Capabilities { get; set; }
}
