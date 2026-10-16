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

namespace GadrocsWorkshop.Helios.ProfileEditor.ViewModel
{
    using System;

    /// <summary>
    /// these values are used to indicate what type of Helios object is represented by a tree item and
    /// also combined via OR to create a mask to indicate what sort of items will be included when building
    /// a subtree, to create different sorts of views in the UI
    /// </summary>
    [Flags]
    public enum ProfileExplorerTreeItemType
    {
        Folder =    0x001,
        Monitor =   0x002,
        Panel =     0x004,
        Interface = 0x008,
        Visual =    0x010,
        Action =    0x020,
        Trigger =   0x040,
        Value =     0x080,
        Binding =   0x100,
        Profile =   0x200,

        // the following are never item types; they only change how actions and triggers are arranged
        // when included in the mask used to build a subtree

        /// <summary>
        /// put actions and triggers in "Input" and "Output" folders
        /// </summary>
        GroupInputOutput = 0x400,

        /// <summary>
        /// group actions and triggers by the name of the thing they control (for example "Hook Handle"),
        /// with the "Input" and "Output" folders under that name instead of at the top
        /// </summary>
        GroupByElementName = 0x800,

        /// <summary>
        /// for interfaces, treat triggers as "Input" and actions as "Output" (the view of things available to bind);
        /// devices on monitors and panels, and everything without this flag, show actions feeding the device as
        /// "Input" and its triggers as "Output"
        /// </summary>
        TriggersAreInput = 0x1000
    }
}
