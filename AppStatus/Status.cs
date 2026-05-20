namespace FridgeManagement.AppStatus
{
    public enum GenderType
    {
        Male,
        Female
    }

    public enum UserRole
    {
        ADMINISTRATOR,
        CUSTOMERLIAISON,
        INVENTORYLIAISON,
        CUSTOMER,
        FAULTTECHNICIAN,
        MAINTENANCETECHNICIAN,
        PURCHASINGMANAGER,
        SUPPLIER

    }


    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        FaultAssigned,
        FaultScheduled,
        FaultResolved,
        MaintenanceScheduled,
        MaintenanceCompleted,
        PurchaseRequestApproved,
        PurchaseRequestRejected,
        FridgeAllocated,
        FridgeRequested,
        QuotationReceived,
        OrderPlaced,
        DeliveryScheduled
    }


    public enum Status
    {
        Active,
        Inactive
    }


    public enum AllocationStatus
    {
        Active,
        Returned,
        Cancelled
    }



    public enum PurchaseRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Ordered
    }

    public enum FaultPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum FaultStatus
    {
        Reported,
        Assigned,
        Scheduled,
        InProgress,
        Repaired,
        Closed,
        Cancelled
    }

    public enum FridgeRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Fulfilled,
        Cancelled
    }


    public enum QuotationStatus
    {
        Received,
        Accepted,
        Rejected
    }


    public enum PurchaseOrderStatus
    {
        Ordered,
        Shipped,
        Delivered,
        Cancelled
    }


    public enum RFQStatus
    {
        Draft,
        Sent,
        Closed,
        Cancelled
    }





    public enum MaintenanceStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled
    }

}
