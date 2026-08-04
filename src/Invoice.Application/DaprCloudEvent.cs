namespace Invoice.Application;

public sealed record DaprCloudEvent<T>(
    string Id,
    string Source,
    string SpecVersion,
    string Type,
    string? TraceParent,
    T Data);
