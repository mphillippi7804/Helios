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
    using GadrocsWorkshop.Helios.Controls;
    using GadrocsWorkshop.Helios.ProfileEditor.UndoEvents;

    /// <summary>
    /// Group (Ctrl+G) and Ungroup (Ctrl+U) for the monitor and panel editors.
    ///
    /// Group:
    /// - no panel selected: all selected devices are placed in a new panel
    /// - a panel and other devices selected: the first selected panel grows to include the other devices,
    ///   which are moved into it
    /// - two or more panels selected: they merge into the first selected panel, which keeps its name
    ///
    /// The panel starts at the smallest Left and smallest Top of all grouped devices, and is sized to reach
    /// the largest Left + Width and largest Top + Height.  Each device keeps its position on screen, so its
    /// location inside the panel is its old location minus the panel's starting point.
    ///
    /// Ungroup: every device in each selected panel is moved out to the panel's level, at the panel's
    /// location plus its location inside the panel, and then the empty panel is deleted.
    ///
    /// Only generic panels ("Generic Bezel") are treated as panels here.  Specialized panels such as the
    /// Timer Panel and gauges built from several parts are treated as single devices, so they are never
    /// merged or taken apart.
    ///
    /// Each command is one undo step.
    /// </summary>
    internal static class GroupCommands
    {
        private const string NewPanelName = "Group";

        internal static bool IsGroupPanel(HeliosVisual visual) => visual != null && visual.GetType() == typeof(HeliosPanel);

        internal static bool CanGroup(HeliosVisualContainerEditor editor) =>
            editor?.VisualContainer != null && editor.SelectedItems != null && editor.SelectedItems.Count >= 2;

        internal static bool CanUngroup(HeliosVisualContainerEditor editor) =>
            editor?.VisualContainer != null && editor.SelectedItems != null && editor.SelectedItems.Any(IsGroupPanel);

        /// <summary>
        /// the area covered by a set of devices in their container's coordinates
        /// </summary>
        internal static Rect GetBounds(IEnumerable<HeliosVisual> visuals)
        {
            Rect bounds = Rect.Empty;
            foreach (HeliosVisual visual in visuals)
            {
                // DisplayRectangle is the same as (Left, Top, Width, Height) unless the device is rotated,
                // in which case it is the area it actually covers
                bounds.Union(visual.DisplayRectangle);
            }
            if (bounds.IsEmpty)
            {
                return bounds;
            }

            // Helios stores whole-pixel positions and sizes, so grow the area outward to whole pixels
            // (a rotated device can cover a half pixel) to match what every other edit produces
            double left = Math.Floor(bounds.Left);
            double top = Math.Floor(bounds.Top);
            double right = Math.Ceiling(bounds.Right);
            double bottom = Math.Ceiling(bounds.Bottom);
            return new Rect(left, top, right - left, bottom - top);
        }

        public static void Group(HeliosVisualContainerEditor editor)
        {
            if (!CanGroup(editor))
            {
                return;
            }

            HeliosVisual container = editor.VisualContainer;

            // selection order decides which panel is kept; container order decides layering
            List<HeliosVisual> selected = editor.SelectedItems.Where(v => v.Parent == container).ToList();
            if (selected.Count < 2)
            {
                return;
            }
            List<HeliosVisual> inLayerOrder = selected.OrderBy(v => container.Children.IndexOf(v)).ToList();
            HeliosPanel target = selected.FirstOrDefault(IsGroupPanel) as HeliosPanel;
            Rect bounds = GetBounds(selected);
            if (bounds.IsEmpty)
            {
                return;
            }

            UndoManager undo = ConfigManager.UndoManager;
            editor.SelectedItems.Clear();
            undo.StartBatch();
            try
            {
                if (target == null)
                {
                    target = CreatePanel(container, bounds, container.Children.IndexOf(inLayerOrder[0]));
                }
                else
                {
                    ExpandPanel(target, bounds);
                }

                foreach (HeliosVisual visual in inLayerOrder)
                {
                    if (visual == target)
                    {
                        continue;
                    }

                    if (IsGroupPanel(visual))
                    {
                        // merge: move this panel's contents into the target, then remove the empty panel
                        foreach (HeliosVisual child in visual.Children.ToList())
                        {
                            MoveInto(child, target, new Point(
                                visual.Left + child.Left - target.Left,
                                visual.Top + child.Top - target.Top));
                        }
                        DeletePanel(visual, editor);
                    }
                    else
                    {
                        MoveInto(visual, target, new Point(visual.Left - target.Left, visual.Top - target.Top));
                    }
                }
            }
            finally
            {
                undo.CloseBatch();
            }

            editor.SelectedItems.Add(target);
        }

        public static void Ungroup(HeliosVisualContainerEditor editor)
        {
            if (!CanUngroup(editor))
            {
                return;
            }

            HeliosVisual container = editor.VisualContainer;
            List<HeliosVisual> panels = editor.SelectedItems.Where(v => IsGroupPanel(v) && v.Parent == container).ToList();
            List<HeliosVisual> released = new List<HeliosVisual>();

            UndoManager undo = ConfigManager.UndoManager;
            editor.SelectedItems.Clear();
            undo.StartBatch();
            try
            {
                foreach (HeliosVisual panel in panels)
                {
                    // the panel's contents take its place in the layer order, keeping their own order
                    int index = container.Children.IndexOf(panel) + 1;
                    foreach (HeliosVisual child in panel.Children.ToList())
                    {
                        Point location = new Point(panel.Left + child.Left, panel.Top + child.Top);
                        Apply(new ReparentControlUndoEvent(child, container, index++, UniqueName(container, child.Name), location));
                        released.Add(child);
                    }
                    DeletePanel(panel, editor);
                }
            }
            finally
            {
                undo.CloseBatch();
            }

            foreach (HeliosVisual visual in released)
            {
                editor.SelectedItems.Add(visual);
            }
        }

        private static HeliosPanel CreatePanel(HeliosVisual container, Rect bounds, int index)
        {
            // invisible panel so the grouped devices look exactly as before
            HeliosPanel panel = new HeliosPanel
            {
                DrawBorder = false,
                FillBackground = false
            };
            panel.Name = NewPanelName;
            Apply(new ReparentControlUndoEvent(panel, container, index, UniqueName(container, NewPanelName), bounds.TopLeft));

            // now attached, so the size change is recorded normally
            panel.Width = bounds.Width;
            panel.Height = bounds.Height;
            return panel;
        }

        /// <summary>
        /// grow an existing panel to cover the bounds of the selection, keeping its current contents in place on screen
        /// </summary>
        private static void ExpandPanel(HeliosPanel panel, Rect bounds)
        {
            double shiftX = panel.Left - bounds.Left;
            double shiftY = panel.Top - bounds.Top;
            if (shiftX != 0d || shiftY != 0d)
            {
                foreach (HeliosVisual child in panel.Children)
                {
                    child.Left += shiftX;
                    child.Top += shiftY;
                }
            }

            // bounds already includes the panel itself, so the panel only ever grows
            panel.Rectangle = bounds;
        }

        private static void MoveInto(HeliosVisual visual, HeliosVisual panel, Point location)
        {
            Apply(new ReparentControlUndoEvent(visual, panel, panel.Children.Count, UniqueName(panel, visual.Name), location));
        }

        private static void DeletePanel(HeliosVisual panel, HeliosVisualContainerEditor editor)
        {
            // close its editor tab, as Delete does
            ProfileEditorCommands.CloseProfileItem.Execute(panel, editor);
            Apply(new ReparentControlUndoEvent(panel, null, 0, panel.Name, new Point(panel.Left, panel.Top)));
        }

        private static void Apply(IUndoItem item)
        {
            item.Do();
            ConfigManager.UndoManager.AddUndoItem(item);
        }

        /// <summary>
        /// names must be unique among siblings, so add a number if the name is already used in the destination
        /// </summary>
        private static string UniqueName(HeliosVisual container, string name)
        {
            string candidate = name;
            int suffix = 0;
            while (container.Children.ContainsKey(candidate))
            {
                suffix++;
                candidate = $"{name} {suffix}";
            }
            return candidate;
        }
    }
}
