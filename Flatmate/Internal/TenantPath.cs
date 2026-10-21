using Microsoft.AspNetCore.Http;

namespace Flatmate.Internal;

internal static class TenantPath
{
    private static readonly object SegmentIndexKey = new();

    public static void Mark(HttpContext context, int segmentIndex)
        => context.Items[SegmentIndexKey] = segmentIndex;

    public static void Rewrite(HttpContext context)
    {
        if (context.Items[SegmentIndexKey] is not int segmentIndex)
        {
            return;
        }

        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var trailingSlash = path.EndsWith('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if ((uint)segmentIndex >= (uint)segments.Length)
        {
            return;
        }

        var removed = segments[segmentIndex];
        var kept = new string[segments.Length - 1];
        var target = 0;
        for (var index = 0; index < segments.Length; index++)
        {
            if (index == segmentIndex)
            {
                continue;
            }

            kept[target++] = segments[index];
        }

        if (segmentIndex == 0)
        {
            context.Request.PathBase = context.Request.PathBase.Add(new PathString("/" + removed));
        }

        if (kept.Length == 0)
        {
            context.Request.Path = new PathString("/");
            return;
        }

        var rebuilt = "/" + string.Join('/', kept);
        if (trailingSlash)
        {
            rebuilt += "/";
        }

        context.Request.Path = new PathString(rebuilt);
    }
}
