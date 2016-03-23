using System;

namespace HolographicStudio.Tweakables
{
    /// <summary>
    /// Generic base class for numeric tweakables
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class NumericTweakable<T> : Tweakable where T : IComparable<T>
    {
        protected PropertyInvoker<T> _property;
        protected T _minimum;
        protected T _maximum;
        protected T _step;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">The descriptive name of the value</param>
        /// <param name="property">The property to manipulate</param>
        /// <param name="min">The minimum value</param>
        /// <param name="max">The maximum value</param>
        /// <param name="step">The step for increaseing/decreasing</param>
        public NumericTweakable(string name, PropertyInvoker<T> property, T min, T max, T step) :
            base(name)
        {
            _property = property;
            _minimum = min;
            _maximum = max;
            _step = step;
        }

        protected abstract T Increase();
        protected abstract T Decrease();

        /// <summary>
        /// Decrease the value
        /// </summary>
        public override void Down()
        {
            T val = Decrease();
            if (val.CompareTo(_minimum) < 0)
            {
                val = _minimum;
            }
            _property.SetProperty(val);
        }

        /// <summary>
        /// Increases the value
        /// </summary>
        public override void Up()
        {
            T val = Increase();
            if (val.CompareTo(_maximum) > 0)
            {
                val = _maximum;
            }
            _property.SetProperty(val);
        }

        /// <summary>
        /// Retrieves value as string
        /// </summary>
        /// <returns></returns>
        public override string ValueAsString()
        {
            return _property.GetProperty().ToString();
        }
    }
}
