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

namespace GadrocsWorkshop.Helios.ProfileEditor.FindReplace
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Popup listing every device that contains the Find value, with Go To, Replace and Replace All.
    /// </summary>
    public partial class FindResultsWindow : Window
    {
        private readonly Func<HeliosProfile> _getProfile;
        private readonly Action<IList<HeliosVisual>> _goTo;
        private readonly ObservableCollection<SearchResultRow> _rows = new ObservableCollection<SearchResultRow>();

        /// <param name="getProfile">returns the profile currently loaded in the editor</param>
        /// <param name="goTo">opens the tabs containing the given devices and selects them</param>
        public FindResultsWindow(Func<HeliosProfile> getProfile, Action<IList<HeliosVisual>> goTo)
        {
            _getProfile = getProfile;
            _goTo = goTo;
            InitializeComponent();
            ResultsGrid.ItemsSource = _rows;
        }

        /// <summary>
        /// the value from the main window's Find input that these results are for
        /// </summary>
        public string SearchText { get; private set; }

        /// <summary>
        /// show a new set of results
        /// </summary>
        public void ShowResults(string searchText, IEnumerable<SearchResultRow> rows)
        {
            SearchText = searchText;
            _rows.Clear();
            foreach (SearchResultRow row in rows)
            {
                _rows.Add(row);
            }
            Title = $"Find All Results - \"{searchText}\"";
            StatusText.Text = "";
            UpdateSummary();
            UpdateButtons();
        }

        private void UpdateSummary()
        {
            int occurrences = _rows.Sum(r => r.MatchCount);
            SummaryText.Text = $"Found \"{SearchText}\" in {_rows.Count} device(s), {occurrences} value(s).";
        }

        private void UpdateButtons()
        {
            bool hasSelection = ResultsGrid.SelectedItems.Count > 0;
            GoToButton.IsEnabled = hasSelection;
            ReplaceButton.IsEnabled = hasSelection;
            ReplaceAllButton.IsEnabled = _rows.Count > 0;
        }

        private List<SearchResultRow> SelectedRows => ResultsGrid.SelectedItems.OfType<SearchResultRow>().ToList();

        private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // only react to double clicks on a row, not on headers or scroll bars
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is DataGridRow))
            {
                source = VisualTreeHelper.GetParent(source);
            }
            if (source is DataGridRow row && row.Item is SearchResultRow result)
            {
                _goTo(new List<HeliosVisual> { result.Visual });
            }
        }

        private void GoTo_Click(object sender, RoutedEventArgs e)
        {
            List<SearchResultRow> selected = SelectedRows;
            if (selected.Count == 0)
            {
                return;
            }
            _goTo(selected.Select(r => r.Visual).ToList());
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            List<SearchResultRow> selected = SelectedRows;
            if (selected.Count == 0)
            {
                return;
            }
            ReplaceResult result = ProfileSearch.Replace(selected.Select(r => r.Visual), SearchText, ReplaceTextBox.Text);
            AfterReplace(result);
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            HeliosProfile profile = _getProfile();
            if (profile == null)
            {
                return;
            }

            MessageBoxResult answer = MessageBox.Show(this,
                $"Replace every instance of \"{SearchText}\" with \"{ReplaceTextBox.Text}\" in the whole profile?\n\nThis can be undone with Edit > Undo.",
                "Replace All", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK)
            {
                return;
            }

            // newly matching devices may exist that were not in the list (e.g. added since the search)
            List<SearchResultRow> current = ProfileSearch.Find(profile, SearchText);
            foreach (SearchResultRow row in current.Where(r => _rows.All(existing => existing.Visual != r.Visual)))
            {
                _rows.Add(row);
            }

            ReplaceResult result = ProfileSearch.ReplaceAll(profile, SearchText, ReplaceTextBox.Text);
            AfterReplace(result);
        }

        private void AfterReplace(ReplaceResult result)
        {
            // keep the rows so the user can see what changed, but refresh their contents
            foreach (SearchResultRow row in _rows)
            {
                row.Refresh(SearchText);
            }
            UpdateSummary();
            UpdateButtons();

            string status = $"Replaced {result.Occurrences} occurrence(s) in {result.Devices} device(s).";
            if (result.ReadOnlySkipped > 0)
            {
                status += $" {result.ReadOnlySkipped} match(es) were skipped because they are generated names (for example interface binding names) and cannot be edited here.";
            }
            if (result.InvalidNameSkipped > 0)
            {
                status += $" {result.InvalidNameSkipped} device name(s) were left unchanged because the new name would be blank, contain characters other than letters, digits, spaces, '-' or '_', or match another device in the same panel.";
            }
            StatusText.Text = status;
        }
    }
}
