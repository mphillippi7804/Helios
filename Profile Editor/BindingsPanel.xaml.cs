//  Copyright 2014 Craig Courtney
//  Copyright 2022 Helios Contributors
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
    using GadrocsWorkshop.Helios.ProfileEditor.UndoEvents;
    using GadrocsWorkshop.Helios.ProfileEditor.ViewModel;
    using GadrocsWorkshop.Helios.Windows.Controls;
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;

    public enum BindingPanelType
    {
        Input,
        Output
    }

    /// <summary>
    /// Interaction logic for InputBindingsPanel.xaml
    /// </summary>
    public partial class BindingsPanel : UserControl
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private HeliosProfile _actionTriggerProfile = null;

        /// <summary>
        /// everything in the profile that can be bound: triggers and actions together
        /// </summary>
        private ProfileExplorerTreeItem _sourceList = null;

        static BindingsPanel()
        {
            Type ownerType = typeof(BindingsPanel);

            CommandManager.RegisterClassCommandBinding(ownerType, new CommandBinding(ApplicationCommands.Delete, Delete_Executed, Delete_CanExecute));
        }

        public BindingsPanel()
        {
            DataContext = this;
            InitializeComponent();

            ProfileExplorerItems = new ProfileExplorerTreeItemCollection();
            LoadBindings();
            LoadSources();
        }

        #region Properties

        public BindingPanelType BindingType
        {
            get { return (BindingPanelType)GetValue(BindingTypeProperty); }
            set { SetValue(BindingTypeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BindingType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BindingTypeProperty =
            DependencyProperty.Register("BindingType", typeof(BindingPanelType), typeof(BindingsPanel), new PropertyMetadata(BindingPanelType.Input, OnBindingTypeChanged));


        public ProfileExplorerTreeItemCollection ProfileExplorerItems
        {
            get { return (ProfileExplorerTreeItemCollection)GetValue(ProfileExplorerItemsProperty); }
            set { SetValue(ProfileExplorerItemsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ProfileExplorerItems.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProfileExplorerItemsProperty =
            DependencyProperty.Register("ProfileExplorerItems", typeof(ProfileExplorerTreeItemCollection), typeof(BindingsPanel), new PropertyMetadata(null));

        public ProfileExplorerTreeItemCollection BindingItems
        {
            get { return (ProfileExplorerTreeItemCollection)GetValue(BindingItemsProperty); }
            set { SetValue(BindingItemsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BindingItems.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BindingItemsProperty =
            DependencyProperty.Register("BindingItems", typeof(ProfileExplorerTreeItemCollection), typeof(BindingsPanel), new PropertyMetadata(null));

        public HeliosObject BindingObject
        {
            get { return (HeliosObject)GetValue(BindingObjectProperty); }
            set { SetValue(BindingObjectProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Profile.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BindingObjectProperty =
            DependencyProperty.Register("BindingObject", typeof(HeliosObject), typeof(BindingsPanel), new PropertyMetadata(null, OnBindingFocusChanged));

        public HeliosBinding SelectedBinding
        {
            get { return (HeliosBinding)GetValue(SelectedBindingProperty); }
            set { SetValue(SelectedBindingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedBinding.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedBindingProperty =
            DependencyProperty.Register("SelectedBinding", typeof(HeliosBinding), typeof(BindingsPanel), new PropertyMetadata(null));

        public StaticValueEditor ValueEditor
        {
            get { return (StaticValueEditor)GetValue(ValueEditorProperty); }
            set { SetValue(ValueEditorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ValueEditor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueEditorProperty =
            DependencyProperty.Register("ValueEditor", typeof(StaticValueEditor), typeof(BindingsPanel), new PropertyMetadata(null));

        public bool BMSFalconPathExists
        {
            get => ConfigManager.BMSFalconPath.Length > 0;
        }

        #endregion

        private static void OnBindingFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            BindingsPanel p = d as BindingsPanel;
            p.LoadBindings();
            p.LoadSources();
        }

        private static void OnBindingTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            BindingsPanel p = d as BindingsPanel;
            p.LoadBindings();
            p.LoadSources();
        }

        public void Clear()
        {
            ClearBindings();
            ProfileExplorerItems = null;
            ClearSources();
        }

        /// <summary>
        /// the selected object's actions with their input bindings and its triggers with their output bindings,
        /// shown together (formerly the separate Input and Output tabs)
        /// </summary>
        private void LoadBindings()
        {
            if (BindingObject != null)
            {
                ClearBindings();
                BindingItems = new ProfileExplorerTreeItemCollection();

                // "Input": the actions that feed this device; "Output": the triggers it sends out
                ProfileExplorerTreeItem bindings = new ProfileExplorerTreeItem(BindingObject,
                    ProfileExplorerTreeItemType.Action | ProfileExplorerTreeItemType.Trigger | ProfileExplorerTreeItemType.Binding |
                    ProfileExplorerTreeItemType.GroupInputOutput);
                if (bindings.HasChildren)
                {
                    foreach (ProfileExplorerTreeItem item in bindings.Children)
                    {
                        // show everything for the selected device: Input/Output, its actions and triggers, and their bindings
                        item.ExpandAll();
                        BindingItems.Add(item);
                    }
                }

                // alphabetical at every level; for the same name, input before output
                AlphabeticalTreeSort.AttachAll(BindingItems, AlphabeticalTreeSort.InputBindingsFirst);
            }
            else
            {
                BindingItems = null;
            }
        }

        private void ClearBindings()
        {
            if (BindingItems != null)
            {
                BindingItems.Disconnect();
                BindingItems = null;
            }
        }

        /// <summary>
        /// everything in the profile that can be bound, triggers and actions together (formerly the separate
        /// Input and Output tabs); drag a trigger onto an action, or an action onto a trigger, in the Bindings list
        /// </summary>
        private void LoadSources()
        {
            ProfileExplorerTreeItemType sourceTypes = ProfileExplorerTreeItemType.Interface | ProfileExplorerTreeItemType.Monitor |
                                                      ProfileExplorerTreeItemType.Panel | ProfileExplorerTreeItemType.Visual |
                                                      ProfileExplorerTreeItemType.Trigger | ProfileExplorerTreeItemType.Action |
                                                      // device > name of the thing > "Input" (triggers providing data) / "Output" (actions sending data)
                                                      ProfileExplorerTreeItemType.GroupByElementName | ProfileExplorerTreeItemType.GroupInputOutput |
                                                      ProfileExplorerTreeItemType.TriggersAreInput;

            if (BindingObject != null && BindingObject.Profile != _actionTriggerProfile)
            {
                ClearSources();
                HeliosProfile newProfile = BindingObject.Profile;
                if (newProfile != null)
                {
                    Logger.Debug("loading triggers and actions for bindings");
                    _sourceList = new ProfileExplorerTreeItem(newProfile, sourceTypes);

                    // alphabetical at every level; for the same name, input (triggers) before output (actions)
                    AlphabeticalTreeSort.AttachAll(_sourceList.Children, AlphabeticalTreeSort.TriggersFirst);
                }
                _actionTriggerProfile = newProfile;
            }

            ProfileExplorerItems = _sourceList?.Children;
        }

        private void ClearSources()
        {
            if (_sourceList != null)
            {
                _sourceList.Disconnect();
                _sourceList = null;
            }
        }

        private void UpdateValueEditor()
        {
            StaticValueEditor editor = null;

            if (BindingObject != null && SelectedBinding != null && SelectedBinding.ValueSource != BindingValueSources.TriggerValue)
            {
                if (SelectedBinding.Action.ValueEditorType == null || SelectedBinding.ValueSource == BindingValueSources.LuaScript)
                {
                    editor = new TextStaticEditor();
                }
                else
                {
                    editor = (StaticValueEditor)Activator.CreateInstance(SelectedBinding.Action.ValueEditorType);
                }

                Binding bind = new Binding("SelectedBinding.Value");
                editor.Profile = BindingObject.Profile;
                editor.StaticValue = SelectedBinding.Value;

                bind.Source = this;
                bind.Mode = BindingMode.TwoWay;
                bind.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                editor.SetBinding(StaticValueEditor.StaticValueProperty, bind);
            }

            ValueEditor = editor;
        }

        private void BindingsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ProfileExplorerTreeItem selectedItem = e.NewValue as ProfileExplorerTreeItem;

            // selecting an entry opens everything beneath it
            selectedItem?.ExpandAll();

            if (selectedItem != null && selectedItem.ItemType.HasFlag(ProfileExplorerTreeItemType.Binding))
            {
                SelectedBinding = (HeliosBinding)selectedItem.ContextItem;
                UpdateValueEditor();
            }
            else
            {
                SelectedBinding = null;
            }
        }

        private void ValueSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateValueEditor();
        }

        #region Command Handlers

        private static void Delete_CanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            BindingsPanel editor = target as BindingsPanel;
            HeliosBinding binding = e.Parameter as HeliosBinding;
            if (editor != null)
            {
                e.Handled = true;
                e.CanExecute = binding != null || editor.SelectedBinding != null;
            }
        }

        private static void Delete_Executed(object target, ExecutedRoutedEventArgs e)
        {
            BindingsPanel editor = target as BindingsPanel;
            HeliosBinding binding = e.Parameter as HeliosBinding;
            if (editor != null)
            {
                BindingDeleteUndoEvent undo;
                if (binding != null)
                {
                    undo = new BindingDeleteUndoEvent(binding);
                }
                else
                {
                    undo = new BindingDeleteUndoEvent(editor.SelectedBinding);
                }
                undo.Do();
                ConfigManager.UndoManager.AddUndoItem(undo);
                e.Handled = true;
            }
        }

        #endregion

    }
}
