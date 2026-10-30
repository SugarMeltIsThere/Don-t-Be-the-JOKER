using Unity.Profiling;

public static class PerfMarkers
{
    public const string UIRefreshName = "DBtJ.UIManager.Refresh";
    public const string TableSyncName = "DBtJ.TableView.Sync";

    public static readonly ProfilerMarker UIRefresh = new ProfilerMarker(ProfilerCategory.Scripts, UIRefreshName);
    public static readonly ProfilerMarker TableSync = new ProfilerMarker(ProfilerCategory.Scripts, TableSyncName);
}
