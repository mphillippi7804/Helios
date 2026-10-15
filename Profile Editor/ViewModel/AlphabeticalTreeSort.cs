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

namespace GadrocsWorkshop.Helios.ProfileEditor.ViewModel
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// Shows every level under the "Monitors" folder of a tree in alphabetical order: monitors, the panels
    /// and devices on them, and each device's actions.
    ///
    /// Only the on-screen order changes; the tree items themselves keep their order, so layering and
    /// the Profile Explorer are not affected.  Items added or renamed later are kept in order too.
    /// </summary>
    internal class AlphabeticalTreeSort
    {
        private const string MonitorsFolderName = "Monitors";

        private readonly HashSet<ProfileExplorerTreeItemCollection> _sorted = new HashSet<ProfileExplorerTreeItemCollection>();

        /// <summary>
        /// start sorting the Monitors branch of a tree built for a whole profile
        /// </summary>
        public static void Attach(ProfileExplorerTreeItem profileRoot)
        {
            if (profileRoot == null)
            {
                return;
            }

            AlphabeticalTreeSort sorter = new AlphabeticalTreeSort();
            foreach (ProfileExplorerTreeItem item in profileRoot.Children)
            {
                sorter.SortIfMonitorsFolder(item);
            }

            // the Monitors folder is only created once there is something in it
            profileRoot.Children.CollectionChanged += sorter.ProfileRoot_CollectionChanged;
        }

        private void ProfileRoot_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null)
            {
                return;
            }
            foreach (ProfileExplorerTreeItem item in e.NewItems)
            {
                SortIfMonitorsFolder(item);
            }
        }

        private void SortIfMonitorsFolder(ProfileExplorerTreeItem item)
        {
            if (item.ItemType == ProfileExplorerTreeItemType.Folder && item.Name == MonitorsFolderName)
            {
                SortBranch(item);
            }
        }

        private void SortBranch(ProfileExplorerTreeItem item)
        {
            SortLevel(item.Children);
            foreach (ProfileExplorerTreeItem child in item.Children)
            {
                SortBranch(child);
            }
        }

        private void SortLevel(ProfileExplorerTreeItemCollection children)
        {
            if (!_sorted.Add(children))
            {
                return;
            }

            // the tree view shows this collection through its default view, so sorting the view sorts the display
            if (CollectionViewSource.GetDefaultView(children) is ListCollectionView view)
            {
                view.CustomSort = NaturalNameComparer.Instance;
                view.IsLiveSorting = true;
                view.LiveSortingProperties.Add(nameof(ProfileExplorerTreeItem.Name));
            }

            // new panels, devices or device folders appearing later get sorted as well
            children.CollectionChanged += Level_CollectionChanged;
        }

        private void Level_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null)
            {
                return;
            }
            foreach (ProfileExplorerTreeItem item in e.NewItems)
            {
                SortBranch(item);
            }
        }

        /// <summary>
        /// alphabetical, ignoring case, with numbers compared by value so "Panel 2" comes before "Panel 10"
        /// </summary>
        private class NaturalNameComparer : IComparer
        {
            public static readonly NaturalNameComparer Instance = new NaturalNameComparer();

            public int Compare(object x, object y)
            {
                return CompareNames((x as ProfileExplorerTreeItem)?.Name, (y as ProfileExplorerTreeItem)?.Name);
            }

            internal static int CompareNames(string a, string b)
            {
                a = a ?? "";
                b = b ?? "";
                int i = 0;
                int j = 0;
                while (i < a.Length && j < b.Length)
                {
                    if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                    {
                        int startA = i;
                        int startB = j;
                        while (i < a.Length && char.IsDigit(a[i])) i++;
                        while (j < b.Length && char.IsDigit(b[j])) j++;
                        string numberA = a.Substring(startA, i - startA).TrimStart('0');
                        string numberB = b.Substring(startB, j - startB).TrimStart('0');
                        if (numberA.Length != numberB.Length)
                        {
                            return numberA.Length.CompareTo(numberB.Length);
                        }
                        int numberCompare = string.CompareOrdinal(numberA, numberB);
                        if (numberCompare != 0)
                        {
                            return numberCompare;
                        }
                        continue;
                    }

                    int startTextA = i;
                    int startTextB = j;
                    while (i < a.Length && !char.IsDigit(a[i])) i++;
                    while (j < b.Length && !char.IsDigit(b[j])) j++;
                    int textCompare = string.Compare(
                        a.Substring(startTextA, i - startTextA),
                        b.Substring(startTextB, j - startTextB),
                        CultureInfo.CurrentCulture,
                        CompareOptions.IgnoreCase);
                    if (textCompare != 0)
                    {
                        return textCompare;
                    }
                }

                // shorter wins when one is a prefix of the other; fall back to exact order for stability
                int lengthCompare = (a.Length - i).CompareTo(b.Length - j);
                return lengthCompare != 0 ? lengthCompare : string.CompareOrdinal(a, b);
            }
        }
    }
}
