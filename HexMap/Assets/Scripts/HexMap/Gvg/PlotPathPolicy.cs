using System;
using HexMap.Runtime;

namespace HexMap.Gvg
{
    public interface ICampFactionResolver
    {
        /// <summary>
        /// Resolves a camp to the faction id that currently owns it.
        /// Returns false when the camp has no known faction; factionId is then
        /// <see cref="Plot.NoFactionId"/> (neutral).
        /// </summary>
        bool TryGetFaction(int campId, out int factionId);
    }

    public sealed class PlotPathPolicy : IHexPathPolicy
    {
        private readonly PlotRegistry m_Registry;
        private readonly ICampFactionResolver m_CampFactionResolver;
        private int m_MovingFactionId;

        public PlotPathPolicy(PlotRegistry registry, int movingFactionId)
            : this(registry, NoCampFactionResolver.Instance, movingFactionId)
        {
        }

        public PlotPathPolicy(
            PlotRegistry registry,
            ICampFactionResolver campFactionResolver,
            int movingFactionId)
        {
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            m_CampFactionResolver = campFactionResolver ?? throw new ArgumentNullException(nameof(campFactionResolver));
            m_MovingFactionId = movingFactionId;
        }

        public int MovingFactionId { get { return m_MovingFactionId; } }

        /// <summary>
        /// Updates the moving faction on a reused policy instance. The registry and
        /// camp resolver never change between searches; only the faction id varies.
        /// </summary>
        public void SetMovingFaction(int movingFactionId)
        {
            m_MovingFactionId = movingFactionId;
        }

        public bool CanPass(HexCell cell)
        {
            Plot plot;
            if (!m_Registry.TryGetPlot(cell, out plot)) return false;
            return plot.IsOpenForPathfinding &&
                plot.BlockingState == BlockingState.Passable &&
                plot.OwnerFactionId == m_MovingFactionId &&
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

            int affiliatedFactionId;
            return m_CampFactionResolver.TryGetFaction(plot.AffiliatedCampId, out affiliatedFactionId) &&
                affiliatedFactionId == m_MovingFactionId;
        }

        private sealed class NoCampFactionResolver : ICampFactionResolver
        {
            public static readonly NoCampFactionResolver Instance = new NoCampFactionResolver();

            public bool TryGetFaction(int campId, out int factionId)
            {
                factionId = Plot.NoFactionId;
                return false;
            }
        }
    }
}
