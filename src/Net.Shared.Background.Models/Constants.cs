namespace Net.Shared.Background.Models;

public static class Constants
{
    public static class BackgroundTaskActions
    {
        public static string StartGettingProcessableData(string taskName) => $"Start getting processable data for the task: {taskName}.";
        public static string StartGettingUnprocessableData(string taskName) => $"Start getting unprocessable data for the task: {taskName}.";
        public static string StopGettingData(string taskName) => $"Stop getting data for the task: {taskName}.";
                 
        public static string StartHandlingData(string taskName) => $"Start handling data for the task: {taskName}.";
        public static string StopHandlingData(string taskName) => $"Stop handling data for the task: {taskName}.";
                 
        public static string StartSavingData(string taskName) => $"Start saving data for the task: {taskName}.";
        public static string StopSavingData(string taskName) => $"Stop saving data for the task: {taskName}.";
    }
}