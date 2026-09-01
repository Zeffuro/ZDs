using System;
using Dalamud.Plugin.Services;

namespace ZDs.Helpers;

/// <summary>Provides concise, lazily resolved access to Dalamud services.</summary>
public static class ServiceExtensions
{
    private static class ServiceInstance<T> where T : class, IDalamudService
    {
        public static T? Instance => field ??= Plugin.PluginInterface.GetService(typeof(T)) as T;
    }

    extension<T>(T) where T : class, IDalamudService
    {
        public static T Get() => ServiceInstance<T>.Instance
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} was not available.");
    }
}
