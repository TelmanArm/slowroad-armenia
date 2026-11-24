using System.Text.RegularExpressions;

namespace SlowRoad.Tests;

// Posts the admin login form the way a browser does: fetch the form, carry its
// antiforgery token and cookie back in.
internal static class LoginForm
{
    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string email, string password, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/Admin/Login" : $"/Admin/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var form = await client.GetAsync(url);
        form.EnsureSuccessStatusCode();

        var html = await form.Content.ReadAsStringAsync();
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "no antiforgery token on the login form");

        var fields = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        };
        if (returnUrl is not null)
            fields["ReturnUrl"] = returnUrl;

        var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/Login")
        {
            Content = new FormUrlEncodedContent(fields),
        };
        foreach (var cookie in Cookies(form))
            request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request);
    }

    // name=value pairs from Set-Cookie, dropping the attributes.
    private static IEnumerable<string> Cookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(v => v.Split(';')[0])
            : Enumerable.Empty<string>();
}
