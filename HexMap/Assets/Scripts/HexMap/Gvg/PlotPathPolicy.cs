using System;
using HexMap.Runtime;

namespace HexMap.Gvg
{
    public sealed class PlotPathPolicy : IHexPathPolicy
    {
        private readonly PlotRegistry m_Registry;
        private readonly FactionId m_MovingFaction;

        public PlotPathPolicy(PlotRegistry registry, FactionId movingFaction)
        {
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            m_MovingFaction = movingFaction;
        }

        public FactionId MovingFaction { get { return m_MovingFaction; } }

        public bool CanPass(HexCell cell)
        {
            Plot plot;
            if (!m_Registry.TryGetPlot(cell, out plot)) return false;
            return plot.IsOpenForPathfinding &&
                plot.BlockingState == BlockingState.Passable &&
                plot.OwnerFaction == m_MovingFaction;
        }

        public bool CanEnter(HexCell cell)
        {
            Plot plot;
            if (!m_Registry.TryGetPlot(cell, out plot)) return false;
            if (!plot.IsOpenForPathfinding || plot.BlockingState != BlockingState.Passable) return false;
            return plot.OwnershipMode != OwnershipMode.Fixed ||
                plot.OwnerFaction == m_MovingFaction;
        }
    }
}
