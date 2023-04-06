namespace Net.Shared.Background.Models;

public static class Constants
{
    public static class BackgroundTaskActions
    {
        public static string StartGettingProcessableData(string taskName, string step) => $"Start getting processable data for the task: {taskName} by step: {step} by step: {step}.";
        public static string StartGettingUnprocessableData(string taskName, string step) => $"Start getting unprocessable data for the task: {taskName} by step: {step} by step: {step}.";
        public static string StopGettingData(string taskName, string step) => $"Stop getting data for the task: {taskName} by step: {step}.";

        public static string StartHandlingData(string taskName, string step) => $"Start handling data for the task: {taskName} by step: {step}.";
        public static string StopHandlingData(string taskName, string step) => $"Stop handling data for the task: {taskName} by step: {step}.";

        public static string StartSavingData(string taskName, string step) => $"Start saving data for the task: {taskName} by step: {step}.";
        public static string StopSavingData(string taskName, string step) => $"Stop saving data for the task: {taskName} by step: {step}.";
    }
}