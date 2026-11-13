using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace DigitalDashboard.Views;

public partial class ClockView : UserControl
{
    public ClockView()
    {
        InitializeComponent();
        EnableMouseDragScrolling(WeatherScroller);
        EnableMouseDragScrolling(EventsScroller);
    }

    // Native touch uses Avalonia's inertial scrolling. Some touch drivers instead
    // emit mouse events; support dragging these read-only lists in that case too.
    private static void EnableMouseDragScrolling(ScrollViewer viewer)
    {
        IPointer? draggingPointer = null;
        double previousY = 0;

        viewer.PointerPressed += (_, e) =>
        {
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
