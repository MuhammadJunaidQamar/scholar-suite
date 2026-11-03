using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Scholar.Api.Endpoints;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Auth;

public sealed class AuthenticationTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Browser_login_sets_httponly_session_cookies_and_me_returns_user()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();

        var response = await browser.PostAsync("/api/auth/login", new { identifier = school.Email, password = school.Password });

        await response.EnsureSuccessWithBodyAsync();
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookies, c => c.StartsWith("ss_at=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, c => c.StartsWith("ss_rt=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                                         && c.Contains("path=/api/auth", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, c => c.StartsWith("ss_csrf=", StringComparison.Ordinal) && !c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var body = await response.ReadJsonAsync<BrowserSessionResponse>();
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"accessToken\":", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"refreshToken\":", raw, StringComparison.Ordinal);
        Assert.Equal(school.SchoolId, body.User.SchoolId);

        var me = await browser.GetJsonAsync<Scholar.Application.Access.MeDto>("/api/auth/me");
        Assert.Equal(school.UserId, me.User.Id);
        Assert.Single(me.Schools);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_get_the_same_generic_error()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();

        var wrongPassword = await browser.PostAsync("/api/auth/login", new { identifier = school.Email, password = "Nope-12345" });
        var unknownUser = await browser.PostAsync("/api/auth/login", new { identifier = "ghost@nowhere.test", password = "Nope-12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal("auth.invalid_credentials", await wrongPassword.ProblemCodeAsync());
        Assert.Equal("auth.invalid_credentials", await unknownUser.ProblemCodeAsync());
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();

        for (var i = 0; i < 5; i++)
        {
            await browser.PostAsync("/api/auth/login", new { identifier = school.Email, password = "Wrong-pass-1" });
        }

        var correct = await browser.PostAsync("/api/auth/login", new { identifier = school.Email, password = school.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal("auth.locked_out", await correct.ProblemCodeAsync());
    }

    [Fact]
    public async Task Unsafe_requests_without_csrf_token_are_rejected()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();

        var loginWithoutCsrf = await browser.PostAsync("/api/auth/login",
            new { identifier = school.Email, password = school.Password }, withCsrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, loginWithoutCsrf.StatusCode);
        Assert.Equal("csrf.invalid", await loginWithoutCsrf.ProblemCodeAsync());

        await browser.LoginAsync(school.Email, school.Password);
        var changeWithoutCsrf = await browser.PostAsync("/api/auth/password/change",
            new { currentPassword = school.Password, newPassword = "Another-pass-77" }, withCsrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, changeWithoutCsrf.StatusCode);
    }

    [Fact]
    public async Task Csrf_cookie_planted_by_an_attacker_does_not_satisfy_a_signed_in_session()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(school.Email, school.Password);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password/change")
        {
            Content = JsonContent.Create(new { currentPassword = school.Password, newPassword = "Another-pass-77" }),
        };
        request.Headers.Add("X-CSRF-Token", "attacker-chosen-value");
        request.Headers.Add("Cookie", "ss_csrf=attacker-chosen-value");
        var response = await browser.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_reuse_of_an_old_token_revokes_the_session()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(school.Email, school.Password);
        var firstRefreshToken = browser.Cookie("ss_rt");

        var refreshed = await browser.PostAsync("/api/auth/refresh", new { });
        await refreshed.EnsureSuccessWithBodyAsync();
        var secondRefreshToken = browser.Cookie("ss_rt");
        Assert.NotEqual(firstRefreshToken, secondRefreshToken);

        // An attacker replays the stolen first token well after the rotation grace window.
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var attacker = Factory.CreateBrowserClient();
        await attacker.EnsureCsrfAsync();
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replay.Headers.Add("X-CSRF-Token", attacker.CsrfToken);
        replay.Headers.Add("Cookie", $"ss_rt={firstRefreshToken}; ss_csrf={attacker.CsrfToken}");
        var replayResponse = await attacker.Http.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
        Assert.Equal("auth.refresh_reused", await replayResponse.ProblemCodeAsync());

        // The whole session is gone, including for the legitimate browser.
        var me = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        var legitimateRefresh = await browser.PostAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, legitimateRefresh.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refresh_within_grace_window_does_not_revoke_the_session()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(school.Email, school.Password);
        var token = browser.Cookie("ss_rt");

        await (await browser.PostAsync("/api/auth/refresh", new { })).EnsureSuccessWithBodyAsync();

        using var secondTab = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        secondTab.Headers.Add("X-CSRF-Token", browser.CsrfToken);
        secondTab.Headers.Add("Cookie", $"ss_rt={token}; ss_csrf={browser.CsrfToken}");
        var raced = await Factory.CreateClient().SendAsync(secondTab);
        Assert.Equal("auth.refresh_in_progress", await raced.ProblemCodeAsync());

        var me = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_session_immediately()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(school.Email, school.Password);
        var accessToken = browser.Cookie("ss_at", "/api");

        await (await browser.PostAsync("/api/auth/logout", new { })).EnsureSuccessWithBodyAsync();

        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Mobile_clients_use_bearer_tokens_without_csrf_and_can_rotate()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var client = Factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/token", new { identifier = school.Email, password = school.Password });
        var tokens = await login.ReadJsonAsync<TokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/token/refresh", new { refreshToken = tokens.RefreshToken });
        var rotated = await refresh.ReadJsonAsync<TokenResponse>();
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions_but_keeps_the_current_one()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var laptop = Factory.CreateBrowserClient();
        using var phone = Factory.CreateBrowserClient();
        await laptop.LoginAsync(school.Email, school.Password);
        await phone.LoginAsync(school.Email, school.Password);

        var change = await laptop.PostAsync("/api/auth/password/change",
            new { currentPassword = school.Password, newPassword = "Brand-new-pass-9" });
        await change.EnsureSuccessWithBodyAsync();

        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/auth/me")).StatusCode);

        using var fresh = Factory.CreateBrowserClient();
        await fresh.LoginAsync(school.Email, "Brand-new-pass-9");
    }

    [Fact]
    public async Task Weak_new_password_is_rejected_with_field_errors()
    {
        var school = await Factory.CreateSchoolWithUserAsync();
        using var browser = Factory.CreateBrowserClient();
        await browser.LoginAsync(school.Email, school.Password);

        var response = await browser.PostAsync("/api/auth/password/change", new { currentPassword = school.Password, newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("newPassword", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Endpoints_require_authentication_by_default()
    {
        using var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/auth/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.required", await response.ProblemCodeAsync());
    }
}
