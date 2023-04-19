using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Models;

public sealed record BackgroundTaskInfo(string Name, int Number, BackgroundTaskSettings Settings);
