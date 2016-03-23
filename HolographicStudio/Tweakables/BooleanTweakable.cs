using System;
using System.Globalization;

namespace HolographicStudio.Tweakables
{
    /// <summary>
    /// Tweaks a boolean property
    /// </summary>
    public class BooleanTweakable : Tweakable
    {
        PropertyInvoker<bool> _property;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="property">The boolean property to manipulate</param>
        public BooleanTweakable(string name, PropertyInvoker<bool> property) :
            base(name)
        {
            _property = property;
        }

        public override void Down()
        {
            _property.SetProperty(!_property.GetProperty());
        }

        public override void Up()
        {
            _property.SetProperty(!_property.GetProperty());
        }

        public override string ValueAsString()
        {
            return _property.GetProperty().ToString();
        }

        public override void SetValueAsString(string value)
        {
            try
            {
                bool boolValue = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                _property.SetProperty(boolValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not convert value for setting '{0}'", Name);
            }            
        }
    }
}
