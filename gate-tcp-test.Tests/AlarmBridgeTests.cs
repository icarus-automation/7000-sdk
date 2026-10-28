// The bridge end to end against a fake server on 127.0.0.1. Offer() is exactly what OutputTags calls on the DLL thread.
using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace GateTcpTest.Tests;

public class AlarmBridgeTests(ITestOutputHelper output)
{
    const string Epc = "30351597B03DAA10A02DD553";
    const string Gate = "169.254.68.93:1272";
    static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task One_tag_reported_20_times_in_30_seconds_makes_one_post_and_never_waits_on_a_slow_server()
    {
        await using var server = await FakeAlarmServer.StartAsync(delay: TimeSpan.FromSeconds(10));
        var log = new LogLines();
        var bridge = new AlarmBridge(server.Config(), log.Add);

        var run = Stopwatch.StartNew();
        var slowest = TimeSpan.Zero;
        int whileServerBusy = 0;
        for (int i = 0; i < 20; i++)
        {
            if (i > 0) await Task.Delay(1400); // ~27 s in all, like a tag sitting in the gate
            var call = Stopwatch.StartNew();
            bridge.Offer(Gate, Epc);
            call.Stop();
            if (call.Elapsed > slowest) slowest = call.Elapsed;

            if (i == 0) await server.WaitForPosts(1, TimeSpan.FromSeconds(5)); // the 10 s request is now in flight
            else if (server.Answered == 0) whileServerBusy++;
        }
        TimeSpan reported = run.Elapsed;
        await log.WaitFor("ALARM SENT", Wait);
        bridge.Stop(TimeSpan.FromSeconds(3));

        output.WriteLine($"20 reports in {reported.TotalSeconds:0.0} s, {whileServerBusy} of them while the server was still answering, slowest Offer {slowest.TotalMilliseconds:0.000} ms");
        foreach (string line in log.All) output.WriteLine(line);

        Assert.True(reported < TimeSpan.FromSeconds(30), $"reports took {reported}");
        Assert.True(whileServerBusy >= 5, $"only {whileServerBusy} reports overlapped the slow answer");
        Assert.True(slowest < TimeSpan.FromMilliseconds(100), $"Offer took {slowest.TotalMilliseconds} ms");
        ReceivedPost post = Assert.Single(server.Posts);
        Assert.Equal(Epc, post.Epc);
        Assert.Equal("GATE-1", post.ReaderName);
        Assert.StartsWith("application/json", post.ContentType);
        Assert.Matches(new Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$"), post.ScannedAt);
        Assert.Contains("ALARM SENT  epc=30351597B03DAA10A02DD553 reader=GATE-1 alarm recorded (HTTP 201 alarm_created, 10.", log.All.Single());
    }

    [Fact]
    public async Task Sold_and_duplicate_answers_print_their_own_lines()
    {
        await using var server = await FakeAlarmServer.StartAsync(TimeSpan.Zero,
            (_, epc) => epc == "SOLD" ? new Reply(200, "ignored_sold") : new Reply(200, "duplicate"));
        var log = new LogLines();
        var bridge = new AlarmBridge(server.Config(), log.Add);

        bridge.Offer(Gate, "SOLD");
        bridge.Offer("169.254.68.94:4000", "DUP");
        await log.WaitFor("DUPLICATE", Wait);
        bridge.Stop(TimeSpan.FromSeconds(3));

        Assert.Equal(
            new[] { "SOLD TAG    epc=SOLD reader=GATE-1 ignored, the tag is sold",
                    "DUPLICATE   epc=DUP reader=169.254.68.94 already alarmed in the last 30 s" },
            log.All.Select(l => l[..l.IndexOf(" (HTTP", StringComparison.Ordinal)]));
    }

    [Fact]
    public async Task Busy_server_and_dropped_connection_are_retried_with_the_same_item()
    {
        await using var server = await FakeAlarmServer.StartAsync(TimeSpan.Zero, (n, _) => n switch
        {
            1 => new Reply(503, Message: "Service Unavailable"),
            2 => new Reply(0, Abort: true), // network error
            3 => new Reply(429, Message: "ThrottlerException: Too Many Requests"),
            _ => Reply.AlarmCreated,
        });
        var log = new LogLines();
        var bridge = new AlarmBridge(server.Config(), log.Add);

        bridge.Offer(Gate, Epc);
        await log.WaitFor("ALARM SENT", Wait); // after 1 + 2 + 4 s of backoff
        bridge.Stop(TimeSpan.FromSeconds(3));
        foreach (string line in log.All) output.WriteLine(line);

        Assert.Equal(4, server.Posts.Count);
        Assert.Single(server.Posts.Select(p => p.ScannedAt).Distinct());
        Assert.Equal(3, log.Count("RETRY"));
    }

    [Fact]
    public async Task Bad_request_is_not_retried_and_the_next_report_tries_again()
    {
        await using var server = await FakeAlarmServer.StartAsync(TimeSpan.Zero, (_, _) => new Reply(400, Message: "epc must be a string"));
        var log = new LogLines();
        var bridge = new AlarmBridge(server.Config(), log.Add);

        bridge.Offer(Gate, Epc);
        await log.WaitFor("SEND FAILED", Wait);
        bridge.Offer(Gate, Epc); // 2.5 s later in real life, well inside the 30 s cooldown
        await log.WaitFor("SEND FAILED", Wait, count: 2);
        bridge.Stop(TimeSpan.FromSeconds(3));

        Assert.Equal(2, server.Posts.Count);
        Assert.Equal("SEND FAILED epc=30351597B03DAA10A02DD553 reader=GATE-1 HTTP 400 epc must be a string, not retried", log.All[0]);
    }

    [Fact]
    public async Task Stop_lets_the_queue_finish()
    {
        await using var server = await FakeAlarmServer.StartAsync(TimeSpan.FromMilliseconds(300));
        var log = new LogLines();
        var bridge = new AlarmBridge(server.Config(), log.Add);

        foreach (string epc in new[] { "A1", "A2", "A3" }) bridge.Offer(Gate, epc);
        bridge.Stop(TimeSpan.FromSeconds(3));

        Assert.Equal(3, server.Posts.Count);
        Assert.Equal(3, log.Count("ALARM SENT"));
        Assert.Equal(0, log.Count("BRIDGE      stopped"));
    }

    [Fact]
    public async Task Stop_gives_up_after_the_grace_period_and_refuses_new_tags()
    {
        await using var server = await FakeAlarmServer.StartAsync(TimeSpan.FromSeconds(10));
        var log = new LogLines();
        var bridge = new AlarmBridge(server.Config(), log.Add);

        bridge.Offer(Gate, Epc);
        await server.WaitForPosts(1, TimeSpan.FromSeconds(5));
        var stop = Stopwatch.StartNew();
        bridge.Stop(TimeSpan.FromSeconds(3));
        stop.Stop();
        bridge.Offer(Gate, "AFTER-STOP");
        await Task.Delay(500);

        Assert.InRange(stop.Elapsed, TimeSpan.FromSeconds(2.9), TimeSpan.FromSeconds(4.5));
        Assert.Contains("BRIDGE      stopped with 1 alarm(s) not sent", log.All);
        Assert.Single(server.Posts);
    }
}
