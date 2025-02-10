namespace Joonaxii.Collections
{
    public interface IRefComparable<T>
    {
        int CompareTo(in T other);
    }
}
