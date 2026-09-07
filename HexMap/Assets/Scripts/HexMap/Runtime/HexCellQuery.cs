using HexMap.Core;

namespace HexMap.Runtime
{
    public enum HexCellQueryStatus
    {
        Found = 0,
        OutsideMap = 1,
        Missing = 2
    }

    public readonly struct HexCellQuery
    {
        private HexCellQuery(HexCellQueryStatus status, HexCell cell)
        {
            Status = status;
            Cell = cell;
        }

        public HexCellQueryStatus Status { get; }
        public HexCell Cell { get; }
        public bool HasCell
        {
            get { return Status == HexCellQueryStatus.Found; }
        }

        public static HexCellQuery Found(HexCell cell)
        {
            return new HexCellQuery(HexCellQueryStatus.Found, cell);
        }

        public static HexCellQuery OutsideMap()
        {
            return new HexCellQuery(HexCellQueryStatus.OutsideMap, default(HexCell));
        }

        public static HexCellQuery Missing()
        {
            return new HexCellQuery(HexCellQueryStatus.Missing, default(HexCell));
        }
    }
}