using Unity.Profiling;

public static class PerfMarkers
{
    public const string UIRefreshName = "DBtJ.UIManager.Refresh";

    public static readonly ProfilerMarker UIRefresh = new ProfilerMarker(ProfilerCategory.Scripts, UIRefreshName);
}
