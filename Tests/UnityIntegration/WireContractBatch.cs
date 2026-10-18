using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NET-002 Unity batch check: asserts the production JsonUtility codec (GameSession.Wire), measures
/// worst-case sizes, and writes golden fixtures for the console checks. Copy into a temporary project's
/// Assets/Editor with Tests/GameplayIntegration/WorstCaseMatch.cs; see README.md.
/// Run: Unity -batchmode -nographics -projectPath &lt;temp&gt; -executeMethod WireContractBatch.Run -wireOut &lt;dir&gt;
/// </summary>
public static class WireContractBatch
{
    private static readonly List<string> Report = new List<string>();
    private static int failures, assertions;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) failures++;
        Report.Add((condition ? "PASS " : "FAIL ") + message);
    }

    public static void Run()
    {
        string output = Argument("-wireOut") ?? "/tmp/td-wire-contract";
        Directory.CreateDirectory(Path.Combine(output, "Fixtures"));
        try
        {
            RawJsonUtility();
            Contract(output);
            Sizes();
        }
        catch (Exception error) { failures++; Report.Add("FAIL unexpected exception: " + error); }
        Report.Add($"{assertions} assertions, {failures} failures.");
        File.WriteAllLines(Path.Combine(output, "wire-contract-report.txt"), Report);
        foreach (var line in Report) Debug.Log("[WireContract] " + line);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>Records what JsonUtility does without the codec, which is why the codec exists.</summary>
    private static void RawJsonUtility()
    {
        var raw = JsonUtility.FromJson<ProjectionPacket>(JsonUtility.ToJson(new ProjectionPacket { Outcome = new CommandOutcome { RequestId = "x", Position = new TokenDto() } }));
        Report.Add("OBSERVED raw JsonUtility: null nested View decodes as " + (raw.View == null ? "null" : "a default instance"));
        Report.Add("OBSERVED raw JsonUtility: null view arrays decode as " + (raw.View?.Hand == null ? "null" : "empty arrays (length " + raw.View.Hand.Length + ")"));
        var command = JsonUtility.FromJson<CommandRequest>(JsonUtility.ToJson(new CommandRequest { RequestId = "c", Kind = CommandKind.Draw }));
        Report.Add("OBSERVED raw JsonUtility: null CommandRequest.Train decodes as " + (command.Train == null ? "null" : $"a default instance (IsShared={command.Train.IsShared}, Owner={command.Train.Owner})"));
        var unknown = JsonUtility.FromJson<CommandRequest>("{\"RequestId\":\"u\",\"Kind\":99,\"Surplus\":5}");
        Report.Add($"OBSERVED raw JsonUtility: out-of-range enum decodes as {(int)unknown.Kind}; unknown fields are ignored");
        bool threw = false;
        try { JsonUtility.FromJson<CommandRequest>("{"); } catch (ArgumentException) { threw = true; }
        Report.Add("OBSERVED raw JsonUtility: malformed JSON " + (threw ? "throws ArgumentException" : "does not throw"));
    }

    private static void Contract(string output)
    {
        var wire = GameSession.Wire;
        string Encode(object value)
        {
            string json, error;
            bool ok = value is CommandRequest r ? wire.TryEncode(r, out json, out error) : wire.TryEncode((ProjectionPacket)value, out json, out error);
            if (!ok) throw new InvalidOperationException(error);
            return json;
        }
        void Fixture(string name, string json) => File.WriteAllText(Path.Combine(output, "Fixtures", name), json);
        ProjectionPacket Projection(string json) => wire.TryDecodeProjection(json, out var p, out _) ? p : null;
        CommandRequest Command(string json) => wire.TryDecodeCommand(json, out var c, out _) ? c : null;

        var host = new AuthoritativeMatchCoordinator("wire", new ulong[] { 10, 50 },
            deckSupplier: round => DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id));

        string unseated = Encode(host.Submit(99, new CommandRequest { RequestId = "x" })[0].Packet);
        Fixture("unseated-reply.json", unseated);
        var unseatedBack = Projection(unseated);
        Check(unseatedBack != null && unseatedBack.View == null && unseatedBack.Outcome?.Error == RuleError.UnknownPlayer, "unseated reply decodes with an absent view");
        var client = new ClientSession(_ => { });
        client.Receive(unseatedBack);
        Check(client.View == null, "client installs no view from an unseated reply");

        var packet = host.GetProjection(10);
        string plain = Encode(packet);
        Fixture("view-without-required-train.json", plain);
        var plainBack = Projection(plain);
        Check(plainBack?.View != null && plainBack.View.RequiredTrain == null && plainBack.Outcome == null, "absent required train and outcome stay absent");
        Check(plainBack.View.Token.MatchId == "wire" && plainBack.View.Hand.SequenceEqual(packet.View.Hand), "present values survive");
        Check(JsonUtility.ToJson(plainBack) == plain, "re-encoding a decoded projection is byte-identical");

        packet.View.RequiredTrain = TrainIdDto.From(TrainId.Shared);
        string required = Encode(packet);
        Fixture("view-with-required-train.json", required);
        Check(Projection(required)?.View?.RequiredTrain?.IsShared == true, "present required train survives");

        string draw = Encode(new CommandRequest { RequestId = "d", Kind = CommandKind.Draw, Expected = packet.View.Token });
        Fixture("command-draw.json", draw);
        var drawBack = Command(draw);
        Check(drawBack != null && drawBack.Train == null && drawBack.Expected?.MatchId == "wire", "draw keeps an absent train");
        string place = Encode(new CommandRequest { RequestId = "p", Kind = CommandKind.Place, DominoId = 3, Expected = packet.View.Token, Train = TrainIdDto.From(TrainId.Shared) });
        Fixture("command-place.json", place);
        Check(Command(place)?.Train?.IsShared == true, "place keeps a present train");

        foreach (var bad in new[] { null, "", "{", "null", "[]", "42", draw.Substring(0, draw.Length / 2), draw.Replace("\"WireVersion\":1", "\"WireVersion\":2"), draw.Replace("\"Kind\":2", "\"Kind\":99"), draw.Replace("\"WireVersion\":1,", "") })
        {
            bool rejected;
            try { rejected = Command(bad) == null; }
            catch (Exception) { rejected = false; }
            Check(rejected, "command rejected without throwing: " + Shorten(bad));
        }
        foreach (var bad in new[] { null, "", "{", "null", plain.Substring(0, plain.Length - 3), plain.Replace("\"WireVersion\":1", "\"WireVersion\":0"), "{\"WireVersion\":1}" })
        {
            bool rejected;
            try { rejected = Projection(bad) == null; }
            catch (Exception) { rejected = false; }
            Check(rejected, "projection rejected without throwing: " + Shorten(bad));
        }
        Check(Command(draw.Replace("\"RequestId\":\"d\"", "\"RequestId\":\"" + new string('a', WireCodec.MaximumCommandWireBytes) + "\"")) == null, "oversized command rejected");

        // Identical retries still replay through the production codec.
        var ready = Encode(new CommandRequest { RequestId = "ready", Kind = CommandKind.Ready, Expected = host.GetProjection(10).View.Token });
        Check(host.Submit(10, Command(ready))[0].Packet.Outcome.Accepted, "decoded ready accepted");
        long revision = host.GetProjection(10).View.Token.Revision;
        var retry = host.Submit(10, Command(ready));
        Check(retry.Count == 1 && retry[0].Packet.Outcome.Accepted && host.GetProjection(10).View.Token.Revision == revision, "decoded retry replays without effect");
    }

    private static void Sizes()
    {
        var wire = GameSession.Wire;
        foreach (int players in new[] { 2, 4, 8 })
        {
            int largest = 0, largestCommand = 0, packets = 0, largestChars = 0;
            foreach (int seed in Enumerable.Range(0, 20))
                WorstCaseMatch.Play(players, seed, seed % 2 == 0 ? CommitmentMode.Immediate : CommitmentMode.Draft,
                    p =>
                    {
                        if (!wire.TryEncode(p, out var json, out var error)) throw new InvalidOperationException(error);
                        packets++;
                        largestChars = Math.Max(largestChars, json.Length);
                        largest = Math.Max(largest, WireCodec.WireBytes(json));
                        if (!wire.TryDecodeProjection(json, out _, out var decodeError)) throw new InvalidOperationException(decodeError);
                    },
                    c => largestCommand = Math.Max(largestCommand, wire.TryEncode(c, out var json, out _) ? WireCodec.WireBytes(json) : int.MaxValue));
            Report.Add($"MEASURED players={players} matches=20 packets={packets} largestProjection={largestChars} chars / {largest} wire bytes ({(double)WireCodec.MaximumProjectionWireBytes / largest:F2}x headroom) largestCommand={largestCommand} wire bytes");
            Check(largest < WireCodec.MaximumProjectionWireBytes, $"{players}-player worst case fits the projection guard");
            Check(largestCommand < WireCodec.MaximumCommandWireBytes, $"{players}-player commands fit the command guard");
        }
    }

    private static string Shorten(string value) => value == null ? "<null>" : value.Length <= 40 ? value : value.Substring(0, 40) + "…";

    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
