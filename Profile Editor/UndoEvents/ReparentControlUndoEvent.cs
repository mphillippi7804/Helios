//  Copyright 2026 Helios Contributors (written by mphillippi7804 for a modified version of Helios)
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

namespace GadrocsWorkshop.Helios.ProfileEditor.UndoEvents
{
    using System;
    using System.Windows;

    /// <summary>
    /// Moves one control from one container to another (or adds it, or deletes it), changing its name
    /// and location at the same time.  The same control instance is moved, so its bindings are kept.
    ///
    /// The control is changed only while it is detached from the profile, so these changes are not
    /// also recorded as separate property changes.
    /// </summary>
    internal class ReparentControlUndoEvent : IUndoItem
    {
        private readonly HeliosVisual _control;

        private readonly HeliosVisual _from;
        private readonly int _fromIndex;
        private readonly string _fromName;
        private readonly Point _fromLocation;

        private readonly HeliosVisual _to;
        private readonly int _toIndex;
        private readonly string _toName;
        private readonly Point _toLocation;

        /// <param name="control">the control to move</param>
        /// <param name="to">new container, or null to delete the control</param>
        /// <param name="toIndex">position in the new container's children (z-order); clamped to the end</param>
        /// <param name="toName">name in the new container</param>
        /// <param name="toLocation">Left and Top in the new container</param>
        public ReparentControlUndoEvent(HeliosVisual control, HeliosVisual to, int toIndex, string toName, Point toLocation)
        {
            _control = control;

            _from = control.Parent;
            _fromIndex = _from?.Children.IndexOf(control) ?? -1;
            _fromName = control.Name;
            _fromLocation = new Point(control.Left, control.Top);

            _to = to;
            _toIndex = toIndex;
            _toName = toName;
            _toLocation = toLocation;
        }

        public void Do()
        {
            Move(_from, _to, _toIndex, _toName, _toLocation);
        }

        public void Undo()
        {
            Move(_to, _from, _fromIndex, _fromName, _fromLocation);
        }

        private void Move(HeliosVisual source, HeliosVisual destination, int index, string name, Point location)
        {
            source?.Children.Remove(_control);

            // detached now, so none of this is recorded separately
            _control.Name = name;
            _control.Left = location.X;
            _control.Top = location.Y;

            if (destination == null)
            {
                return;
            }

            int clamped = Math.Max(0, Math.Min(index, destination.Children.Count));
            destination.Children.Insert(clamped, _control);
        }
    }
}
