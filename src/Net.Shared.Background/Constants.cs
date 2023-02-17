namespace Net.Shared.Background;

internal static class Constants
{
    internal static class Actions
    {
        internal const string StartGettingProcessableData = "Start getting processable data";
        internal const string StartGettingUnprocessableData = "Start getting processable data";
        internal const string StopGettingData = "Stop getting data";

        internal const string StartHandlingData = "Start handling data";
        internal const string StopHandlingData = "Stop handling data";

        internal const string StartSavingData = "Start saving data";
        internal const string StopSavingData = "Stop saving data";
    }
}