namespace UMLForge.BuildingBlocks.Jobs;

/// <summary>Kết quả 1 lần chạy job. <c>Trigger</c>: "startup" · "schedule" · "manual".</summary>
public sealed record JobRunResult(
    string JobName,
    bool Succeeded,
    string Summary,
    long DurationMs,
    DateTime FinishedAt,
    string Trigger);

/// <summary>Thông tin job để màn hình Admin (UI-11) liệt kê.</summary>
public sealed record JobInfo(string JobName, TimeSpan Interval, JobRunResult? LastResult);
