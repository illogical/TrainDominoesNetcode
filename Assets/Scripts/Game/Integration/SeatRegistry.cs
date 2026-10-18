using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TrainDominoesRulesEngine;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>Why a reconnecting client could not reclaim a seat.</summary>
    public enum ReclaimError { None, WrongMatch, UnknownSeat, WrongSecret, SeatStillConnected, AlreadySeated }

    /// <summary>
    /// Frozen match seats and the transport identity currently bound to each one.
    /// Rule state, outcome caches and projections key on seats, never on transport IDs,
    /// so an authenticated reconnect can rebind a seat without changing its history.
    /// Each seat has a host-issued secret, sent only to that seat, which proves ownership on reconnect.
    /// </summary>
    public sealed class SeatRegistry
    {
        private readonly Dictionary<ulong, PlayerId> seatsByTransport;
        private readonly Dictionary<PlayerId, ulong> transportsBySeat;
        private readonly Dictionary<PlayerId, string> secrets = new Dictionary<PlayerId, string>();
        private readonly Func<string> newSecret;

        public SeatRegistry(IEnumerable<ulong> transportIds, Func<string> secretSource = null)
        {
            var ids = transportIds?.OrderBy(id => id).ToArray() ?? throw new ArgumentNullException(nameof(transportIds));
            if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Duplicate transport identity.");
            Seats = ids.Select((id, index) => new PlayerId(index)).ToArray();
            seatsByTransport = ids.Select((id, index) => new { id, seat = Seats[index] }).ToDictionary(p => p.id, p => p.seat);
            transportsBySeat = seatsByTransport.ToDictionary(p => p.Value, p => p.Key);
            newSecret = secretSource ?? RandomSecret;
            foreach (var seat in Seats) secrets[seat] = newSecret();
        }

        /// <summary>Seats in fixed seat order.</summary>
        public IReadOnlyList<PlayerId> Seats { get; }

        /// <summary>Transport identities currently bound to a seat, in seat order.</summary>
        public IEnumerable<ulong> Transports => Seats.Select(seat => transportsBySeat[seat]);

        public bool TryGetSeat(ulong transport, out PlayerId seat) => seatsByTransport.TryGetValue(transport, out seat);

        public ulong TransportFor(PlayerId seat) => transportsBySeat[seat];

        /// <summary>The seat's current secret. Only ever placed in that seat's own projection.</summary>
        public string SecretFor(PlayerId seat) => secrets[seat];

        /// <summary>
        /// Moves a seat to a new transport when the caller proves ownership and the old connection is gone.
        /// The secret rotates on success, so a copied or replayed secret cannot be used again.
        /// </summary>
        public ReclaimError TryRebind(int seatIndex, string secret, ulong newTransport, Func<ulong, bool> isConnected)
        {
            if (seatIndex < 0 || seatIndex >= Seats.Count) return ReclaimError.UnknownSeat;
            var seat = Seats[seatIndex];
            if (!FixedTimeEquals(secrets[seat], secret)) return ReclaimError.WrongSecret;
            ulong old = transportsBySeat[seat];
            if (old == newTransport) return ReclaimError.None; // A repeated attempt from the connection that already holds the seat.
            if (seatsByTransport.ContainsKey(newTransport)) return ReclaimError.AlreadySeated;
            if (isConnected != null && isConnected(old)) return ReclaimError.SeatStillConnected;
            seatsByTransport.Remove(old);
            seatsByTransport[newTransport] = seat;
            transportsBySeat[seat] = newTransport;
            secrets[seat] = newSecret();
            return ReclaimError.None;
        }

        /// <summary>Compares without stopping at the first difference, so timing reveals nothing about the secret.</summary>
        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length) return false;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
            return difference == 0;
        }

        private static string RandomSecret()
        {
            var bytes = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }
    }
}
