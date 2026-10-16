//  Copyright 2014 Craig Courtney
//  Copyright 2020 Ammo Goettsch
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
    using CommandLine;
    using CommandLine.Text;
    using System.Collections.Generic;

    class CommandLineOptions : Util.CommonCommandLineOptions
    {
        [Option('g', "generateInterfaceJson")]
        public bool GenerateInterfaceJson { get; set; }

        [Option('s', "generateInterfaceSchema")]
        public bool GenerateInterfaceSchema { get; set; }

        [Value(0, MetaName = "[Profile]", HelpText = "Filename & path of the Profile or Helio16 package to open on start")]
        public IEnumerable<string> Profiles
        {
            get;
            set;
        } = new List<string>();
        [Usage(ApplicationAlias = "\"Developer Edition.exe\"")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Opening a Helios16 installer package at startup", UnParserSettings.WithGroupSwitchesOnly(), new CommandLineOptions() { Profiles = new string[] { @"c:\users\public\downloads\Wibble Profile.helios16" } });
                yield return new Example("Opening a Profile at startup from the active Helios Profiles directory", new CommandLineOptions() { Profiles = new string[] { @"Wobble.hpf" } });
                yield return new Example("Debug logging enabled", new CommandLineOptions() { LogLevel = LogLevel.Debug });
                yield return new Example("Using an alternate Helios data directory", new CommandLineOptions() { DocumentsName = "HeliosVR" });
            }
        }

    }
}
