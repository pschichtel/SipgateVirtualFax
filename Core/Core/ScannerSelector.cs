using System;
using NTwain;

namespace SipgateVirtualFax.Core
{
    public class ScannerSelector
    {
        public string Name { get; }
        public Func<IDataSource, bool> Selector { get; }

        public ScannerSelector(string name, Func<IDataSource, bool> selector)
        {
            Name = name;
            Selector = selector;
        }

        protected bool Equals(ScannerSelector other)
        {
            return Name == other.Name && Selector.Equals(other.Selector);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((ScannerSelector) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode() * 397) ^ Selector.GetHashCode();
            }
        }
    }
}