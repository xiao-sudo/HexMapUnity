namespace HexMap.Runtime
{
    public interface IHexPathPolicy
    {
        bool CanPass(HexCell cell);
        bool CanEnter(HexCell cell);
    }
}
