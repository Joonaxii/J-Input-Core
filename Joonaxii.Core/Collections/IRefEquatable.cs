namespace Joonaxii.Collections
{
    public interface IRefEquatable<T>
    {
        bool Equals(in T other);
    }
}