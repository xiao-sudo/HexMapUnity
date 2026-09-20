namespace HexMap.UnityRuntime
{
    internal interface IHexRenderTarget
    {
        bool IsValid { get; }

        void Apply(HexAppearance appearance);

        void Invalidate();
    }
}