// The pure rules: cooldown, reader names, what to do with each HTTP status, and gate-bridge.json parsing.
using System.Text.Json;

namespace GateTcpTest.Tests;

public class CooldownTests
{
    const string Epc = "30351597B03DAA10A02DD553";
    static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
    static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Same_tag_inside_the_window_is_skipped()
    {
        var cooldown = new Cooldown(Window);
        Assert.True(cooldown.TryStart(Epc, At(100)));
        Assert.False(cooldown.TryStart(Epc, At(102.5)));  // the gate's next report
        Assert.False(cooldown.TryStart(Epc, At(129.999)));
    }

    [Fact]
    public void Same_tag_after_the_window_is_queued_again_and_starts_a_new_window()
    {
        var cooldown = new Cooldown(Window);
        Assert.True(cooldown.TryStart(Epc, At(100)));
        Assert.True(cooldown.TryStart(Epc, At(130)));
        Assert.False(cooldown.TryStart(Epc, At(131)));
    }

    [Fact]
    public void Other_tags_are_not_held_back()
    {
        var cooldown = new Cooldown(Window);
        Assert.True(cooldown.TryStart(Epc, At(100)));
        Assert.True(cooldown.TryStart("E2806995000040039398B5EA", At(101)));
    }

    [Fact]
    public void Expired_entries_are_removed_so_the_map_does_not_grow()
    {
        var cooldown = new Cooldown(Window);
        for (int i = 0; i < 100; i++) cooldown.TryStart($"EPC{i}", At(100));
        Assert.Equal(100, cooldown.Count);
        cooldown.TryStart("NEW", At(131));
        Assert.Equal(1, cooldown.Count);
    }

    [Fact]
    public void Clear_lets_the_next_report_through()
    {
        var cooldown = new Cooldown(Window);
        cooldown.TryStart(Epc, At(100));
        cooldown.Clear(Epc, At(100));
        Assert.True(cooldown.TryStart(Epc, At(101)));
    }

    [Fact]
    public void Clear_for_an_old_item_keeps_a_newer_entry()
    {
        var cooldown = new Cooldown(Window);
        cooldown.TryStart(Epc, At(100));
        cooldown.TryStart(Epc, At(131)); // queued again while the first item was still retrying
        cooldown.Clear(Epc, At(100));    // the first item then fails
        Assert.False(cooldown.TryStart(Epc, At(132)));
    }
}

public class ReaderNameTests
{
    static readonly Dictionary<string, string> Gates = new() { ["169.254.68.93"] = "GATE-1" };

    [Theory]
    [InlineData("169.254.68.93:1272", "169.254.68.93")]
    [InlineData("169.254.68.93:50113", "169.254.68.93")]
    [InlineData("169.254.68.93", "169.254.68.93")]
    public void IpOf_strips_the_port(string connId, string ip) => Assert.Equal(ip, AlarmRules.IpOf(connId));

    [Theory]
    [InlineData("169.254.68.93:1272")]
    [InlineData("169.254.68.93:50113")] // same gate after a reconnect, new port
    public void Gate_in_the_map_gets_its_name_whatever_the_port(string connId) =>
        Assert.Equal("GATE-1", AlarmRules.ReaderName(connId, Gates));

    [Fact]
    public void Gate_not_in_the_map_uses_its_ip() =>
        Assert.Equal("169.254.68.94", AlarmRules.ReaderName("169.254.68.94:1272", Gates));
}

public class StatusRuleTests
{
    [Theory]
    [InlineData(201)] // alarm_created
    [InlineData(200)] // ignored_sold or duplicate
    public void Success_is_done(int status) => Assert.Equal(PostAction.Done, AlarmRules.ActionFor(status));

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public void Bad_request_is_never_retried(int status) => Assert.Equal(PostAction.Fail, AlarmRules.ActionFor(status));

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void Busy_or_broken_server_is_retried(int status) => Assert.Equal(PostAction.Retry, AlarmRules.ActionFor(status));

    [Theory]
    [InlineData(0, null, 1)]
    [InlineData(1, null, 2)]
    [InlineData(2, null, 4)]
    [InlineData(0, 10, 10)]  // Retry-After longer than the backoff wins
    [InlineData(2, 1, 4)]    // shorter one does not
    [InlineData(0, 120, 30)] // capped
    public void Retry_delay_backs_off(int retry, int? retryAfterSeconds, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds),
            AlarmRules.RetryDelay(retry, retryAfterSeconds is int s ? TimeSpan.FromSeconds(s) : null));

    [Fact]
    public void ScannedAt_is_utc_with_milliseconds_and_z() =>
        Assert.Equal("2026-11-02T10:14:20.000Z",
            AlarmRules.FormatScannedAt(new DateTime(2026, 11, 2, 10, 14, 20, DateTimeKind.Utc)));

    [Fact]
    public void Reply_outcome_and_error_message_are_read()
    {
        Assert.Equal(("ignored_sold", "Success"),
            AlarmRules.ReadReply("""{"statusCode":200,"message":"Success","data":{"outcome":"ignored_sold"}}"""));
        Assert.Equal((null, "epc should not be empty; epc must be a string"),
            AlarmRules.ReadReply("""{"statusCode":400,"message":["epc should not be empty","epc must be a string"],"error":"Bad Request"}"""));
        Assert.Equal((null, null), AlarmRules.ReadReply("<html>502 Bad Gateway</html>"));
    }
}

public class ConfigTests
{
    [Fact]
    public void Full_file_is_read()
    {
        var warnings = new List<string>();
        BridgeConfig c = BridgeConfig.Parse("""
            { "serverUrl": "http://192.168.1.20:8000/", "cooldownSeconds": 45, "gates": { "169.254.68.93": "GATE-1" } }
            """, warnings.Add);
        Assert.Equal("http://192.168.1.20:8000/api/v1/alarms", c.AlarmUrl?.ToString());
        Assert.Equal(TimeSpan.FromSeconds(45), c.Cooldown);
        Assert.Equal("GATE-1", c.Gates["169.254.68.93"]);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Cooldown_defaults_to_30_seconds()
    {
        BridgeConfig c = BridgeConfig.Parse("""{ "serverUrl": "http://localhost:8000" }""", _ => { });
        Assert.Equal(TimeSpan.FromSeconds(30), c.Cooldown);
        Assert.Empty(c.Gates);
    }

    [Theory]
    [InlineData("""{ "gates": {} }""")]
    [InlineData("""{ "serverUrl": "" }""")]
    [InlineData("""{ "serverUrl": "localhost:8000" }""")]
    public void Missing_or_bad_server_url_means_log_only_with_a_warning(string json)
    {
        var warnings = new List<string>();
        Assert.Null(BridgeConfig.Parse(json, warnings.Add).AlarmUrl);
        Assert.Contains(warnings, w => w.Contains("serverUrl"));
    }

    [Fact]
    public void Misspelt_key_is_an_error() =>
        Assert.Throws<JsonException>(() => BridgeConfig.Parse("""{ "serverUrl": "http://x", "cooldown": 30 }""", _ => { }));

    [Fact]
    public void Missing_file_means_log_only_with_a_warning()
    {
        var warnings = new List<string>();
        Assert.Null(BridgeConfig.Load(Path.Combine(AppContext.BaseDirectory, "no-such-gate-bridge.json"), warnings.Add).AlarmUrl);
        Assert.Contains(warnings, w => w.Contains("not found"));
    }
}
