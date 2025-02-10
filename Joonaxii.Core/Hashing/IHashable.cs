namespace Joonaxii.Hashing
{
    public interface IHashable<C>
    {
        ref C UpdateHash(ref C state);
    }
}
