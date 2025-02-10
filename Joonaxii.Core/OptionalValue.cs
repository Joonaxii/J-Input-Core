namespace Joonaxii
{
    [System.Serializable]
    public struct OptionalValue<T>
    {
        public bool enabled;
        public T value;

        public void Clear()
        {
            enabled = false;
            value = default;
        }
    }

    [System.Serializable]
    public struct SelectionValue<T, U>
    {
        public bool useRhs;
        public T lhs;
        public U rhs;
    }
}
