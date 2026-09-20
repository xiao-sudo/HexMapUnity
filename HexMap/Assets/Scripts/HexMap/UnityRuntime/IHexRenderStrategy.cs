using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime
{
    internal interface IHexRenderStrategy : System.IDisposable
    {
        IReadOnlyDictionary<HexCoord, IHexRenderTarget> Build(
            RuntimeHexMap map,
            HexLayout layout,
            HexMapRenderConfig config,
            int generation);

        void Activate();

        void Render();
    }
}