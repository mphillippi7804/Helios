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

namespace GadrocsWorkshop.Helios.ProfileEditor.ViewModel
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// Shows every level of a tree in alphabetical order, with numbers compared by value
    /// ("Panel 2" before "Panel 10").  Items with the same name are then ordered by an optional rank,
    /// which the Bindings panel uses to put input items before output items.
    ///
    /// Only the on-screen order changes; the tree items themselves keep their order, so layering and
    /// everything saved in the profile are not affected.  Items added or renamed later are kept in order too.
    /// </summary>
    internal class AlphabeticalTreeSort
    {
        private readonly HashSet<ProfileExplorerTreeItemCollection> _sorted = new HashSet<ProfileExplorerTreeItemCollection>();
        private readonly NaturalNameComparer _comparer;

        private AlphabeticalTreeSort(Func<ProfileExplorerTreeItem, int> rank)
        {
            _comparer = new NaturalNameComparer(rank);
        }

        /// <summary>
        /// sort every level of the tree whose top level is the given collection
        /// </summary>
        /// <param name="topLevel">the collection shown at the top of the tree view</param>
        /// <param name="rank">optional secondary order for items with the same name; lower comes first</param>
        public static void AttachAll(ProfileExplorerTreeItemCollection topLevel, Func<ProfileExplorerTreeItem, int> rank = null)
        {
            if (topLevel == null)
            {
                return;
            }
            new AlphabeticalTreeSort(rank).SortLevelAndBelow(topLevel);
        }

        /// <summary>
        /// rank for lists of things to bind: triggers (the Input side) before actions (the Output side)
        /// </summary>
        public static int TriggersFirst(ProfileExplorerTreeItem item) =>
            item.ItemType.HasFlag(ProfileExplorerTreeItemType.Trigger) ? 0 :
            item.ItemType.HasFlag(ProfileExplorerTreeItemType.Action) ? 1 : 0;

        /// <summary>
        /// rank for a device's bindings: its actions and their input bindings before its triggers and their output bindings
        /// </summary>
        public static int InputBindingsFirst(ProfileExplorerTreeItem item)
        {
            if (item.ItemType.HasFlag(ProfileExplorerTreeItemType.Binding))
            {
                return item.Parent != null && item.Parent.ItemType.HasFlag(ProfileExplorerTreeItemType.Trigger) ? 1 : 0;
            }
            return item.ItemType.HasFlag(ProfileExplorerTreeItemType.Trigger) ? 1 : 0;
        }

        private void SortLevelAndBelow(ProfileExplorerTreeItemCollection children)
        {
            SortLevel(children);
            foreach (ProfileExplorerTreeItem child in children)
            {
                SortLevelAndBelow(child.Children);
            }
        }

        private void SortLevel(ProfileExplorerTreeItemCollection children)
        {
            if (children == null || !_sorted.Add(children))
            {
                return;
            }

            // the tree view shows this collection through its default view, so sorting the view sorts the display
            if (CollectionViewSource.GetDefaultView(children) is ListCollectionView view)
            {
                view.CustomSort = _comparer;
                view.IsLiveSorting = true;
                view.LiveSortingProperties.Add(nameof(ProfileExplorerTreeItem.Name));
            }

            // things appearing later (new panels, devices, folders, bindings) get sorted as well
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
                SortLevelAndBelow(item.Children);
            }
        }

        private class NaturalNameComparer : IComparer
        {
            private readonly Func<ProfileExplorerTreeItem, int> _rank;

            public NaturalNameComparer(Func<ProfileExplorerTreeItem, int> rank)
            {
                _rank = rank;
            }

            public int Compare(object x, object y)
            {
                ProfileExplorerTreeItem a = x as ProfileExplorerTreeItem;
                ProfileExplorerTreeItem b = y as ProfileExplorerTreeItem;
                int result = CompareNames(a?.Name, b?.Name);
                if (result != 0 || _rank == null || a == null || b == null)
                {
                    return result;
                }
                return _rank(a).CompareTo(_rank(b));
            }

            /// <summary>
            /// alphabetical, ignoring case, with numbers compared by value so "Panel 2" comes before "Panel 10"
            /// </summary>
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

                // shorter wins when one is a prefix of the other; names that differ only by case keep a stable order
                int lengthCompare = (a.Length - i).CompareTo(b.Length - j);
                return lengthCompare != 0 ? lengthCompare : string.CompareOrdinal(a, b);
            }
        }
    }
}
