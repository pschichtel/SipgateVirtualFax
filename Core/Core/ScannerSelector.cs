using System;
using System.Linq;
using NTwain;

namespace SipgateVirtualFax.Core;

public class ScannerSelector
{
    public string Name { get; }
    public Func<ITwainSession, IDataSource?> Selector { get; }
        
    public ScannerSelector(string name, Func<ITwainSession, IDataSource?> selector)
    {
        Name = name;
        Selector = selector;
    }

    public static ScannerSelector SelectBy<T>(IDataSource source, Func<IDataSource, T> f) where T : notnull
    {
        var id = f(source);
        IDataSource? SelectSource(ITwainSession session)
        {
            return session.GetSources().First(s => f(s).Equals(id));
        }
        return new ScannerSelector(source.Name, SelectSource);
    }

    public static ScannerSelector SelectById(IDataSource source)
    {
        return SelectBy(source, s => s.Id);
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