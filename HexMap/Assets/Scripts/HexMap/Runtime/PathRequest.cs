using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HexMap.Runtime
{
    public sealed class PathRequest
    {
        private readonly IReadOnlyList<HexCell> m_Starts;
        private readonly IReadOnlyList<HexCell> m_Targets;
        private readonly IHexPathPolicy m_Policy;

        public PathRequest(HexCell start, IReadOnlyList<HexCell> targets, IHexPathPolicy policy)
            : this(new[] { start }, targets, policy) { }

        public PathRequest(IReadOnlyList<HexCell> starts, IReadOnlyList<HexCell> targets, IHexPathPolicy policy)
        {
            if (starts == null) throw new ArgumentNullException(nameof(starts));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            m_Starts = new ReadOnlyCollection<HexCell>(new List<HexCell>(starts));
            m_Targets = new ReadOnlyCollection<HexCell>(new List<HexCell>(targets));
            m_Policy = policy;
        }

        public HexCell Start { get { return m_Starts.Count == 0 ? default(HexCell) : m_Starts[0]; } }
        public IReadOnlyList<HexCell> Starts { get { return m_Starts; } }
        public IReadOnlyList<HexCell> Targets { get { return m_Targets; } }
        public IHexPathPolicy Policy { get { return m_Policy; } }
    }

    public sealed class ReusablePathRequest
    {
        private readonly List<HexCell> m_Starts;
        private readonly List<HexCell> m_Targets;
        private IHexPathPolicy m_Policy;

        public ReusablePathRequest(HexCell start, int targetCapacity, IHexPathPolicy policy)
            : this(1, targetCapacity, policy) { TryAddStart(start); }

        public ReusablePathRequest(HexCell start, List<HexCell> targetBuffer, IHexPathPolicy policy)
            : this(new List<HexCell>(1), targetBuffer, policy) { TryAddStart(start); }

        public ReusablePathRequest(int startCapacity, int targetCapacity, IHexPathPolicy policy)
            : this(new List<HexCell>(ValidateCapacity(startCapacity, nameof(startCapacity))),
                new List<HexCell>(ValidateCapacity(targetCapacity, nameof(targetCapacity))), policy) { }

        public ReusablePathRequest(IReadOnlyList<HexCell> starts, int targetCapacity, IHexPathPolicy policy)
            : this(new List<HexCell>(starts ?? throw new ArgumentNullException(nameof(starts))),
                new List<HexCell>(ValidateCapacity(targetCapacity, nameof(targetCapacity))), policy) { }

        public ReusablePathRequest(List<HexCell> startBuffer, List<HexCell> targetBuffer, IHexPathPolicy policy)
        {
            if (startBuffer == null) throw new ArgumentNullException(nameof(startBuffer));
            if (targetBuffer == null) throw new ArgumentNullException(nameof(targetBuffer));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            m_Starts = startBuffer;
            m_Targets = targetBuffer;
            m_Starts.Clear();
            m_Targets.Clear();
            m_Policy = policy;
        }

        public HexCell Start
        {
            get { return m_Starts.Count == 0 ? default(HexCell) : m_Starts[0]; }
            set
            {
                if (m_Starts.Count == 0 && !TryAddStart(value))
                    throw new InvalidOperationException("The start buffer has no remaining capacity.");
                if (m_Starts.Count > 1 || m_Starts[0].Id != value.Id || m_Starts[0].Coordinate != value.Coordinate)
                    m_Starts[0] = value;
            }
        }

        public IReadOnlyList<HexCell> Starts { get { return m_Starts; } }
        public int StartCount { get { return m_Starts.Count; } }
        public int StartCapacity { get { return m_Starts.Capacity; } }
        public IReadOnlyList<HexCell> Targets { get { return m_Targets; } }
        public int TargetCount { get { return m_Targets.Count; } }
        public int TargetCapacity { get { return m_Targets.Capacity; } }

        public IHexPathPolicy Policy
        {
            get { return m_Policy; }
            set { if (value == null) throw new ArgumentNullException(nameof(value)); m_Policy = value; }
        }

        public void ClearStarts() { m_Starts.Clear(); }
        public bool TryAddStart(HexCell start)
        {
            if (m_Starts.Count >= m_Starts.Capacity) return false;
            m_Starts.Add(start);
            return true;
        }

        public void ClearTargets() { m_Targets.Clear(); }
        public bool TryAddTarget(HexCell target)
        {
            if (m_Targets.Count >= m_Targets.Capacity) return false;
            m_Targets.Add(target);
            return true;
        }

        private static int ValidateCapacity(int capacity, string parameterName)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(parameterName);
            return capacity;
        }
    }
}
