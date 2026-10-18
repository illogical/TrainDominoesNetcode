using System;
using System.Collections.Generic;
using System.Linq;
using TrainDominoesRulesEngine;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>
    /// Frozen match seats and the transport identity currently bound to each one.
    /// Rule state, outcome caches and projections key on seats, never on transport IDs,
    /// so a future authenticated reconnect can rebind a seat without changing its history.
    /// </summary>
    public sealed class SeatRegistry
    {
        private readonly Dictionary<ulong, PlayerId> seatsByTransport;
        private readonly Dictionary<PlayerId, ulong> transportsBySeat;

        public SeatRegistry(IEnumerable<ulong> transportIds)
        {
            var ids = transportIds?.OrderBy(id => id).ToArray() ?? throw new ArgumentNullException(nameof(transportIds));
            if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Duplicate transport identity.");
            Seats = ids.Select((id, index) => new PlayerId(index)).ToArray();
            seatsByTransport = ids.Select((id, index) => new { id, seat = Seats[index] }).ToDictionary(p => p.id, p => p.seat);
            transportsBySeat = seatsByTransport.ToDictionary(p => p.Value, p => p.Key);
        }

        /// <summary>Seats in fixed seat order.</summary>
        public IReadOnlyList<PlayerId> Seats { get; }

        /// <summary>Transport identities currently bound to a seat, in seat order.</summary>
        public IEnumerable<ulong> Transports => Seats.Select(seat => transportsBySeat[seat]);

        public bool TryGetSeat(ulong transport, out PlayerId seat) => seatsByTransport.TryGetValue(transport, out seat);

        public ulong TransportFor(PlayerId seat) => transportsBySeat[seat];
    }
}
