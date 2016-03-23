namespace HolographicStudio.Tweakables
{
    /// <summary>
    /// Abstract base class for values that can be manipulated through arrow keys
    /// </summary>
    public abstract class Tweakable
    {
        public string Name
        {
            get;
            set;
        }

        public bool ReadOnly
        {
            get;
            private set;
        }

        public abstract string ValueAsString();
        public abstract void SetValueAsString(string value);

        public Tweakable(string name, bool readOnly = false)
        {
            Name = name;
            ReadOnly = readOnly;
        }

        public abstract void Down();
        public abstract void Up();

        public bool Next()
        {
            return false;
        }

        public bool Previous()
        {
            return false;
        }
    }

}
