using Microsoft.AspNetCore.Http;

namespace Services;


public static class RefHelper
{
    private const string RefCookieName = "ref_code";

    public static string GetCurrentRefFromRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Ref", out var headerRef))
            return headerRef.ToString();

        if (request.Cookies.TryGetValue(RefCookieName, out var cookieRef))
            return cookieRef;
        return null;
    }
}