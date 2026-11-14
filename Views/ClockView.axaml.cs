using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DigitalDashboard.Views;

public partial class ClockView : UserControl
{
    public ClockView()
    {
        InitializeComponent();
        EnableMouseDragScrolling(WeatherScroller);
        EnableMouseDragScrolling(EventsScroller);
        EnableMouseDragScrolling(PlugsScroller);
    }

    private void Navigate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section }) return;

        OverviewPanel.IsVisible = section == "Overview";
        PlugsPanel.IsVisible = section == "Plugs";
        FeaturePanel.IsVisible = !OverviewPanel.IsVisible && !PlugsPanel.IsVisible;
        FeatureTitle.Text = section;
        FeatureDescription.Text = section switch
        {
            "Climate" => "Your thermostat and climate controls will appear here once connected.",
            "Security" => "Your door locks and security devices will appear here once connected.",
            _ => string.Empty
        };

        foreach (var control in this.GetVisualDescendants())
        {
            if (control is Button button && button.Classes.Contains("nav"))
                button.Classes.Set("active", Equals(button.Tag, section));
        }
    }

    // Native touch uses Avalonia's inertial scrolling. Some touch drivers instead
    // emit mouse events; support dragging these read-only lists in that case too.
    private static void EnableMouseDragScrolling(ScrollViewer viewer)
    {
        IPointer? draggingPointer = null;
        double previousY = 0;

        viewer.PointerPressed += (_, e) =>
        {
            // Keep power and refresh buttons clickable inside the plugs list.
            if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) != null)
                return;

            if (e.Pointer.Type != PointerType.Mouse
                || !e.GetCurrentPoint(viewer).Properties.IsLeftButtonPressed
                || viewer.Extent.Height <= viewer.Viewport.Height)
            {
                return;
            }

            previousY = e.GetPosition(viewer).Y;
            draggingPointer = e.Pointer;
            e.Pointer.Capture(viewer);
            e.Handled = true;
        };

        viewer.PointerMoved += (_, e) =>
        {
            if (draggingPointer != e.Pointer)
            {
                return;
            }

            var currentY = e.GetPosition(viewer).Y;
            viewer.Offset = new Vector(viewer.Offset.X, viewer.Offset.Y + previousY - currentY);
            previousY = currentY;
            e.Handled = true;
        };

        viewer.PointerReleased += (_, e) =>
        {
            if (draggingPointer != e.Pointer)
            {
                return;
            }

            draggingPointer = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        };

        viewer.PointerCaptureLost += (_, _) => draggingPointer = null;
        viewer.DetachedFromVisualTree += (_, _) =>
        {
            draggingPointer?.Capture(null);
            draggingPointer = null;
        };
    }
}
