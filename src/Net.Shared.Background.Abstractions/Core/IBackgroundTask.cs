﻿using Net.Shared.Background.Models;

namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundTask
{
    NetSharedBackgroundTaskInfo TaskInfo { get; }
    Task Run(NetSharedBackgroundTaskInfo info, CancellationToken cToken = default);
}