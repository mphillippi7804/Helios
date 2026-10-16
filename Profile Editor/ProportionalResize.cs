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
    /// <summary>
    /// Resizes a control and scales everything inside it by the same proportion, the same way dragging
    /// the resize handles does.  For example a panel going from 100 to 90 wide makes each contained
    /// device 10% narrower and moves its Left 10% closer to the panel's left edge.
    /// </summary>
    internal static class ProportionalResize
    {
        /// <summary>
        /// set a new width and/or height (null leaves that dimension alone), then scale the contents
        /// by however much the control actually changed (aspect lock may change the other dimension too).
        /// The whole change is one undo step.
        /// </summary>
        public static void Resize(HeliosVisual control, double? width, double? height)
        {
            if (control == null || control is Monitor)
            {
                // monitors match physical displays; their contents are never rescaled by typing a size
                if (control != null)
                {
                    if (width.HasValue) control.Width = width.Value;
                    if (height.HasValue) control.Height = height.Value;
                }
                return;
            }

            double oldWidth = control.Width;
            double oldHeight = control.Height;

            UndoManager undo = ConfigManager.UndoManager;
            bool ownBatch = undo != null && !undo.InBatch;
            if (ownBatch)
            {
                undo.StartBatch();
            }
            try
            {
                if (width.HasValue)
                {
                    control.Width = width.Value;
                }
                if (height.HasValue)
                {
                    control.Height = height.Value;
                }

                double scaleX = oldWidth > 0d ? control.Width / oldWidth : 1d;
                double scaleY = oldHeight > 0d ? control.Height / oldHeight : 1d;

                // ReSharper disable CompareOfFloatsByEqualityOperator exact comparison is intended
                if (scaleX != 1d || scaleY != 1d)
                {
                    control.ScaleChildren(scaleX, scaleY);
                }
                // ReSharper restore CompareOfFloatsByEqualityOperator
            }
            finally
            {
                if (ownBatch)
                {
                    undo.CloseBatch();
                }
            }
        }
    }
}
