using System.Diagnostics;

namespace Svl.AI.ObservationalMemory;

public static class ActivityExtensions
{
    public static void SetException(this Activity? activity, Exception exception)
    {
        if (activity == null) return;
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddEvent(new ActivityEvent(
            "exception",
            tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.ToString() }
            }));
    }
}
