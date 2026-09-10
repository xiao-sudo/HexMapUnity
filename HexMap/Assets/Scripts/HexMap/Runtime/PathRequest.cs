using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HexMap.Runtime
{
    public sealed class PathRequest
    {
        private readonly HexCell m_Start;
        private readonly IReadOnlyList<HexCell> m_Targets;
        private readonly IHexPathPolicy m_Policy;

        public PathRequest(
            HexCell start,
            IReadOnlyList<HexCell> targets,
            IHexPathPolicy policy)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            m_Start = start;
            m_Targets = new ReadOnlyCollection<HexCell>(new List<HexCell>(targets));
            m_Policy = policy;
        }

        public HexCell Start
        {
            get { return m_Start; }
        }

        public IReadOnlyList<HexCell> Targets
        {
            get { return m_Targets; }
        }

        public IHexPathPolicy Policy
        {
            get { return m_Policy; }
        }
    }

    public sealed class ReusablePathRequest
    {
        private HexCell m_Start;
        private IReadOnlyList<HexCell> m_Targets;
        private IHexPathPolicy m_Policy;

        public ReusablePathRequest(
            HexCell start,
            IReadOnlyList<HexCell> targets,
            IHexPathPolicy policy)
        {
            Start = start;
            Targets = targets;
            Policy = policy;
        }

        public HexCell Start
        {
            get { return m_Start; }
            set { m_Start = value; }
        }

        public IReadOnlyList<HexCell> Targets
        {
            get { return m_Targets; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                m_Targets = value;
            }
        }

        public IHexPathPolicy Policy
        {
            get { return m_Policy; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                m_Policy = value;
            }
        }
    }
}
