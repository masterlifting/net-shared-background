namespace Shared.Background;

internal static class Constants
{
    internal static class Actions
    {
        internal const string Start = "Start";
        internal const string Done = "Done";
        internal const string Stop = "Stop";
        internal const string NoConfig = "Configuration was not found";
        internal const string Limit = "Size of data reached the limit for processing";
        internal const string NextStart = "Next start over: ";

        internal const string NoData = "No data";
        internal const string Success = "Success";
        internal static class ProcessableActions
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
}