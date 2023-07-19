using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Models;

public sealed record BackgroundTaskModel(string Name, int Number, BackgroundTaskSettings Settings);
