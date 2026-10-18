using System;
using System.Linq;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>
    /// The gameplay wire contract. All RPC payloads pass through here so presence, version and size
    /// rules live in one place. In memory, an absent nested value is null; on the wire it is an explicit
    /// <c>Has*</c> flag, because JsonUtility cannot represent a null nested class.
    /// </summary>
    /// <remarks>
    /// The JSON functions are supplied by the caller (JsonUtility in Unity, System.Text.Json in console
    /// checks) so this class has no Unity dependency. Decoding never throws.
    /// </remarks>
    public sealed class WireCodec
    {
        /// <summary>Bump when the payload shape changes. Host and clients must run the same build.</summary>
        public const int Version = 1;

        /// <summary>
        /// Upper bound for one projection, in on-wire bytes. Reliable RPCs are streamed by Unity Transport,
        /// so this is an adapter guard against runaway growth, not a transport limit. Measured worst case:
        /// see Assets/Docs/GAMEPLAY_INTEGRATION_VERIFICATION.md.
        /// </summary>
        public const int MaximumProjectionWireBytes = 64 * 1024;

        /// <summary>Commands are tiny (about 0.5 KB on the wire); anything far larger is rejected before parsing.</summary>
        public const int MaximumCommandWireBytes = 4 * 1024;

        /// <summary>Bounds every array in a decoded projection; the double-twelve set has 91 tiles.</summary>
        public const int MaximumArrayLength = 1024;

        private readonly Func<object, string> toJson;
        private readonly Func<string, Type, object> fromJson;

        public WireCodec(Func<object, string> toJson, Func<string, Type, object> fromJson)
        {
            this.toJson = toJson ?? throw new ArgumentNullException(nameof(toJson));
            this.fromJson = fromJson ?? throw new ArgumentNullException(nameof(fromJson));
        }

        /// <summary>
        /// Size of a string RPC argument: Netcode's FastBufferWriter writes a 4-byte length and two bytes
        /// per character unless oneByteChars is requested.
        /// </summary>
        public static int WireBytes(string json) => 4 + 2 * (json?.Length ?? 0);

        public bool TryEncode(CommandRequest request, out string json, out string error)
        {
            json = null;
            if (request == null) { error = "No command to encode."; return false; }
            // Flags are wire-only and derived from nullness, so setting them on the caller's object is idempotent.
            request.WireVersion = Version;
            request.HasExpected = request.Expected != null;
            request.HasTrain = request.Train != null;
            return Finish(toJson(request), MaximumCommandWireBytes, out json, out error);
        }

        public bool TryEncode(ProjectionPacket packet, out string json, out string error)
        {
            json = null;
            if (packet == null) { error = "No projection to encode."; return false; }
            packet.WireVersion = Version;
            packet.HasView = packet.View != null;
            packet.HasOutcome = packet.Outcome != null;
            if (packet.View != null) packet.View.HasRequiredTrain = packet.View.RequiredTrain != null;
            return Finish(toJson(packet), MaximumProjectionWireBytes, out json, out error);
        }

        public bool TryDecodeCommand(string json, out CommandRequest request, out string error)
        {
            request = null;
            if (!TryParse(json, MaximumCommandWireBytes, out request, out error)) return false;
            if (request.WireVersion != Version) return Fail(out error, $"Wire version {request.WireVersion} does not match {Version}.", ref request);
            if (!Enum.IsDefined(typeof(CommandKind), request.Kind)) return Fail(out error, "Unknown command kind.", ref request);
            if (!request.HasExpected) request.Expected = null;
            else if (request.Expected == null) return Fail(out error, "Expected position is flagged but missing.", ref request);
            if (!request.HasTrain) request.Train = null;
            else if (request.Train == null) return Fail(out error, "Train is flagged but missing.", ref request);
            return true;
        }

        public bool TryDecodeProjection(string json, out ProjectionPacket packet, out string error)
        {
            packet = null;
            if (!TryParse(json, MaximumProjectionWireBytes, out packet, out error)) return false;
            if (packet.WireVersion != Version) return Fail(out error, $"Wire version {packet.WireVersion} does not match {Version}.", ref packet);
            if (!packet.HasView) packet.View = null;
            if (!packet.HasOutcome) packet.Outcome = null;
            if (packet.View == null && packet.Outcome == null) return Fail(out error, "Projection carries neither a view nor an outcome.", ref packet);
            if (packet.HasOutcome && (packet.Outcome == null || packet.Outcome.Position == null)) return Fail(out error, "Outcome is flagged but incomplete.", ref packet);
            if (packet.Events == null) packet.Events = Array.Empty<PresentationEventDto>();
            if (packet.Events.Length > MaximumArrayLength) return Fail(out error, "Too many events.", ref packet);
            if (packet.HasView && !TryNormalize(packet.View, out error)) { packet = null; return false; }
            return true;
        }

        private bool Finish(string json, int limit, out string result, out string error)
        {
            result = null;
            if (string.IsNullOrEmpty(json)) { error = "Serializer produced no output."; return false; }
            int bytes = WireBytes(json);
            if (bytes > limit) { error = $"Encoded payload is {bytes} bytes; the limit is {limit}."; return false; }
            result = json; error = null;
            return true;
        }

        private bool TryParse<T>(string json, int limit, out T value, out string error) where T : class
        {
            value = null;
            if (string.IsNullOrEmpty(json)) { error = "Empty payload."; return false; }
            if (WireBytes(json) > limit) { error = $"Payload is {WireBytes(json)} bytes; the limit is {limit}."; return false; }
            try { value = fromJson(json, typeof(T)) as T; }
            // A trust boundary: any parser failure (ArgumentException in Unity, JsonException in .NET) is a rejection.
            catch (Exception parse) { error = "Malformed payload: " + parse.GetType().Name; return false; }
            if (value == null) { error = "Payload did not parse."; return false; }
            error = null;
            return true;
        }

        /// <summary>Required nested values must be present; arrays are never null afterwards.</summary>
        private static bool TryNormalize(PlayerViewDto view, out string error)
        {
            error = null;
            if (view.Token == null || string.IsNullOrEmpty(view.Token.MatchId)) error = "View has no match position.";
            else if (view.Settings == null || view.Allowed == null) error = "View is missing settings or allowed actions.";
            if (error != null) return false;

            if (!view.HasRequiredTrain) view.RequiredTrain = null;
            else if (view.RequiredTrain == null) { error = "Required train is flagged but missing."; return false; }

            view.Players = OrEmpty(view.Players);
            view.ReadyPlayers = OrEmpty(view.ReadyPlayers);
            view.Hand = OrEmpty(view.Hand);
            view.AnnouncedPlayers = OrEmpty(view.AnnouncedPlayers);
            view.HandCounts = OrEmpty(view.HandCounts);
            view.Totals = OrEmpty(view.Totals);
            view.Trains = OrEmpty(view.Trains);
            view.OpenDoubles = OrEmpty(view.OpenDoubles);
            view.LegalPlacements = OrEmpty(view.LegalPlacements);
            view.RoundResults = OrEmpty(view.RoundResults);
            view.Winners = OrEmpty(view.Winners);
            view.Allowed.ChallengeTargets = OrEmpty(view.Allowed.ChallengeTargets);
            foreach (var train in view.Trains) if (train != null) train.Placements = OrEmpty(train.Placements);
            foreach (var result in view.RoundResults) if (result != null) result.Scores = OrEmpty(result.Scores);

            var lengths = new[]
            {
                view.Players.Length, view.ReadyPlayers.Length, view.Hand.Length, view.AnnouncedPlayers.Length,
                view.HandCounts.Length, view.Totals.Length, view.Trains.Length, view.OpenDoubles.Length,
                view.LegalPlacements.Length, view.RoundResults.Length, view.Winners.Length, view.Allowed.ChallengeTargets.Length,
            };
            if (lengths.Any(n => n > MaximumArrayLength)) { error = "View array exceeds the length bound."; return false; }
            if (view.Trains.Any(t => t == null || t.Id == null || t.Placements.Length > MaximumArrayLength)
                || view.OpenDoubles.Any(t => t == null)
                || view.LegalPlacements.Any(p => p == null || p.Train == null)
                || view.RoundResults.Any(r => r == null)
                || view.HandCounts.Any(c => c == null) || view.Totals.Any(c => c == null))
            { error = "View contains an incomplete element."; return false; }
            return true;
        }

        private static T[] OrEmpty<T>(T[] value) => value ?? Array.Empty<T>();

        private static bool Fail<T>(out string error, string message, ref T value) where T : class
        {
            error = message; value = null;
            return false;
        }
    }
}
