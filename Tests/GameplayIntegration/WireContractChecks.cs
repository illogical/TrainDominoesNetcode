using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// NET-002 wire-contract checks through the production WireCodec over System.Text.Json, plus decoding of
/// golden fixtures produced by JsonUtility in the Unity batch check. JsonUtility itself is asserted there.
/// </summary>
internal static class WireContractChecks
{
    private static readonly string Current = "\"WireVersion\":" + WireCodec.Version;
    private static string Version(int v) => "\"WireVersion\":" + v;
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static readonly WireCodec Codec = LoopbackRelay.Codec;

    private static string Encode(CommandRequest r) => Codec.TryEncode(r, out var json, out var e) ? json : throw new Exception(e);
    private static string Encode(ProjectionPacket p) => Codec.TryEncode(p, out var json, out var e) ? json : throw new Exception(e);
    private static CommandRequest Command(string json) => Codec.TryDecodeCommand(json, out var r, out _) ? r : null;
    private static ProjectionPacket Projection(string json) => Codec.TryDecodeProjection(json, out var p, out _) ? p : null;

    private static AuthoritativeMatchCoordinator Host() => new AuthoritativeMatchCoordinator("wire", new ulong[] { 10, 50 },
        deckSupplier: round => DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id));

    public static void Run()
    {
        Presence();
        Rejection();
        Deduplication();
        Size();
        Fixtures();
        Console.WriteLine("Wire contract: " + assertions + " assertions passed.");
    }

    private static void Presence()
    {
        var host = Host();
        // The unseated reply carries only an outcome; it must never become a blank view.
        var unseated = host.Submit(99, new CommandRequest { RequestId = "x" })[0].Packet;
        var decoded = Projection(Encode(unseated));
        Check(decoded != null && decoded.View == null && decoded.Outcome?.Error == RuleError.UnknownPlayer, "unseated reply keeps an absent view");
        var client = new ClientSession(_ => { });
        client.Receive(decoded);
        Check(client.View == null, "client installs no view from an unseated reply");

        var view = host.GetProjection(10);
        Check(view.View.RequiredTrain == null, "fixture view has no required train");
        var plain = Projection(Encode(view));
        Check(plain.View.RequiredTrain == null && plain.Outcome == null, "absent required train and outcome survive the round trip");
        Check(plain.View.Token.MatchId == "wire" && plain.View.Hand.Length == view.View.Hand.Length, "present values survive the round trip");
        view.View.RequiredTrain = TrainIdDto.From(TrainId.Shared);
        Check(Projection(Encode(view)).View.RequiredTrain?.IsShared == true, "present required train survives the round trip");

        var draw = new CommandRequest { RequestId = "d", Kind = CommandKind.Draw, Expected = view.View.Token };
        var drawBack = Command(Encode(draw));
        Check(drawBack.Train == null && drawBack.Expected?.MatchId == "wire", "absent train and present position survive");
        var place = new CommandRequest { RequestId = "p", Kind = CommandKind.Place, DominoId = 3, Train = TrainIdDto.From(TrainId.Personal(new PlayerId(1))) };
        var placeBack = Command(Encode(place));
        Check(placeBack.Expected == null && placeBack.Train?.Owner == 1 && !placeBack.Train.IsShared, "absent position and present train survive");

        // Arrays are never null after decoding, even when the sender omitted them.
        var sparse = Projection(Encode(view).Replace("\"Hand\":[", "\"Ignored\":["));
        Check(sparse != null && sparse.View.Hand != null && sparse.View.Hand.Length == 0, "missing arrays decode as empty");
    }

    private static void Rejection()
    {
        var valid = Encode(new CommandRequest { RequestId = "r", Kind = CommandKind.Draw });
        foreach (var bad in new[] { null, "", "{", "null", "[]", "42", "{\"Kind\":\"Draw\"}", valid.Substring(0, valid.Length / 2) })
            Check(Command(bad) == null, "malformed command rejected: " + (bad ?? "<null>"));
        Check(Command(valid.Replace(Current, Version(WireCodec.Version + 1))) == null, "wrong command version rejected");
        Check(Command(valid.Replace(Current, Version(0))) == null, "unversioned command rejected");
        Check(Command(valid.Replace("\"Kind\":2", "\"Kind\":99")) == null, "unknown command kind rejected");
        Check(Command(valid.Replace("\"HasTrain\":false", "\"HasTrain\":true")) == null, "flagged but missing train rejected");
        Check(Command(valid.Replace("\"RequestId\":\"r\"", "\"RequestId\":\"" + new string('a', WireCodec.MaximumCommandWireBytes) + "\"")) == null, "oversized command rejected before parsing");
        Check(!Codec.TryEncode(new CommandRequest { RequestId = new string('a', WireCodec.MaximumCommandWireBytes) }, out _, out _), "oversized command is not sent");

        var host = Host();
        var packet = Encode(host.GetProjection(10));
        foreach (var bad in new[] { null, "", "{", "null", "{\"View\":7}", packet.Substring(0, packet.Length - 3) })
            Check(Projection(bad) == null, "malformed projection rejected: " + (bad ?? "<null>"));
        Check(Projection(packet.Replace(Current, Version(WireCodec.Version + 1))) == null, "wrong projection version rejected");
        Check(Projection("{" + Current + ",\"HasView\":false,\"HasOutcome\":false}") == null, "empty projection rejected");
        Check(Projection(packet.Replace("\"MatchId\":\"wire\"", "\"MatchId\":\"\"")) == null, "view without a match position rejected");
        Check(Projection(packet.Replace("\"HasRequiredTrain\":false", "\"HasRequiredTrain\":true")) == null, "flagged but missing required train rejected");
        var huge = new ProjectionPacket { Outcome = new CommandOutcome { RequestId = new string('a', WireCodec.MaximumProjectionWireBytes), Position = new TokenDto() } };
        Check(!Codec.TryEncode(huge, out _, out _), "oversized projection is not sent");
    }

    private static void Deduplication()
    {
        // Wire flags must not change request identity: an identical retry still replays.
        var host = Host();
        var ready = Encode(new CommandRequest { RequestId = "ready", Kind = CommandKind.Ready, Expected = host.GetProjection(10).View.Token });
        Check(host.Submit(10, Command(ready))[0].Packet.Outcome.Accepted, "decoded ready accepted");
        long revision = host.GetProjection(10).View.Token.Revision;
        var retry = host.Submit(10, Command(ready));
        Check(retry.Count == 1 && retry[0].Packet.Outcome.Accepted && host.GetProjection(10).View.Token.Revision == revision, "decoded retry replays without effect");
    }

    private static void Size()
    {
        int largest = 0, largestCommand = 0;
        foreach (int seed in Enumerable.Range(0, 10))
            WorstCaseMatch.Play(8, seed, seed % 2 == 0 ? CommitmentMode.Immediate : CommitmentMode.Draft,
                p => largest = Math.Max(largest, WireCodec.WireBytes(Encode(p))),
                c => largestCommand = Math.Max(largestCommand, WireCodec.WireBytes(Encode(c))));
        Check(largest < WireCodec.MaximumProjectionWireBytes, "eight-player worst case fits the projection guard");
        Check(largestCommand < WireCodec.MaximumCommandWireBytes, "largest command fits the command guard");
        Console.WriteLine($"Wire size (System.Text.Json approximation, 10 eight-player matches): projection {largest} bytes, command {largestCommand} bytes.");
    }

    /// <summary>JsonUtility output from the Unity batch check must decode identically in .NET.</summary>
    private static void Fixtures([CallerFilePath] string source = "")
    {
        string folder = Path.Combine(Path.GetDirectoryName(source), "Fixtures");
        Check(Directory.Exists(folder), "JsonUtility fixtures exist (run the Unity wire check to produce them)");
        string Read(string name) => File.ReadAllText(Path.Combine(folder, name));

        var unseated = Projection(Read("unseated-reply.json"));
        Check(unseated != null && unseated.View == null && unseated.Outcome != null, "JsonUtility unseated reply decodes without a view");
        var view = Projection(Read("view-without-required-train.json"));
        Check(view != null && view.View != null && view.View.RequiredTrain == null && view.Outcome == null, "JsonUtility absent values decode as absent");
        var required = Projection(Read("view-with-required-train.json"));
        Check(required?.View?.RequiredTrain?.IsShared == true, "JsonUtility present required train decodes");
        var draw = Command(Read("command-draw.json"));
        Check(draw != null && draw.Train == null && draw.Expected != null, "JsonUtility draw command decodes with an absent train");
        var place = Command(Read("command-place.json"));
        Check(place?.Train != null && place.Expected != null, "JsonUtility place command decodes with a present train");
    }
}
