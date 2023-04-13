using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Models;

public sealed record NetSharedBackgroundTaskInfo(string Name, int Number, BackgroundTaskSettings Settings);
