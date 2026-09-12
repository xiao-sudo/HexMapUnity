using System;
using HexMap.Runtime;

namespace HexMap.Gvg
{
    public interface ICampFactionResolver
    {
        bool TryGetFaction(int campId, out FactionId factionId);
    }

    public sealed class PlotPathPolicy : IHexPathPolicy
    {
        private readonly PlotRegistry m_Registry;
        private readonly ICampFactionResolver m_CampFactionResolver;
        private readonly FactionId m_MovingFaction;

        public PlotPathPolicy(PlotRegistry registry, FactionId movingFaction)
            : this(registry, NoCampFactionResolver.Instance, movingFaction)
        {
        }

        public PlotPathPolicy(
            PlotRegistry registry,
            ICampFactionResolver campFactionResolver,
            FactionId movingFaction)
        {
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            m_CampFactionResolver = campFactionResolver ?? throw new ArgumentNullException(nameof(campFactionResolver));
            m_MovingFaction = movingFaction;
        }

        public FactionId MovingFaction { get { return m_MovingFaction; } }

        public bool CanPass(HexCell cell)
        {
            Plot plot;
            if (!m_Registry.TryGetPlot(cell, out plot)) return false;
            return plot.IsOpenForPathfinding &&
                plot.BlockingState == BlockingState.Passable &&
                plot.OwnerFaction == m_MovingFaction &&
                IsAffiliatedFactionAllowed(plot);
        }

        public bool CanEnter(HexCell cell)
        {
            Plot plot;
            if (!m_Registry.TryGetPlot(cell, out plot)) return false;
            if (!plot.IsOpenForPathfinding || plot.BlockingState != BlockingState.Passable) return false;
            return IsAffiliatedFactionAllowed(plot);
        }

        private bool IsAffiliatedFactionAllowed(Plot plot)
        {
            if (plot.AffiliatedCampId == Plot.NoAffiliatedCampId) return true;

            FactionId affiliatedFaction;
            return m_CampFactionResolver.TryGetFaction(plot.AffiliatedCampId, out affiliatedFaction) &&
                affiliatedFaction == m_MovingFaction;
        }

        private sealed class NoCampFactionResolver : ICampFactionResolver
        {
            public static readonly NoCampFactionResolver Instance = new NoCampFactionResolver();

            public bool TryGetFaction(int campId, out FactionId factionId)
            {
                factionId = FactionId.Neutral;
                return false;
            }
        }
    }
}