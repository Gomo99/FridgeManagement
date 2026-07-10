namespace FridgeManagement.AppStatus
{
    public enum FaultStatus
    {
        Reported,
        Assigned,
        Scheduled,
        Travelling,
        OnSite,
        Diagnosing,
        WaitingForParts,
        Repairing,
        Testing,
        Repaired,
        CustomerConfirmed,
        Closed,
        Cancelled
    }
}