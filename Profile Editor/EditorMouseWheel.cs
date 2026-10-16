//  Copyright 2020 Helios Contributors
//
//  Helios is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  Helios is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace GadrocsWorkshop.Helios.ProfileEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;

    /// <summary>
    /// Mouse wheel behavior for the monitor and panel editor tabs:
    ///
    /// - Ctrl + wheel zooms in and out, keeping the point under the mouse pointer in place
    /// - side (tilt) wheel scrolls left and right
    /// - Shift + wheel also scrolls left and right, for mice that send their side wheel that way
    /// - plain wheel keeps scrolling up and down as before
    /// </summary>
    internal class EditorMouseWheel
    {
        /// <summary>
        /// Windows message for the horizontal (tilt / side) mouse wheel, which WPF on .NET Framework does not
        /// turn into an event
        /// </summary>
        private const int WM_MOUSEHWHEEL = 0x020E;

        /// <summary>
        /// one wheel notch as reported by Windows
        /// </summary>
        private const double WHEEL_DELTA = 120d;

        /// <summary>
        /// pixels scrolled per wheel notch, matching WPF's own vertical scrolling (3 lines of 16 pixels)
        /// </summary>
        private const double PIXELS_PER_NOTCH = 48d;

        private const double ZOOM_STEP = 0.25d;
        private const double ZOOM_MIN = -4d;
        private const double ZOOM_MAX = 4d;

        /// <summary>
        /// all live editor tabs, so the window message hook can find the one under the mouse
        /// </summary>
        private static readonly List<WeakReference<EditorMouseWheel>> Instances = new List<WeakReference<EditorMouseWheel>>();

        /// <summary>
        /// windows we have already hooked; a tab dragged out into a floating window gets a new one
        /// </summary>
        private static readonly HashSet<HwndSource> HookedSources = new HashSet<HwndSource>();

        private readonly FrameworkElement _document;
        private readonly ScrollViewer _scroller;
        private readonly HeliosVisualContainerEditor _editor;

        private EditorMouseWheel(FrameworkElement document, ScrollViewer scroller, HeliosVisualContainerEditor editor)
        {
            _document = document;
            _scroller = scroller;
            _editor = editor;
        }

        /// <summary>
        /// add zoom and horizontal scroll handling to an editor tab
        /// </summary>
        /// <param name="document">the tab content</param>
        /// <param name="scroller">the scroll viewer around the editor</param>
        /// <param name="editor">the editor that is zoomed</param>
        public static void Attach(FrameworkElement document, ScrollViewer scroller, HeliosVisualContainerEditor editor)
        {
            EditorMouseWheel handler = new EditorMouseWheel(document, scroller, editor);
            Instances.Add(new WeakReference<EditorMouseWheel>(handler));

            document.PreviewMouseWheel += handler.Document_PreviewMouseWheel;

            // the window is only known once the tab is shown, and changes if the tab is floated or docked again
            document.Loaded += (sender, e) => HookWindow(document);
        }

        private void Document_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Control:
                    Zoom(e.Delta);
                    e.Handled = true;
                    break;
                case ModifierKeys.Shift:
                    // tilting right scrolls right, but the vertical wheel rolled "down" (negative) should too
                    ScrollHorizontally(-e.Delta);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// change zoom level by one step per wheel notch, keeping the point under the mouse pointer still
        /// </summary>
        private void Zoom(int wheelDelta)
        {
            double steps = Math.Round(wheelDelta / WHEEL_DELTA);
            if (steps == 0d)
            {
                // high resolution wheels send small deltas; still move one step in that direction
                steps = Math.Sign(wheelDelta);
            }

            double oldLevel = _editor.ZoomLevel;
            double newLevel = Math.Max(ZOOM_MIN, Math.Min(ZOOM_MAX, oldLevel + steps * ZOOM_STEP));
            if (newLevel == oldLevel)
            {
                return;
            }

            // where the pointer is on the editor, in zoomed pixels, before the change
            Point pointer = Mouse.GetPosition(_editor);
            double oldFactor = _editor.ZoomFactor;
            double oldHorizontalOffset = _scroller.HorizontalOffset;
            double oldVerticalOffset = _scroller.VerticalOffset;

            // changing the level recalculates ZoomFactor immediately
            _editor.ZoomLevel = newLevel;
            double newFactor = _editor.ZoomFactor;
            if (oldFactor <= 0d || newFactor <= 0d)
            {
                return;
            }

            // let the scroll viewer learn the new size of the editor before scrolling within it
            _scroller.UpdateLayout();

            // the same spot on the profile is now (ratio) times further from the editor's corner,
            // so scroll by the difference to keep it under the pointer
            double ratio = newFactor / oldFactor;
            _scroller.ScrollToHorizontalOffset(oldHorizontalOffset + pointer.X * (ratio - 1d));
            _scroller.ScrollToVerticalOffset(oldVerticalOffset + pointer.Y * (ratio - 1d));
        }

        /// <summary>
        /// scroll sideways; positive delta scrolls to the right
        /// </summary>
        private void ScrollHorizontally(int wheelDelta)
        {
            double offset = _scroller.HorizontalOffset + wheelDelta / WHEEL_DELTA * PIXELS_PER_NOTCH;
            _scroller.ScrollToHorizontalOffset(Math.Max(0d, Math.Min(offset, _scroller.ScrollableWidth)));
        }

        private static void HookWindow(Visual visual)
        {
            if (!(PresentationSource.FromVisual(visual) is HwndSource source) || HookedSources.Contains(source))
            {
                return;
            }
            HookedSources.Add(source);
            source.AddHook(WndProc);
            source.Disposed += (sender, e) => HookedSources.Remove(source);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_MOUSEHWHEEL)
            {
                return IntPtr.Zero;
            }

            EditorMouseWheel target = FindTabUnderMouse();
            if (target == null)
            {
                return IntPtr.Zero;
            }

            // high word of wParam is the signed wheel delta; positive means tilted to the right
            int delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
            target.ScrollHorizontally(delta);

            // mark it handled so nothing else scrolls as well; Windows expects zero when the message is processed
            handled = true;
            return IntPtr.Zero;
        }

        private static EditorMouseWheel FindTabUnderMouse()
        {
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
            foreach (WeakReference<EditorMouseWheel> reference in Instances.ToList())
            {
                if (reference.TryGetTarget(out EditorMouseWheel handler)
                    && handler._document.IsVisible
                    && handler._scroller.IsMouseOver)
                {
                    return handler;
                }
            }
            return null;
        }
    }
}
