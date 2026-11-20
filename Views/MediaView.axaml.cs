using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace DigitalDashboard.Views;

public partial class MediaView : UserControl
{
    private IPointer? _pointer;
    private ScrollViewer? _scroller;
    private Point _pressPosition;
    private Vector _initialOffset;
    private bool _dragging;

    public MediaView()
    {
        InitializeComponent();
        // Observe input before the row button, including events it has handled.
        LibraryList.AddHandler(PointerPressedEvent, OnLibraryPressed, RoutingStrategies.Tunnel, true);
        LibraryList.AddHandler(PointerMovedEvent, OnLibraryMoved, RoutingStrategies.Tunnel, true);
        LibraryList.AddHandler(PointerReleasedEvent, OnLibraryReleased, RoutingStrategies.Tunnel, true);
        LibraryList.PointerCaptureLost += (_, e) =>
        {
            if (_pointer == e.Pointer && e.Pointer.Captured != LibraryList) ResetDrag();
        };
        DetachedFromVisualTree += (_, _) => ResetDrag();
    }

    private void OnLibraryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pointer != null || !e.GetCurrentPoint(LibraryList).Properties.IsLeftButtonPressed) return;
        // Leave the scrollbar's own thumb and track gestures alone.
        if (e.Source is Visual source && source.GetVisualAncestors()
            .Any(v => v is Avalonia.Controls.Primitives.ScrollBar)) return;
        _scroller = LibraryList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_scroller == null || _scroller.Extent.Height <= _scroller.Viewport.Height) return;
        _pointer = e.Pointer;
        _pressPosition = e.GetPosition(LibraryList);
        _initialOffset = _scroller.Offset;
        // A stationary tap still reaches the button normally.
    }

    private void OnLibraryMoved(object? sender, PointerEventArgs e)
    {
        if (_pointer != e.Pointer || _scroller == null) return;
        var distance = e.GetPosition(LibraryList).Y - _pressPosition.Y;
        if (!_dragging && Math.Abs(distance) < 8) return;
        if (!_dragging)
        {
            _dragging = true;
            // Cancels the button press, so releasing after a swipe cannot play a song.
            e.Pointer.Capture(LibraryList);
        }
        _scroller.Offset = new Vector(_initialOffset.X,
            Math.Clamp(_initialOffset.Y - distance, 0, Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height)));
        e.Handled = true;
    }

    private void OnLibraryReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pointer != e.Pointer) return;
        if (_dragging) e.Handled = true;
        ResetDrag();
    }

    private void ResetDrag()
    {
        var pointer = _pointer;
        _pointer = null;
        _scroller = null;
        _dragging = false;
        if (pointer?.Captured == LibraryList) pointer.Capture(null);
    }
}
