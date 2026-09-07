using HexMap.Runtime;

namespace HexMap.UnityRuntime
{
    public enum HexPickStatus
    {
        Found = 0,
        NoMap = 1,
        NoCamera = 2,
        NoHit = 3,
        NonMapHit = 4,
        OutsideMap = 5,
        Missing = 6
    }

    public readonly struct HexPickResult
    {
        private HexPickResult(HexPickStatus status, HexCell cell)
        {
            Status = status;
            Cell = cell;
        }

        public HexPickStatus Status { get; }
        public HexCell Cell { get; }
        public bool HasCell
        {
            get { return Status == HexPickStatus.Found; }
        }

        public static HexPickResult Create(HexPickStatus status)
        {
            return new HexPickResult(status, default(HexCell));
        }

        public static HexPickResult Found(HexCell cell)
        {
            return new HexPickResult(HexPickStatus.Found, cell);
        }
    }
}