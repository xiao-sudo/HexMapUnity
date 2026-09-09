using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HexMap.Runtime
{
    public sealed class PathRequest
    {
        public PathRequest(
            HexCell start,
            IReadOnlyList<HexCell> targets,
            object context,
            Func<HexCell, object, bool> canPass,
            Func<HexCell, object, bool> canEnter)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (canPass == null)
            {
                throw new ArgumentNullException(nameof(canPass));
            }

            if (canEnter == null)
            {
                throw new ArgumentNullException(nameof(canEnter));
            }

            Start = start;
            Targets = new ReadOnlyCollection<HexCell>(new List<HexCell>(targets));
            Context = context;
            CanPass = canPass;
            CanEnter = canEnter;
        }

        public HexCell Start { get; }
        public IReadOnlyList<HexCell> Targets { get; }
        public object Context { get; }
        public Func<HexCell, object, bool> CanPass { get; }
        public Func<HexCell, object, bool> CanEnter { get; }
    }
}