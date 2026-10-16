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

namespace GadrocsWorkshop.Helios.ProfileEditor.FindReplace
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using GadrocsWorkshop.Helios.Controls;

    /// <summary>
    /// One searchable text value somewhere on a device: a property of the control, a property of an
    /// item in one of its collections (e.g. a rotary switch position), or a field of one of its bindings.
    /// </summary>
    internal class SearchField
    {
        public SearchField(object source, PropertyInfo property, string label, bool isImage, bool forceReadOnly = false)
        {
            Source = source;
            Property = property;
            Label = label;
            IsImage = isImage;
            IsWritable = !forceReadOnly && property.CanWrite && property.GetSetMethod() != null;
        }

        public object Source { get; }
        public PropertyInfo Property { get; }
        public string Label { get; }
        public bool IsImage { get; }
        public bool IsWritable { get; }

        public string Value
        {
            get
            {
                try
                {
                    return Property.GetValue(Source) as string;
                }
                catch (Exception)
                {
                    // some computed properties throw when the control is not fully configured
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// One row in the Find All results: a device (control) that contains at least one match.
    /// </summary>
    public class SearchResultRow : NotifyPropertyChanged
    {
        private string _panel;
        private string _device;
        private string _bindings;
        private string _path;
        private string _images;
        private int _matchCount;

        public SearchResultRow(HeliosVisual visual)
        {
            Visual = visual;
        }

        /// <summary>
        /// the device this row represents
        /// </summary>
        public HeliosVisual Visual { get; }

        public string Panel
        {
            get => _panel;
            private set => SetField(ref _panel, value, nameof(Panel));
        }

        public string Device
        {
            get => _device;
            private set => SetField(ref _device, value, nameof(Device));
        }

        public string Bindings
        {
            get => _bindings;
            private set => SetField(ref _bindings, value, nameof(Bindings));
        }

        public string Path
        {
            get => _path;
            private set => SetField(ref _path, value, nameof(Path));
        }

        public string Images
        {
            get => _images;
            private set => SetField(ref _images, value, nameof(Images));
        }

        /// <summary>
        /// number of fields on this device that contain the search text
        /// </summary>
        public int MatchCount
        {
            get => _matchCount;
            private set => SetField(ref _matchCount, value, nameof(MatchCount));
        }

        /// <summary>
        /// recalculate all displayed columns from the current state of the device
        /// </summary>
        public void Refresh(string searchText)
        {
            Panel = ProfileSearch.GetPanelName(Visual);
            Device = Visual.Name;
            Path = ProfileSearch.GetPath(Visual);
            Bindings = string.Join(Environment.NewLine, ProfileSearch.GetInterfaceBindingNames(Visual));

            List<SearchField> fields = ProfileSearch.GetFields(Visual).ToList();
            Images = string.Join(Environment.NewLine, fields
                .Where(f => f.IsImage)
                .Select(f => f.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            MatchCount = string.IsNullOrEmpty(searchText)
                ? 0
                : fields.Count(f => ProfileSearch.Contains(f.Value, searchText));
        }
    }

    /// <summary>
    /// minimal INotifyPropertyChanged base so the results grid updates after a replace
    /// </summary>
    public abstract class NotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// outcome of a replace operation
    /// </summary>
    public class ReplaceResult
    {
        /// <summary>
        /// number of text occurrences replaced
        /// </summary>
        public int Occurrences { get; set; }

        /// <summary>
        /// number of devices that were changed
        /// </summary>
        public int Devices { get; set; }

        /// <summary>
        /// number of matching values that could not be changed because they are generated
        /// (for example binding descriptions that come from interface names)
        /// </summary>
        public int ReadOnlySkipped { get; set; }

        /// <summary>
        /// number of device names that were not changed because the result would not be a valid name
        /// (blank, characters other than letters, digits, space, '-' and '_', or the same as a sibling's name);
        /// such names would break binding references in the saved profile
        /// </summary>
        public int InvalidNameSkipped { get; set; }
    }

    /// <summary>
    /// Searches every device in a profile for a piece of text and replaces it.
    /// </summary>
    public static class ProfileSearch
    {
        /// <summary>
        /// properties that are plumbing rather than user data and must never be searched or replaced
        /// </summary>
        private static readonly HashSet<string> ExcludedProperties = new HashSet<string>
        {
            nameof(HeliosObject.TypeIdentifier),
            nameof(HeliosVisual.Children),
            nameof(HeliosVisual.Parent),
            nameof(HeliosVisual.Monitor),
            nameof(HeliosObject.Profile),
            nameof(HeliosObject.Actions),
            nameof(HeliosObject.Triggers),
            nameof(HeliosObject.Values),
            nameof(HeliosObject.InputBindings),
            nameof(HeliosObject.OutputBindings),
            nameof(HeliosObject.OpaqueHandles),
            nameof(HeliosVisual.NonClickableZones),
            nameof(HeliosVisual.Renderer)
        };

        /// <summary>
        /// the same rule the Properties panel enforces for device names
        /// </summary>
        private static readonly Regex ValidName = new Regex("^[a-zA-Z0-9-_ ]*$");

        /// <summary>
        /// true if a device could be renamed to this without breaking the profile: same rules as the Properties panel,
        /// and no sibling already uses the name
        /// </summary>
        internal static bool IsValidNewName(HeliosVisual visual, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || !ValidName.IsMatch(newName))
            {
                return false;
            }
            if (visual.Parent != null && !newName.Equals(visual.Name) && visual.Parent.Children.ContainsKey(newName))
            {
                return false;
            }
            return true;
        }

        internal static bool Contains(string value, string searchText) =>
            !string.IsNullOrEmpty(value)
            && !string.IsNullOrEmpty(searchText)
            && value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Find every device in the profile that contains the search text anywhere (name, text properties,
        /// image paths, collection items, binding values, binding conditions and binding names).
        /// Matching is case insensitive.
        /// </summary>
        public static List<SearchResultRow> Find(HeliosProfile profile, string searchText)
        {
            List<SearchResultRow> rows = new List<SearchResultRow>();
            if (profile == null || string.IsNullOrEmpty(searchText))
            {
                return rows;
            }

            foreach (HeliosVisual visual in AllVisuals(profile))
            {
                if (!GetFields(visual).Any(f => Contains(f.Value, searchText)))
                {
                    continue;
                }
                SearchResultRow row = new SearchResultRow(visual);
                row.Refresh(searchText);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// Replace the search text in the given devices only.  All changes form a single undo step.
        /// </summary>
        public static ReplaceResult Replace(IEnumerable<HeliosVisual> visuals, string searchText, string replacement)
        {
            ReplaceResult result = new ReplaceResult();
            if (string.IsNullOrEmpty(searchText))
            {
                return result;
            }
            replacement = replacement ?? "";
            Regex pattern = new Regex(Regex.Escape(searchText), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            UndoManager undo = ConfigManager.UndoManager;
            undo?.StartBatch();
            try
            {
                foreach (HeliosVisual visual in visuals.Distinct().ToList())
                {
                    bool changed = false;

                    // snapshot the field list first, because renaming can change derived values
                    foreach (SearchField field in GetFields(visual).ToList())
                    {
                        string oldValue = field.Value;
                        if (!Contains(oldValue, searchText))
                        {
                            continue;
                        }
                        if (!field.IsWritable)
                        {
                            result.ReadOnlySkipped++;
                            continue;
                        }

                        int occurrences = pattern.Matches(oldValue).Count;
                        string newValue = pattern.Replace(oldValue, _ => replacement);

                        if (field.Source is HeliosVisual namedVisual
                            && field.Property.Name == nameof(HeliosObject.Name)
                            && !IsValidNewName(namedVisual, newValue))
                        {
                            result.InvalidNameSkipped++;
                            continue;
                        }
                        try
                        {
                            field.Property.SetValue(field.Source, newValue);
                        }
                        catch (Exception ex)
                        {
                            ConfigManager.LogManager.LogError($"Find/Replace could not set {field.Label} on {visual.Name}", ex);
                            continue;
                        }

                        // changes to controls propagate up to the profile and are recorded automatically,
                        // but bindings and collection items do not, so record those explicitly
                        if (!(field.Source is HeliosVisual))
                        {
                            undo?.AddPropertyChange(field.Source, field.Property.Name, oldValue, newValue);
                        }

                        result.Occurrences += occurrences;
                        changed = true;
                    }

                    if (changed)
                    {
                        result.Devices++;
                    }
                }
            }
            finally
            {
                undo?.CloseBatch();
            }
            return result;
        }

        /// <summary>
        /// Replace the search text everywhere in the profile.  All changes form a single undo step.
        /// </summary>
        public static ReplaceResult ReplaceAll(HeliosProfile profile, string searchText, string replacement)
        {
            return Replace(Find(profile, searchText).Select(r => r.Visual), searchText, replacement);
        }

        /// <summary>
        /// every device in the profile, depth first, starting with each monitor
        /// </summary>
        public static IEnumerable<HeliosVisual> AllVisuals(HeliosProfile profile)
        {
            foreach (Monitor monitor in profile.Monitors)
            {
                foreach (HeliosVisual visual in Descendants(monitor))
                {
                    yield return visual;
                }
            }
        }

        private static IEnumerable<HeliosVisual> Descendants(HeliosVisual root)
        {
            yield return root;
            if (!root.PersistChildren)
            {
                // internal parts of a composite control that are rebuilt by code and never saved;
                // editing them would be lost on reload, so they are not searched
                yield break;
            }
            foreach (HeliosVisual child in root.Children.ToList())
            {
                foreach (HeliosVisual visual in Descendants(child))
                {
                    yield return visual;
                }
            }
        }

        /// <summary>
        /// name of the nearest panel containing the device, or empty if it sits directly on a monitor
        /// </summary>
        public static string GetPanelName(HeliosVisual visual)
        {
            for (HeliosVisual parent = visual.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is HeliosPanel)
                {
                    return parent.Name;
                }
            }
            return "";
        }

        /// <summary>
        /// parent/child path from the monitor down to the device, formatted like a file path
        /// </summary>
        public static string GetPath(HeliosVisual visual)
        {
            List<string> parts = new List<string>();
            for (HeliosVisual current = visual; current != null; current = current.Parent)
            {
                parts.Add(current.Name);
            }
            parts.Reverse();
            return string.Join("\\", parts);
        }

        /// <summary>
        /// names of all bindings between this device and any interface
        /// </summary>
        public static IEnumerable<string> GetInterfaceBindingNames(HeliosVisual visual)
        {
            foreach (HeliosBinding binding in AllBindings(visual))
            {
                string description = null;
                try
                {
                    if (binding.Trigger?.Owner is HeliosInterface || binding.Action?.Owner is HeliosInterface)
                    {
                        description = binding.Description;
                    }
                }
                catch (Exception)
                {
                    // binding to something no longer loaded; ignore
                }
                if (!string.IsNullOrWhiteSpace(description))
                {
                    yield return description;
                }
            }
        }

        private static IEnumerable<HeliosBinding> AllBindings(HeliosObject heliosObject) =>
            heliosObject.InputBindings.Concat(heliosObject.OutputBindings).Distinct().ToList();

        /// <summary>
        /// all searchable text values belonging to a device
        /// </summary>
        internal static IEnumerable<SearchField> GetFields(HeliosVisual visual)
        {
            foreach (PropertyInfo property in visual.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || ExcludedProperties.Contains(property.Name) || property.GetGetMethod() == null)
                {
                    continue;
                }

                if (property.PropertyType == typeof(string))
                {
                    // only user-editable text is interesting; read-only strings are computed
                    if (property.CanWrite && property.GetSetMethod() != null)
                    {
                        yield return new SearchField(visual, property, property.Name, IsImageProperty(property));
                    }
                    continue;
                }

                if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                // collections of configuration items, such as rotary switch positions
                foreach (SearchField field in GetCollectionFields(visual, property))
                {
                    yield return field;
                }
            }

            // bindings attached to this device
            PropertyInfo valueProperty = typeof(HeliosBinding).GetProperty(nameof(HeliosBinding.Value));
            PropertyInfo conditionProperty = typeof(HeliosBinding).GetProperty(nameof(HeliosBinding.Condition));
            PropertyInfo descriptionProperty = typeof(HeliosBinding).GetProperty(nameof(HeliosBinding.Description));
            foreach (HeliosBinding binding in AllBindings(visual))
            {
                yield return new SearchField(binding, valueProperty, "Binding Value", false);
                yield return new SearchField(binding, conditionProperty, "Binding Condition", false);
                yield return new SearchField(binding, descriptionProperty, "Binding", false, forceReadOnly: true);
            }
        }

        private static IEnumerable<SearchField> GetCollectionFields(HeliosVisual visual, PropertyInfo property)
        {
            IEnumerable collection;
            try
            {
                collection = property.GetValue(visual) as IEnumerable;
            }
            catch (Exception)
            {
                yield break;
            }
            if (collection == null)
            {
                yield break;
            }

            List<object> items;
            try
            {
                items = collection.Cast<object>().ToList();
            }
            catch (Exception)
            {
                yield break;
            }

            for (int index = 0; index < items.Count; index++)
            {
                object item = items[index];

                // never descend into other Helios objects here; child controls are visited on their own
                if (item == null || item is HeliosObject || item is string || item.GetType().IsPrimitive)
                {
                    continue;
                }

                foreach (PropertyInfo itemProperty in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (itemProperty.PropertyType != typeof(string)
                        || itemProperty.GetIndexParameters().Length > 0
                        || itemProperty.GetGetMethod() == null
                        || !itemProperty.CanWrite
                        || itemProperty.GetSetMethod() == null)
                    {
                        continue;
                    }
                    yield return new SearchField(item, itemProperty, $"{property.Name}[{index}].{itemProperty.Name}", IsImageProperty(itemProperty));
                }
            }
        }

        private static bool IsImageProperty(PropertyInfo property) =>
            property.Name.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
