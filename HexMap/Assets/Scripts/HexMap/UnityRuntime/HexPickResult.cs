using System;
using HexMap.Runtime;

namespace HexMap.UnityRuntime
{
    public enum HexPickStatus
    {
        Found = 0,
        NoMap = 1,
        NotOnMapPlane = 2,
        OutsideMap = 3,
        Missing = 4
    }

    public readonly struct HexPickResult
    {
        private HexPickResult(HexPickStatus status, HexView view)
        {
            Status = status;
            View = view;
            Cell = view.Cell;
        }

        private HexPickResult(HexPickStatus status)
        {
            Status = status;
            View = null;
            Cell = default(HexCell);
        }

        public HexPickStatus Status { get; }
        public HexView View { get; }
        public HexCell Cell { get; }

        public bool HasCell
        {
            get { return Status == HexPickStatus.Found && View != null; }
        }

        public static HexPickResult Create(HexPickStatus status)
        {
            if (status == HexPickStatus.Found)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Found results require a HexView.");
            }

            return new HexPickResult(status);
        }

        public static HexPickResult Found(HexView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return new HexPickResult(HexPickStatus.Found, view);
        }
    }
}