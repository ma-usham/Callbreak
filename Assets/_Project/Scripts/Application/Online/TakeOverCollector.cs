using System;
using System.Collections.Generic;
using Callbreak.Application.Rooms;
using Callbreak.Core.Match;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The new host's side of a hand-over until its table is rebuilt (ADR 030): it takes the players'
    /// reports over the room's new connection, checks that each comes from the person in that seat
    /// (its room identity, as the lost host said), and says when everyone it waits for has reported
    /// or the wait is over. Nobody is answered before the table is rebuilt; the room then seats them
    /// all at once. A newer connection of the same player replaces the older one.
    /// </summary>
    internal sealed class TakeOverCollector : IDisposable
    {
        private readonly IHostLink _link;
        private readonly SuccessionPackage _package;
        private readonly Seat _ownSeat;
        private readonly HashSet<Seat> _expected;
        private readonly double _until;
        private readonly Dictionary<Seat, Arrival> _arrivals = new Dictionary<Seat, Arrival>();
        private bool _released;

        /// <param name="ownSeat">The seat of the player taking over: nobody over the network takes it.</param>
        /// <param name="expected">The seats whose players the rebuild waits for.</param>
        /// <param name="until">When the wait is over, whoever has reported by then.</param>
        public TakeOverCollector(IHostLink link, SuccessionPackage package, Seat ownSeat, IEnumerable<Seat> expected, double until)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _ownSeat = ownSeat;
            _expected = new HashSet<Seat>(expected);
            _until = until;
            _link.PeerDisconnected += OnPeerDisconnected;
            _link.Received += OnReceived;
        }

        /// <summary>The seats the rebuild waits for whose players have not reported yet.</summary>
        public int Missing
        {
            get
            {
                int missing = 0;
                foreach (Seat seat in _expected)
                {
                    if (!_arrivals.TryGetValue(seat, out Arrival arrival) || arrival.Report == null)
                    {
                        missing++;
                    }
                }

                return missing;
            }
        }

        /// <summary>True once anyone is connected with its seat known.</summary>
        public bool AnyoneHere => _arrivals.Count > 0;

        /// <summary>The reports that came, one per seat.</summary>
        public IReadOnlyList<SeatReport> Reports
        {
            get
            {
                var reports = new List<SeatReport>();
                foreach (Arrival arrival in _arrivals.Values)
                {
                    if (arrival.Report != null)
                    {
                        reports.Add(arrival.Report);
                    }
                }

                return reports;
            }
        }

        /// <summary>Every player connected with its seat known, reported or not, by its connection.</summary>
        public IReadOnlyList<KeyValuePair<PeerId, int>> Present
        {
            get
            {
                var present = new List<KeyValuePair<PeerId, int>>();
                foreach (KeyValuePair<Seat, Arrival> pair in _arrivals)
                {
                    present.Add(new KeyValuePair<PeerId, int>(pair.Value.Peer, (int)pair.Key));
                }

                return present;
            }
        }

        /// <summary>Every seat it waits for has reported, or the wait is over.</summary>
        public bool IsComplete(double now) => now >= _until || Missing == 0;

        /// <summary>The room takes the connection over: nothing is heard here any more.</summary>
        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _link.PeerDisconnected -= OnPeerDisconnected;
            _link.Received -= OnReceived;
        }

        public void Dispose() => Release();

        private void OnPeerDisconnected(PeerId peer)
        {
            foreach (KeyValuePair<Seat, Arrival> pair in _arrivals)
            {
                if (pair.Value.Peer == peer)
                {
                    _arrivals.Remove(pair.Key);
                    return;
                }
            }
        }

        private void OnReceived(PeerId peer, byte[] payload)
        {
            if (_released || payload == null || payload.Length == 0 || payload.Length > Protocol.MaxReportBytes)
            {
                return;
            }

            try
            {
                var reader = new WireReader(payload);
                var kind = (ClientMessageKind)reader.Byte();
                if (kind != ClientMessageKind.Report && kind != ClientMessageKind.Hello)
                {
                    // Nothing else counts before the table is rebuilt.
                    return;
                }

                ushort version = reader.UInt16();
                if (version != Protocol.Version)
                {
                    Turn(peer, RoomError.UpdateRequired);
                    return;
                }

                HelloMessage hello = HelloMessage.ReadAfterVersion(reader, version);
                if (!TrySeatOf(hello.Identity, out Seat seat))
                {
                    // Not one of the people at this table, or someone claiming this device's own seat.
                    Turn(peer, RoomError.Unknown);
                    return;
                }

                SeatReport report = null;
                if (kind == ClientMessageKind.Report)
                {
                    uint seq = reader.UInt32();
                    PlayerView view = WireCodec.ReadPlayerView(reader);
                    if (view.Viewer == seat)
                    {
                        report = new SeatReport(seat, seq, view);
                    }
                }

                if (_arrivals.TryGetValue(seat, out Arrival before) && before.Peer != peer)
                {
                    // GDD §27: a second connection of the same player replaces the first.
                    Turn(before.Peer, RoomError.Replaced);
                }

                _arrivals[seat] = new Arrival(peer, report);
            }
            catch (WireFormatException)
            {
                // A message that cannot be read counts for nothing.
            }
        }

        private bool TrySeatOf(string identity, out Seat seat)
        {
            seat = default;
            if (string.IsNullOrEmpty(identity))
            {
                return false;
            }

            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (s != (int)_ownSeat && _package.IdentityOf((Seat)s) == identity)
                {
                    seat = (Seat)s;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Tells <paramref name="peer"/> why it is not seated, and lets go of it.</summary>
        private void Turn(PeerId peer, RoomError error)
        {
            try
            {
                _link.Send(peer, error == RoomError.Replaced ? HostMessages.RoomClosed(error) : HostMessages.Rejected(error));
                _link.Disconnect(peer);
            }
            catch (Exception)
            {
                // The connection is going anyway.
            }
        }

        private sealed class Arrival
        {
            public Arrival(PeerId peer, SeatReport report)
            {
                Peer = peer;
                Report = report;
            }

            public PeerId Peer { get; }

            /// <summary>What the player brought; null for a plain hello.</summary>
            public SeatReport Report { get; }
        }
    }
}
