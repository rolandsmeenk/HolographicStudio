using System;
using System.Globalization;

namespace HolographicStudio.Tweakables
{
    public class FloatTweakable : NumericTweakable<float>
    {
        public FloatTweakable(string name, PropertyInvoker<float> property, float min, float max, float step) :
            base(name, property, min, max, step)
        {
        }

        protected override float Increase()
        {
            return _property.GetProperty() + _step;
        }

        protected override float Decrease()
        {
            return _property.GetProperty() - _step;
        }

        public override string ValueAsString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0:0.###}", _property.GetProperty()); 
        }

        public override void SetValueAsString(string value)
        {
            try
            {
                float floatValue = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                _property.SetProperty(floatValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not convert value for setting '{0}'", Name);
            }
        }
    }
}
