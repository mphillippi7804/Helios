//  Copyright 2014 Craig Courtney
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

namespace GadrocsWorkshop.Helios
{
    using System;
    using System.Windows;

    public abstract class HeliosVisualContainer : HeliosVisual
    {
        // private HeliosVisual _mouseDownVisual;

        public HeliosVisualContainer(string name, Size nativeSize)
            : base(name, nativeSize)
        {
        }

        public override void Reset()
        {
            base.Reset();
            foreach(HeliosVisual child in Children)
            {
                child.Reset();
            }
        }

        public override void ScaleChildren(double scaleX, double scaleY)
        {
            foreach (HeliosVisual visual in Children)
            {
                // position scales with the container, including devices that hang off the left or top edge
                visual.Left *= scaleX;
                visual.Top *= scaleY;

                // a device turned 90 degrees has its own width running along the container's height
                double childScaleX = scaleX;
                double childScaleY = scaleY;
                if (visual.Rotation != HeliosVisualRotation.None && visual.Rotation != HeliosVisualRotation.ROT180)
                {
                    childScaleX = scaleY;
                    childScaleY = scaleX;
                }

                visual.Width = Math.Max(visual.Width * childScaleX, 1d);
                visual.Height = Math.Max(visual.Height * childScaleY, 1d);

                visual.ScaleChildren(childScaleX, childScaleY);
            }
        }

        //public override bool MouseDown(Point location)
        //{
        //    Point transformedPoint = TransformLocation(location);
        //    foreach (HeliosVisual visual in Children.Reverse())
        //    {
        //        if (!visual.IsHidden && visual.DisplayRectangle.Contains(transformedPoint))
        //        {
        //            Point visualPoint = transformedPoint;
        //            visualPoint.Offset(-visual.Left, -visual.Top);
        //            if (visual.MouseDown(visualPoint))
        //            {
        //                _mouseDownVisual = visual;
        //                break;
        //            }
        //        }
        //    }
        //    return true;
        //}

        //public override void MouseDrag(Point location)
        //{
        //    if (_mouseDownVisual != null)
        //    {
        //        Point visualPoint = TransformLocation(location);
        //        visualPoint.Offset(-_mouseDownVisual.Left, -_mouseDownVisual.Top);
        //        _mouseDownVisual.MouseDrag(visualPoint);
        //    }
        //}

        //public override void MouseUp(Point location)
        //{
        //    if (_mouseDownVisual != null)
        //    {
        //        Point visualPoint = TransformLocation(location);
        //        visualPoint.Offset(-_mouseDownVisual.Left, -_mouseDownVisual.Top);
        //        _mouseDownVisual.MouseUp(visualPoint);
        //    }
        //    _mouseDownVisual = null;
        //}
    }
}
