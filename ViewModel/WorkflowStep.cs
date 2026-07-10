using FridgeManagement.AppStatus;

namespace FridgeManagement.ViewModel
{
    public class WorkflowStep
    {
        public FaultStatus Status { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
