// Copyright 2020 Ammo Goettsch
// 
// Helios is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Helios is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 

namespace GadrocsWorkshop.Helios.ProfileEditor
{
    /// <summary>
    /// Global options for Profile Editor configured via Tools | Options menu or similar.
    /// Client code may use static accessors to check settings.  UI code will use properties against an instance.
    /// NOTE: the string names of properties in the XML file are intentionally not set from the property names
    /// in case the property names change later.
    /// </summary>
    public class GlobalOptions : Helios.GlobalOptions
    {
        private const string SETTINGS_GROUP = "ProfileEditor";

        /// <summary>
        /// backing field for property DefaultCascadeTriggers, contains
        /// true if newly created bindings are configured to cascade triggers
        /// </summary>
        private bool _defaultCascadeTriggers;

        /// <summary>
        /// backing field for property DefaultFillSecondaryMonitors, contains
        /// true if monitors other than the main monitor are filled with a background
        /// color in newly created profiles
        /// </summary>
        private bool _defaultFillSecondaryMonitors;

        /// <summary>
        /// backing field for property DefaultAlwaysOnTop, contains
        /// true if all monitors are set to always display on top in newly
        /// created profiles
        /// </summary>
        private bool _defaultAlwaysOnTop;

        /// <summary>
        /// backing field for property CacheImages, contains
        /// true if image sources and similar objects should be reused by ImageManager
        /// </summary>
        private bool _cacheImages;

        private bool _classicBindingsLayout;

        /// <summary>
        /// raised when the bindings layout option changes, so open Bindings panels can switch layout immediately
        /// </summary>
        public static event System.EventHandler ClassicBindingsLayoutChanged;

        public GlobalOptions()
        {
            _defaultCascadeTriggers = HasDefaultCascadeTriggers;
            _defaultFillSecondaryMonitors = HasDefaultFillSecondaryMonitors;
            _defaultAlwaysOnTop = HasDefaultAlwaysOnTop;
            _cacheImages = HasCacheImages;
            _classicBindingsLayout = HasClassicBindingsLayout;
        }

        #region Properties

        /// <summary>
        /// true if newly created bindings are configured to cascade triggers
        /// </summary>
        public bool DefaultCascadeTriggers
        {
            get => _defaultCascadeTriggers;
            set
            {
                if (_defaultCascadeTriggers == value)
                {
                    return;
                }

                bool oldValue = _defaultCascadeTriggers;
                _defaultCascadeTriggers = value;
                ConfigManager.SettingsManager.SaveSetting(SETTINGS_GROUP, "DefaultCascadeTriggers", value);
                OnPropertyChanged(nameof(DefaultCascadeTriggers), oldValue, value, true);
            }
        }

        /// <summary>
        /// true if monitors other than the main monitor are filled with a background
        /// color in newly created profiles
        /// </summary>
        public bool DefaultFillSecondaryMonitors
        {
            get => _defaultFillSecondaryMonitors;
            set
            {
                if (_defaultFillSecondaryMonitors == value)
                {
                    return;
                }

                bool oldValue = _defaultFillSecondaryMonitors;
                _defaultFillSecondaryMonitors = value;
                ConfigManager.SettingsManager.SaveSetting(SETTINGS_GROUP, "DefaultFillSecondaryMonitors", value);
                OnPropertyChanged(nameof(DefaultFillSecondaryMonitors), oldValue, value, true);
            }
        }

        /// <summary>
        /// true if all monitors are set to always display on top in newly
        /// created profiles
        /// </summary>
        public bool DefaultAlwaysOnTop
        {
            get => _defaultAlwaysOnTop;
            set
            {
                if (_defaultAlwaysOnTop == value)
                {
                    return;
                }

                bool oldValue = _defaultAlwaysOnTop;
                _defaultAlwaysOnTop = value;
                ConfigManager.SettingsManager.SaveSetting(SETTINGS_GROUP, "DefaultAlwaysOnTop", value);
                OnPropertyChanged(nameof(DefaultAlwaysOnTop), oldValue, value, true);
            }
        }

        /// <summary>
        /// true if image sources and similar objects should be reused by ImageManager
        /// </summary>
        public bool CacheImages
        {
            get => _cacheImages;
            set
            {
                if (_cacheImages == value) return;
                bool oldValue = _cacheImages;
                _cacheImages = value;
                ConfigManager.SettingsManager.SaveSetting(SETTINGS_GROUP, "CacheImages", value);
                OnPropertyChanged(nameof(CacheImages), oldValue, value, true);
            }
        }

        /// <summary>
        /// if true, the Bindings panel uses the classic Helios layout (Input and Output tabs, no grouping or sorting);
        /// if false, the current layout (Input and Output together, grouped and sorted)
        /// </summary>
        public bool ClassicBindingsLayout
        {
            get => _classicBindingsLayout;
            set
            {
                if (_classicBindingsLayout == value) return;
                bool oldValue = _classicBindingsLayout;
                _classicBindingsLayout = value;
                ConfigManager.SettingsManager.SaveSetting(SETTINGS_GROUP, "ClassicBindingsLayout", value);
                OnPropertyChanged(nameof(ClassicBindingsLayout), oldValue, value, true);
                ClassicBindingsLayoutChanged?.Invoke(this, System.EventArgs.Empty);
            }
        }

        #endregion

        #region Static Client Interface

        /// <summary>
        /// accessible as utility for client code that can't get access to the GlobalOptions instance readily
        /// </summary>
        /// <returns>
        /// true if newly created bindings are configured to cascade triggers
        /// </returns>
        public static bool HasDefaultCascadeTriggers =>
            ConfigManager.SettingsManager.LoadSetting(SETTINGS_GROUP, "DefaultCascadeTriggers", false);

        /// <summary>
        /// accessible as utility for client code that can't get access to the GlobalOptions instance readily
        /// </summary>
        /// <returns>
        /// true if all monitors are set to always display on top in newly
        /// created profiles
        /// </returns>
        public static bool HasDefaultAlwaysOnTop =>
            ConfigManager.SettingsManager.LoadSetting(SETTINGS_GROUP, "DefaultAlwaysOnTop", true);

        /// <summary>
        /// accessible as utility for client code that can't get access to the GlobalOptions instance readily
        /// </summary>
        /// <returns>
        /// true if monitors other than the main monitor are filled with a background
        /// color in newly created profiles
        /// </returns>
        public static bool HasDefaultFillSecondaryMonitors =>
            ConfigManager.SettingsManager.LoadSetting(SETTINGS_GROUP, "DefaultFillSecondaryMonitors", false);

        /// <summary>
        /// accessible as utility for client code that can't get access to the GlobalOptions instance readily
        /// </summary>
        /// <returns>
        /// true if image sources and similar objects should be reused by ImageManager
        /// </returns>
        public static bool HasCacheImages=>
            ConfigManager.SettingsManager.LoadSetting(SETTINGS_GROUP, "CacheImages", true);

        /// <summary>
        /// accessible as utility for client code that can't get access to the GlobalOptions instance readily
        /// </summary>
        /// <returns>
        /// true if the Bindings panel should use the classic layout with separate Input and Output tabs
        /// </returns>
        public static bool HasClassicBindingsLayout =>
            ConfigManager.SettingsManager.LoadSetting(SETTINGS_GROUP, "ClassicBindingsLayout", false);

        #endregion
    }
}