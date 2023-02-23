namespace Net.Shared.Background.Models;

public static class Constants
{
    public static class BackgroundTaskActions
    {
        public const string StartGettingProcessableData = "Start getting processable data.";
        public const string StartGettingUnprocessableData = "Start getting unprocessable data.";
        public const string StopGettingData = "Stop getting data.";
        
        public const string StartHandlingData = "Start handling data.";
        public const string StopHandlingData = "Stop handling data.";
        
        public const string StartSavingData = "Start saving data.";
        public const string StopSavingData = "Stop saving data.";
    }
}