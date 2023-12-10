using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background.Abstractions.Models;

public sealed record BackgroundTaskModel(string Name, int Number, BackgroundTaskSettings Settings);
