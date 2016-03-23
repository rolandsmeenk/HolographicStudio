using System;
using System.Globalization;

namespace HolographicStudio.Tweakables
{
    public class IntTweakable : NumericTweakable<int>
    {
        public IntTweakable(string name, PropertyInvoker<int> property, int min, int max, int step) :
            base(name, property, min, max, step)
        {
        }

        protected override int Increase()
        {
            return _property.GetProperty() + _step;
        }

        protected override int Decrease()
        {
            return _property.GetProperty() - _step;
        }

        public override void SetValueAsString(string value)
        {
            try
            {
                int intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                _property.SetProperty(intValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not convert value for setting '{0}'", Name);
            }
        }
    }
}
