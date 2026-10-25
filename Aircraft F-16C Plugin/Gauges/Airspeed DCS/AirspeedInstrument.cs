//  Copyright 2026 Helios Contributors
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

namespace GadrocsWorkshop.Helios.Gauges.F16C.AirspeedDCS
{
    using GadrocsWorkshop.Helios.ComponentModel;
    using GadrocsWorkshop.Helios.Controls;
    using System.Collections.Generic;
    using System.Windows;

    /// <summary>
    /// DCS F-16C "Airspeed Instrument": bezel + "Airspeed Indicators" + "Airspeed Set Index Knob".
    ///
    /// Binding device / name                         DCS F-16C interface (device "Airspeed Indicator")
    ///   Airspeed Indicators / Airspeed Indicator Needle   "Airspeed Indicator"  (arg 48)
    ///   Airspeed Indicators / Airspeed Bug                "Set Airspeed"        (arg 70)
    ///   Airspeed Indicators / VNE Needle                  "Max Airspeed"        (arg 47)
    ///   Airspeed Indicators / Mach Indicator Disc         "Mach Indicator"      (arg 49)
    ///   Airspeed Set Index Knob / value                   "SET INDEX Knob"      (arg 71)
    ///   Airspeed Set Index Knob / encoder in/decremented  increment/decrement "SET INDEX Knob" (device 46, command 3001)
    /// The airspeed disc is fixed artwork.
    /// </summary>
    [HeliosControl("Helios.F16C.AirspeedInstrument", AIRSPEED_INSTRUMENT, "F-16", typeof(BackgroundImageRenderer), HeliosControlFlags.None)]
    public class AirspeedInstrument : CompositeVisualWithBackgroundImage
    {
        public const string AIRSPEED_INSTRUMENT = "Airspeed Instrument";
        public const string AIRSPEED_INDICATORS = "Airspeed Indicators";
        public const string AIRSPEED_SET_INDEX_KNOB = "Airspeed Set Index Knob";

        internal const string IMAGE_PATH = "{F-16C}/Gauges/Airspeed DCS/";
        private const string AIRSPEED_INSTRUMENT_BEZEL_IMAGE = IMAGE_PATH + "airspeed_instrument_bezel.png";
        private const string AIRSPEED_SET_INDEX_KNOB_IMAGE = IMAGE_PATH + "airspeed_set_index_knob.png";

        // names used by the DCS F-16C interface
        private const string INTERFACE_DEVICE = "Airspeed Indicator";
        private const string INTERFACE_SET_INDEX_KNOB = "SET INDEX Knob";

        private AirspeedIndicators _airspeedIndicators;
        private RotaryEncoder _airspeedSetIndexKnob;

        public AirspeedInstrument()
            : base(AIRSPEED_INSTRUMENT, new Size(400d, 400d))
        {
            SupportedInterfaces = new[] { typeof(GadrocsWorkshop.Helios.Interfaces.DCS.F16C.F16CInterface) };

            AddAirspeedIndicators();
            AddAirspeedSetIndexKnob(new Point(312d, 314d), new Size(70d, 70d));   // lower right of the instrument
        }

        #region Construction

        private void AddAirspeedIndicators()
        {
            _airspeedIndicators = new AirspeedIndicators(AIRSPEED_INDICATORS, new Size(400d, 400d), AIRSPEED_INDICATORS)
            {
                Name = AIRSPEED_INDICATORS,
                Top = 0d,
                Left = 0d,
                Width = 400d,
                Height = 400d
            };
            Children.Add(_airspeedIndicators);

            // Airspeed Indicators input -> DCS F-16C interface value that drives it
            Dictionary<string, string> inputs = new Dictionary<string, string>
            {
                { AirspeedIndicators.AIRSPEED_INDICATOR_NEEDLE, "Airspeed Indicator" },
                { AirspeedIndicators.AIRSPEED_BUG, "Set Airspeed" },
                { AirspeedIndicators.VNE_NEEDLE, "Max Airspeed" },
                { AirspeedIndicators.MACH_INDICATOR_DISC, "Mach Indicator" }
            };

            foreach (IBindingAction action in _airspeedIndicators.Actions)
            {
                if (!inputs.ContainsKey(action.Name))
                {
                    // e.g. the base "hidden" action
                    continue;
                }

                // the key in the child's own collection is "Airspeed Indicators.set.<name>"
                string actionKey = _airspeedIndicators.Actions.GetKeyForItem(action);
                AddAction(action, AIRSPEED_INDICATORS);
                AddDefaultInputBinding(
                    childName: AIRSPEED_INDICATORS,
                    interfaceTriggerName: $"{INTERFACE_DEVICE}.{inputs[action.Name]}.changed",
                    deviceActionName: actionKey);
            }
        }

        private void AddAirspeedSetIndexKnob(Point posn, Size size)
        {
            _airspeedSetIndexKnob = new RotaryEncoder
            {
                Name = AIRSPEED_SET_INDEX_KNOB,
                KnobImage = AIRSPEED_SET_INDEX_KNOB_IMAGE,
                StepValue = 0.025d,     // DCS argument step for this knob
                RotationStep = 9d,      // 0.025 of a turn per detent
                Top = posn.Y,
                Left = posn.X,
                Width = size.Width,
                Height = size.Height,
                ClickType = RotaryClickType.Swipe
            };
            Children.Add(_airspeedSetIndexKnob);

            // outputs: encoder.incremented / encoder.decremented
            foreach (IBindingTrigger trigger in _airspeedSetIndexKnob.Triggers)
            {
                AddChildTrigger(trigger, AIRSPEED_SET_INDEX_KNOB);
            }
            // input: set.value (rotates the knob artwork, value * 360 degrees)
            AddChildAction(_airspeedSetIndexKnob.Actions["set.value"], AIRSPEED_SET_INDEX_KNOB);

            AddDefaultOutputBinding(
                childName: AIRSPEED_SET_INDEX_KNOB,
                deviceTriggerName: "encoder.incremented",
                interfaceActionName: $"{INTERFACE_DEVICE}.increment.{INTERFACE_SET_INDEX_KNOB}");
            AddDefaultOutputBinding(
                childName: AIRSPEED_SET_INDEX_KNOB,
                deviceTriggerName: "encoder.decremented",
                interfaceActionName: $"{INTERFACE_DEVICE}.decrement.{INTERFACE_SET_INDEX_KNOB}");
            AddDefaultInputBinding(
                childName: AIRSPEED_SET_INDEX_KNOB,
                interfaceTriggerName: $"{INTERFACE_DEVICE}.{INTERFACE_SET_INDEX_KNOB}.changed",
                deviceActionName: "set.value");
        }

        private void AddChildTrigger(IBindingTrigger trigger, string device)
        {
            trigger.Device = device;
            if (!Triggers.ContainsKey(Triggers.GetKeyForItem(trigger)))
            {
                Triggers.Add(trigger);
            }
        }

        private void AddChildAction(IBindingAction action, string device)
        {
            action.Device = device;
            if (!Actions.ContainsKey(Actions.GetKeyForItem(action)))
            {
                Actions.Add(action);
            }
        }

        #endregion

        public override string DefaultBackgroundImage => AIRSPEED_INSTRUMENT_BEZEL_IMAGE;

        protected override void OnBackgroundImageChange()
        {
            // the bezel is the background image itself, nothing else to adjust
        }

        // let mouse input fall through to the Airspeed Set Index Knob
        public override bool HitTest(Point location) => false;

        public override void MouseDown(Point location)
        {
            // No-Op
        }

        public override void MouseDrag(Point location)
        {
            // No-Op
        }

        public override void MouseUp(Point location)
        {
            // No-Op
        }
    }

    /// <summary>
    /// "Airspeed Indicators": the moving parts of the Airspeed Instrument.
    ///
    /// Every input is the raw DCS argument value (0.0 - 1.0) and every part turns
    /// clockwise, 0.0 = 12 o'clock, 1.0 = one full turn:
    ///   Airspeed Indicator Needle   (DCS arg 48)
    ///   Airspeed Bug                (DCS arg 70)
    ///   VNE Needle                  (DCS arg 47)
    ///   Mach Indicator Disc         (DCS arg 49)
    ///
    /// The Mach Indicator Disc is carried clockwise with the Airspeed Indicator Needle and then
    /// turned counter-clockwise from there by (1 - input) of a turn:
    ///   machIndicatorDiscRotation = airspeedIndicatorNeedleRotation - (1 - machIndicatorDisc) * 360
    /// </summary>
    [HeliosControl("Helios.F16C.AirspeedIndicators", AirspeedInstrument.AIRSPEED_INDICATORS, "F-16", typeof(GaugeRenderer), HeliosControlFlags.NotShownInUI)]
    public class AirspeedIndicators : BaseGauge
    {
        public const string AIRSPEED_INDICATOR_NEEDLE = "Airspeed Indicator Needle";
        public const string AIRSPEED_BUG = "Airspeed Bug";
        public const string VNE_NEEDLE = "VNE Needle";
        public const string MACH_INDICATOR_DISC = "Mach Indicator Disc";

        private const string IMAGE_PATH = AirspeedInstrument.IMAGE_PATH;
        private const string AIRSPEED_DISC_IMAGE = IMAGE_PATH + "airspeed_disc.png";
        private const string MACH_INDICATOR_DISC_IMAGE = IMAGE_PATH + "mach_indicator_disc.png";
        private const string VNE_NEEDLE_IMAGE = IMAGE_PATH + "vne_needle.png";
        private const string AIRSPEED_INDICATOR_NEEDLE_IMAGE = IMAGE_PATH + "airspeed_indicator_needle.png";
        private const string AIRSPEED_BUG_IMAGE = IMAGE_PATH + "airspeed_bug.png";
        private const string AIRSPEED_INSTRUMENT_GLASS_IMAGE = IMAGE_PATH + "airspeed_instrument_glass.png";

        // Canvas is 400 x 400 with the instrument centre at (200,200).
        // The 300 px airspeed disc and airspeed indicator needle artwork are drawn 328 units across
        // (scale 1.0933); the Mach Indicator Disc sits under the needle's 90 px (98.4 unit) cover disc.
        private static readonly Point CENTRE = new Point(200d, 200d);
        private const double AIRSPEED_DISC_SIZE = 328d;
        private const double MACH_INDICATOR_DISC_SIZE = 192.4d;
        private const double AIRSPEED_BUG_WIDTH = 41d * AIRSPEED_DISC_SIZE / 653d;
        private static readonly Size FULL_LAYER = new Size(400d, 400d);
        private static readonly Point FULL_LAYER_PIVOT = new Point(200d, 200d);

        private readonly GaugeNeedle _machIndicatorDisc;
        private readonly GaugeNeedle _vneNeedle;
        private readonly GaugeNeedle _airspeedIndicatorNeedle;
        private readonly GaugeNeedle _airspeedBug;

        private readonly HeliosValue _airspeedIndicatorNeedleValue;
        private readonly HeliosValue _airspeedBugValue;
        private readonly HeliosValue _vneNeedleValue;
        private readonly HeliosValue _machIndicatorDiscValue;

        public AirspeedIndicators()
            : this(AirspeedInstrument.AIRSPEED_INDICATORS, new Size(400d, 400d), "")
        {
        }

        /// <param name="device">binding device name; must equal the Name the Airspeed Instrument
        /// gives this child so its default bindings can find these actions</param>
        public AirspeedIndicators(string name, Size size, string device)
            : base(name, size)
        {
            // fixed airspeed (KNOTS x100) disc
            Components.Add(new GaugeImage(AIRSPEED_DISC_IMAGE,
                new Rect(CENTRE.X - AIRSPEED_DISC_SIZE / 2d, CENTRE.Y - AIRSPEED_DISC_SIZE / 2d, AIRSPEED_DISC_SIZE, AIRSPEED_DISC_SIZE)));

            _machIndicatorDisc = new GaugeNeedle(MACH_INDICATOR_DISC_IMAGE, CENTRE,
                new Size(MACH_INDICATOR_DISC_SIZE, MACH_INDICATOR_DISC_SIZE),
                new Point(MACH_INDICATOR_DISC_SIZE / 2d, MACH_INDICATOR_DISC_SIZE / 2d));
            Components.Add(_machIndicatorDisc);

            // VNE Needle sits under the Airspeed Indicator Needle's cover disc; only its outer end shows
            _vneNeedle = new GaugeNeedle(VNE_NEEDLE_IMAGE, CENTRE, FULL_LAYER, FULL_LAYER_PIVOT);
            Components.Add(_vneNeedle);

            // Airspeed Indicator Needle: cover disc with the Mach window and the needle, covers the Mach Indicator Disc
            _airspeedIndicatorNeedle = new GaugeNeedle(AIRSPEED_INDICATOR_NEEDLE_IMAGE, CENTRE,
                new Size(AIRSPEED_DISC_SIZE, AIRSPEED_DISC_SIZE),
                new Point(AIRSPEED_DISC_SIZE / 2d, AIRSPEED_DISC_SIZE / 2d));
            Components.Add(_airspeedIndicatorNeedle);

            // Airspeed Bug: 41 x 653 px green arrow strip, arrow at the top pointing at the centre,
            // turns about the strip's centre; scaled so the strip is 328 units long (arrow on the rim)
            _airspeedBug = new GaugeNeedle(AIRSPEED_BUG_IMAGE, CENTRE,
                new Size(AIRSPEED_BUG_WIDTH, AIRSPEED_DISC_SIZE),
                new Point(AIRSPEED_BUG_WIDTH / 2d, AIRSPEED_DISC_SIZE / 2d));
            Components.Add(_airspeedBug);

            Components.Add(new GaugeImage(AIRSPEED_INSTRUMENT_GLASS_IMAGE, new Rect(0d, 0d, 400d, 400d)));

            _airspeedIndicatorNeedleValue = AddInput(device, AIRSPEED_INDICATOR_NEEDLE, "Airspeed Indicator Needle (DCS arg 48). Turns clockwise.");
            _airspeedIndicatorNeedleValue.Execute += AirspeedIndicatorNeedle_Execute;

            _airspeedBugValue = AddInput(device, AIRSPEED_BUG, "Airspeed Bug, the set index marker (DCS arg 70). Turns clockwise.");
            _airspeedBugValue.Execute += AirspeedBug_Execute;

            _vneNeedleValue = AddInput(device, VNE_NEEDLE, "VNE Needle, max allowable airspeed (DCS arg 47). Turns clockwise.");
            _vneNeedleValue.Execute += VneNeedle_Execute;

            _machIndicatorDiscValue = AddInput(device, MACH_INDICATOR_DISC, "Mach Indicator Disc (DCS arg 49). Follows the Airspeed Indicator Needle clockwise, then turns counter-clockwise by (1 - value) of a turn.");
            _machIndicatorDiscValue.Execute += MachIndicatorDisc_Execute;
        }

        private HeliosValue AddInput(string device, string name, string description)
        {
            HeliosValue value = new HeliosValue(this, new BindingValue(0d), device, name, description,
                "0.0 to 1.0 (one full turn)", BindingValueUnits.Numeric);
            Actions.Add(value);
            return value;
        }

        private static double Turns(double value) => value * 360d;

        private void UpdateMachIndicatorDisc()
        {
            _machIndicatorDisc.Rotation = _airspeedIndicatorNeedle.Rotation - Turns(1d - _machIndicatorDiscValue.Value.DoubleValue);
        }

        private void AirspeedIndicatorNeedle_Execute(object action, HeliosActionEventArgs e)
        {
            _airspeedIndicatorNeedleValue.SetValue(e.Value, e.BypassCascadingTriggers);
            _airspeedIndicatorNeedle.Rotation = Turns(e.Value.DoubleValue);
            UpdateMachIndicatorDisc();
        }

        private void AirspeedBug_Execute(object action, HeliosActionEventArgs e)
        {
            _airspeedBugValue.SetValue(e.Value, e.BypassCascadingTriggers);
            _airspeedBug.Rotation = Turns(e.Value.DoubleValue);
        }

        private void VneNeedle_Execute(object action, HeliosActionEventArgs e)
        {
            _vneNeedleValue.SetValue(e.Value, e.BypassCascadingTriggers);
            _vneNeedle.Rotation = Turns(e.Value.DoubleValue);
        }

        private void MachIndicatorDisc_Execute(object action, HeliosActionEventArgs e)
        {
            _machIndicatorDiscValue.SetValue(e.Value, e.BypassCascadingTriggers);
            UpdateMachIndicatorDisc();
        }
    }
}
