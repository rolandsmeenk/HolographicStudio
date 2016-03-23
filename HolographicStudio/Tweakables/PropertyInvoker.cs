using System;
using System.Reflection;

namespace HolographicStudio.Tweakables
{
    /// <summary>
    /// Used as a wrapper class for passing a property
    /// </summary>
    /// <typeparam name="T">The type of the property return value</typeparam>
    public class PropertyInvoker<T>
    {
        private PropertyInfo propInfo;
        private object obj;

        /// <summary>
        /// Construct an property Invoker
        /// </summary>
        /// <param name="PropertyName">the property of an object we want to access</param>
        /// <param name="o">the object</param>
        public PropertyInvoker(string PropertyName, object o)
        {
            // set the instance object
            this.obj = o;
#if DESKTOP
            // get the property information and check weather the type matches T
            this.propInfo = o.GetType().GetProperty(PropertyName, typeof(T));
#else
            this.propInfo = o.GetType().GetTypeInfo().GetDeclaredProperty(PropertyName);

#endif
            
            if (propInfo == null)
            {
                throw new NullReferenceException();
            }
        }

        /// <summary>
        /// Wrapping the get and set into a property
        /// </summary>
        public T Property
        {
            get
            {
                return (T)propInfo.GetValue(obj, null);
            }
            set
            {
                propInfo.SetValue(obj, value, null);
            }
        }

        /// <summary>
        /// Wrapping the get function
        /// </summary>
        /// <returns></returns>
        public T GetProperty()
        {
            return this.Property;
        }

        /// <summary>
        /// Wrapping the set function
        /// </summary>
        /// <param name="value"></param>
        public void SetProperty(T value)
        {
            this.Property = value;
        }
    }
}
