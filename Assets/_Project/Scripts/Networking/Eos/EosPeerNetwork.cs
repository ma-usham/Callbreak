using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Platform;
using PlayEveryWare.EpicOnlineServices;
using Debug = UnityEngine.Debug;
using SystemInfo = UnityEngine.SystemInfo;

namespace Callbreak.Networking.Eos
{
    /// <summary>
    /// <see cref="IPeerNetwork"/> over Epic Online Services, through the plugin's EOSManager (started
    /// by <see cref="EosPlatform.Start"/>, which ticks the SDK every frame).
    /// <list type="bullet">
    /// <item>Sign-in: EOS Connect with a Device ID, so a player needs no Epic account. The device id
    /// is made when the login finds none, and a new player is made on the continuance token.
    /// <see cref="LocalAddress"/> is the Product User ID.</item>
    /// <item>Directory: EOS Lobbies. A host lists a room as a public, advertised lobby in this
    /// protocol version's bucket, with the room code in its CODE attribute and no presence,
    /// invites, host migration or voice room. A lookup searches the bucket for that code and answers
    /// the lobby owner's Product User ID.</item>
    /// <item>Quick Match tables (<see cref="IQuickTableDirectory"/>): lobbies of the same kind in a
    /// bucket of their own, so a private room never shows among them. Each lobby has the public
    /// attributes CODE, FREE (its free seats; 0 hides it from searches) and OPENED (when its host
    /// opened it, UTC ms). A search finds tables with free seats, oldest first.</item>
    /// <item>Packets: EOS P2P, reliable and ordered, on channel 0 of a socket named after the
    /// channel, with NAT traversal and Epic's relays as the fallback.</item>
    /// </list>
    /// <para>
    /// Everything runs on the main thread. The SDK calls back while EOSManager ticks it, and those
    /// callbacks only note what happened: the events are raised from <see cref="TryReceive"/>, which
    /// the owner drains every frame, so no handler runs inside the SDK. They come out in order with
    /// the packets, and everything that arrived on a connection comes out before its
    /// <see cref="Disconnected"/>. Every task completes with its continuations posted. No task
    /// faults, and a cancelled call answers false or Unknown. Nothing logged here names a player, a
    /// room code or a credential.
    /// </para>
    /// </summary>
    public sealed class EosPeerNetwork : IPeerNetwork, IQuickTableDirectory, IDisposable
    {
        /// <summary>
        /// How long a close of this side's may wait for the SDK to report it. After that,
        /// <see cref="Disconnected"/> is raised anyway, for a connection that never opened, say.
        /// </summary>
        public const double CloseConfirmSeconds = 5.0;

        /// <summary>The lobby attribute that holds a room's code.</summary>
        private const string CodeKey = "CODE";

        /// <summary>The lobby attribute that holds a Quick Match table's free seats. Searches skip 0.</summary>
        private const string FreeKey = "FREE";

        /// <summary>The lobby attribute that holds when a Quick Match table opened, by its host's clock (UTC ms since 1970).</summary>
        private const string OpenedKey = "OPENED";

        /// <summary>
        /// The lobby attribute that holds the generation a private room is listed at (ADR 030: a player
        /// who took the room over from a lost host lists it one generation up). A lobby without it is
        /// generation 0, as every lobby made before it existed.
        /// </summary>
        private const string GenerationKey = "GEN";

        /// <summary>
        /// Nobody joins a listing: guests look the code up (or find the table) and connect straight
        /// to the host. So the capacity only has to leave room in it, in case a search skips full
        /// lobbies. It is a table's four seats.
        /// </summary>
        private const uint ListingCapacity = 4;

        /// <summary>How many lobbies a lookup reads. More than one only when two hosts drew the same code.</summary>
        private const uint SearchLimit = 8;

        /// <summary>How many open Quick Match tables a search returns at most.</summary>
        private const uint QuickSearchLimit = 10;

        /// <summary>How often an update of a table's free seats is tried before it is given up.</summary>
        private const int FreeUpdateAttempts = 3;

        /// <summary>The pause before a failed update of a table's free seats is tried again.</summary>
        private const double FreeRetrySeconds = 1.0;

        /// <summary>One pump reads at most this many packets from the SDK; the rest wait for the next.</summary>
        private const int DrainLimit = 256;

        /// <summary>One TryReceive raises at most this many events before it gives up for the frame.</summary>
        private const int MaxItemsPerCall = 10000;

        /// <summary>The name a player signs in with when the profile has none.</summary>
        private const string DefaultDisplayName = "Player";

        /// <summary>
        /// Listings of other protocol versions are never found, so an old build and a new one never
        /// meet in a room.
        /// </summary>
        private static readonly string Bucket = "callbreak-rooms:v" + Protocol.Version;

        /// <summary>The bucket of Quick Match tables, apart from private rooms (and from other protocol versions).</summary>
        private static readonly string QuickBucket = "callbreak-quick:v" + Protocol.Version;

        private readonly Func<string> _displayName;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>Events noted in SDK callbacks, not yet put in order with the packets.</summary>
        private readonly Queue<Item> _pending = new Queue<Item>();

        /// <summary>Events and packets, in the order <see cref="TryReceive"/> hands them over.</summary>
        private readonly Queue<Item> _ready = new Queue<Item>();

        /// <summary>The packets of one pump, while the events are put in order with them.</summary>
        private readonly List<Item> _drained = new List<Item>();

        /// <summary>Private rooms this device lists, by room code.</summary>
        private readonly Dictionary<string, Listing> _listings = new Dictionary<string, Listing>(StringComparer.Ordinal);

        /// <summary>Quick Match tables this device lists, by room code.</summary>
        private readonly Dictionary<string, Listing> _quickListings = new Dictionary<string, Listing>(StringComparer.Ordinal);

        /// <summary>
        /// When each of this device's tables opened, until it is unlisted. A table listed again after
        /// its listing was lost keeps its age, and with it its place among the oldest.
        /// </summary>
        private readonly Dictionary<string, long> _quickOpened = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>Both kinds, by lobby id, for the lobby service's news.</summary>
        private readonly Dictionary<string, Listing> _listingsByLobby = new Dictionary<string, Listing>(StringComparer.Ordinal);
        private readonly Dictionary<Link, LinkState> _links = new Dictionary<Link, LinkState>();

        /// <summary>Connections this side closed that the SDK has not reported closed yet, with the time it may take.</summary>
        private readonly Dictionary<Link, double> _closing = new Dictionary<Link, double>();

        /// <summary>Connections whose <see cref="Disconnected"/> is noted but not raised yet. Nothing is sent to them.</summary>
        private readonly HashSet<Link> _announcing = new HashSet<Link>();

        /// <summary>The connections this instance allowed (Accept or Send), which its Dispose closes.</summary>
        private readonly HashSet<Link> _allowed = new HashSet<Link>();

        private ProductUserId _localUser;
        private string _localAddress;
        private Task<bool> _signingIn;
        private RoomError _signInError = RoomError.Unknown;
        private SocketId _receiveSocket;
        private NetworkStatus _networkStatus = NetworkStatus.Online;
        private ulong _connectionRequests;
        private ulong _connectionsOpened;
        private ulong _connectionsClosed;
        private ulong _memberStatus;
        private ulong _loginStatus;
        private bool _disposed;

        /// <summary>
        /// Completed when this network shuts down: whatever waits for the SDK stops waiting then, and
        /// answers Unknown or false without another call into the SDK.
        /// </summary>
        private readonly TaskCompletionSource<bool> _closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <param name="displayName">
        /// The player's name, read at each sign-in (EOS keeps it as the Connect user's display name).
        /// "Player" when it is null or blank.
        /// </param>
        public EosPeerNetwork(Func<string> displayName)
        {
            _displayName = displayName;

            // Shut down before the plugin shuts EOS down. It releases the platform from
            // Application.quitting, which in the Editor runs as Play mode ends, and an SDK call after
            // that crashes the process. Play mode's end (in the Editor) and wantsToQuit (in a player)
            // come before it; quitting itself is the last chance.
            UnityEngine.Application.wantsToQuit += OnWantsToQuit;
            UnityEngine.Application.quitting += Dispose;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        private enum LinkState : byte
        {
            /// <summary>The peer asked; nothing is allowed yet.</summary>
            Requested,

            /// <summary>This side allowed it (Accept, or a packet was sent); not open yet.</summary>
            Accepted,

            /// <summary>The SDK reported it open.</summary>
            Open,
        }

        private enum ItemKind : byte
        {
            Packet,
            ConnectionRequested,
            Connected,
            Disconnected,
            ListingLost,
        }

        public event Action<string, string> ConnectionRequested;

        public event Action<string, string> Connected;

        public event Action<string, string> Disconnected;

        public event Action<string> ListingLost;

        /// <summary>The SDK's largest packet: 1170 bytes.</summary>
        public int MaxPacketSize => P2PInterface.MAX_PACKET_SIZE;

        public string LocalAddress => _localAddress;

        /// <summary>
        /// The last thing the SDK refused, and its result code, e.g. "the sign-in failed
        /// (InvalidCredentials)": for diagnostics (EosCheck). It names no player, room or credential.
        /// Null until something fails.
        /// </summary>
        public string LastFailure { get; private set; }

        private static PlatformInterface Platform => EOSManager.Instance.GetEOSPlatformInterface();

        private double Now => _clock.Elapsed.TotalSeconds;

        /// <summary>
        /// True while calls into the SDK on <paramref name="platform"/>'s objects are safe: this
        /// network has not shut down, and EOS still runs on that very platform. After every await,
        /// pause or callback, the code checks this before it touches the SDK again. When it fails, the
        /// call is skipped and a handle is left to leak, rather than released into a platform that is
        /// gone.
        /// </summary>
        private bool Alive(PlatformInterface platform) => !_disposed && platform != null && ReferenceEquals(Platform, platform);

        public Task<bool> SignInAsync(CancellationToken ct)
        {
            if (_disposed)
            {
                return Task.FromResult(false);
            }

            if (IsSignedIn())
            {
                return Task.FromResult(true);
            }

            if (_signingIn == null || _signingIn.IsCompleted)
            {
                _signingIn = SignInCoreAsync();
            }

            return Abandonable(_signingIn, ct, false);
        }

        /// <summary>
        /// Lists the code. A call that is cancelled before it answers None leaves nothing listed: a
        /// lobby made for it meanwhile is destroyed.
        /// </summary>
        public Task<RoomError> ListAsync(string roomCode, CancellationToken ct) => ListAsync(roomCode, 0, ct);

        /// <summary>
        /// Lists the code at <paramref name="generation"/> (ADR 030). Another device's lobby of the
        /// code at an earlier generation is no obstacle; one at this generation or a later one is.
        /// </summary>
        public Task<RoomError> ListAsync(string roomCode, int generation, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(roomCode))
            {
                throw new ArgumentException("A room code is required.", nameof(roomCode));
            }

            if (_disposed)
            {
                return Task.FromResult(RoomError.Unknown);
            }

            if (_listings.TryGetValue(roomCode, out Listing listing))
            {
                if (listing.Generation == generation)
                {
                    return listing.Live ? Task.FromResult(RoomError.None) : ListedAsync(listing, ct);
                }

                // This device lists one generation of a code at a time.
                Withdraw(listing);
            }

            listing = new Listing(roomCode, false) { Generation = generation };
            _listings.Add(roomCode, listing);
            listing.Making = MakeListingAsync(listing, ct);
            return ListedAsync(listing, ct);
        }

        public void Unlist(string roomCode)
        {
            if (roomCode != null && _listings.TryGetValue(roomCode, out Listing listing))
            {
                // A listing still being made is destroyed by its making as soon as its lobby exists.
                Withdraw(listing);
            }
        }

        public Task<PeerLookup> LookUpAsync(string roomCode, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(roomCode))
            {
                throw new ArgumentException("A room code is required.", nameof(roomCode));
            }

            if (_disposed)
            {
                return Task.FromResult(new PeerLookup(null, RoomError.Unknown));
            }

            return Abandonable(LookUpCoreAsync(roomCode, ct), ct, new PeerLookup(null, RoomError.Unknown));
        }

        /// <summary>
        /// Lists this device's Quick Match table, opened at <paramref name="openedAtUnixMs"/> (its
        /// host's tap, by this device's clock). A code listed again after its lobby was lost keeps the
        /// time it was first listed with. A call that is cancelled before it answers None leaves
        /// nothing listed, as with <see cref="ListAsync"/>. Listing a table that is listed already
        /// only sets its free seats.
        /// </summary>
        public Task<RoomError> ListQuickAsync(string roomCode, int freeSeats, long openedAtUnixMs, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(roomCode))
            {
                throw new ArgumentException("A room code is required.", nameof(roomCode));
            }

            if (freeSeats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(freeSeats), "Free seats cannot be negative.");
            }

            if (_disposed)
            {
                return Task.FromResult(RoomError.Unknown);
            }

            if (_quickListings.TryGetValue(roomCode, out Listing listing))
            {
                listing.Free = freeSeats;
                PushFree(listing);
                return listing.Live ? Task.FromResult(RoomError.None) : ListedAsync(listing, ct);
            }

            if (!_quickOpened.TryGetValue(roomCode, out long openedAt))
            {
                openedAt = openedAtUnixMs;
                _quickOpened[roomCode] = openedAt;
            }

            listing = new Listing(roomCode, true) { Free = freeSeats, OpenedAtUnixMs = openedAt };
            _quickListings.Add(roomCode, listing);
            listing.Making = MakeListingAsync(listing, ct);
            return ListedAsync(listing, ct);
        }

        /// <summary>
        /// Sets the table's free seats in the directory. The latest number wins: while one update is on
        /// its way, a newer number waits for it and follows. 0 hides the table from searches, as fast as
        /// the directory allows (EosCheck measures it).
        /// </summary>
        public void SetQuickFreeSeats(string roomCode, int freeSeats)
        {
            if (freeSeats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(freeSeats), "Free seats cannot be negative.");
            }

            if (roomCode != null && _quickListings.TryGetValue(roomCode, out Listing listing))
            {
                listing.Free = freeSeats;
                PushFree(listing);
            }
        }

        public void UnlistQuick(string roomCode)
        {
            if (roomCode != null && _quickListings.TryGetValue(roomCode, out Listing listing))
            {
                // A table still being listed is destroyed by its making as soon as its lobby exists.
                Withdraw(listing);
            }
        }

        /// <summary>
        /// The open tables with free seats, oldest first, at most ten. This device's own table is among
        /// them while it is open (its <see cref="QuickTable.HostAddress"/> is <see cref="LocalAddress"/>).
        /// None with no tables is a normal answer.
        /// </summary>
        public Task<QuickTableSearch> FindQuickTablesAsync(CancellationToken ct)
        {
            if (_disposed)
            {
                return Task.FromResult(new QuickTableSearch(null, RoomError.Unknown));
            }

            return Abandonable(FindQuickCoreAsync(ct), ct, new QuickTableSearch(null, RoomError.Unknown));
        }

        /// <summary>
        /// Allows the connection. Called again for a pair this side is still closing, it starts afresh:
        /// that close raises no <see cref="Disconnected"/>, and whatever is still queued from the old
        /// connection is dropped.
        /// </summary>
        public void Accept(string address, string channel)
        {
            SocketId socket = SocketOf(channel);
            CheckAddress(address);
            var link = new Link(address, channel);
            bool superseded = _closing.Remove(link);
            if (_announcing.Remove(link))
            {
                superseded = true;
            }

            if (superseded)
            {
                DropQueued(link, true);
            }

            if (!TryP2P(out P2PInterface p2p))
            {
                return;
            }

            var options = new AcceptConnectionOptions { LocalUserId = _localUser, RemoteUserId = ProductUserId.FromString(address), SocketId = socket };
            Result result = p2p.AcceptConnection(ref options);
            if (result != Result.Success)
            {
                Failed("a connection could not be allowed", result);
                return;
            }

            _allowed.Add(link);
            if (!_links.TryGetValue(link, out LinkState state) || state == LinkState.Requested)
            {
                _links[link] = LinkState.Accepted;
            }
        }

        /// <summary>
        /// Closes the connection, and drops what is still queued from it, as the SDK drops its own.
        /// For a connection this side allowed (Accept or Send), <see cref="Disconnected"/> follows
        /// once the SDK reports it closed, or after <see cref="CloseConfirmSeconds"/>. Nothing is sent
        /// to the pair until then. A request that was never allowed just goes, with no event.
        /// </summary>
        public void Close(string address, string channel)
        {
            SocketId socket = SocketOf(channel);
            CheckAddress(address);
            var link = new Link(address, channel);
            _allowed.Remove(link);
            DropQueued(link, false);
            if (_links.TryGetValue(link, out LinkState state))
            {
                _links.Remove(link);
                if (state != LinkState.Requested && !_announcing.Contains(link))
                {
                    _closing[link] = Now + CloseConfirmSeconds;
                }
            }

            if (!TryP2P(out P2PInterface p2p))
            {
                return;
            }

            var options = new CloseConnectionOptions { LocalUserId = _localUser, RemoteUserId = ProductUserId.FromString(address), SocketId = socket };
            Result result = p2p.CloseConnection(ref options);
            if (result != Result.Success)
            {
                Failed("a connection could not be closed", result);
            }
        }

        public void Send(string address, string channel, byte[] packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length > MaxPacketSize)
            {
                throw new ArgumentException("A packet holds at most " + MaxPacketSize + " bytes.", nameof(packet));
            }

            SocketId socket = SocketOf(channel);
            CheckAddress(address);
            var link = new Link(address, channel);
            if (!TryP2P(out P2PInterface p2p) || _closing.ContainsKey(link) || _announcing.Contains(link))
            {
                // A connection that is closing, or whose drop is not told yet, takes nothing more: a
                // packet would open it again behind the back of the Disconnected on its way.
                return;
            }

            var options = new SendPacketOptions
            {
                LocalUserId = _localUser,
                RemoteUserId = ProductUserId.FromString(address),
                SocketId = socket,
                Channel = 0,
                Data = new ArraySegment<byte>(packet),
                AllowDelayedDelivery = true,
                Reliability = PacketReliability.ReliableOrdered,
                DisableAutoAcceptConnection = false,
            };
            Result result = p2p.SendPacket(ref options);
            if (result == Result.Success)
            {
                // Sending allows the connection, as Accept does.
                _allowed.Add(link);
                if (!_links.TryGetValue(link, out LinkState state) || state == LinkState.Requested)
                {
                    _links[link] = LinkState.Accepted;
                }

                return;
            }

            Failed("a packet could not be sent", result);
            if (result != Result.InvalidParameters && result != Result.InvalidUser)
            {
                // A reliable, ordered stream with a packet missing is worse than none: end it, so
                // the room logic hears of it.
                Close(address, channel);
            }
        }

        /// <summary>
        /// Hands over the oldest packet, raising on the way every event that comes before it. When
        /// nothing is queued, it first takes what the SDK received and noted since the last time.
        /// </summary>
        public bool TryReceive(out string address, out string channel, out byte[] packet)
        {
            for (int i = 0; i < MaxItemsPerCall && !_disposed; i++)
            {
                if (_ready.Count == 0)
                {
                    Pump();
                    if (_ready.Count == 0)
                    {
                        break;
                    }
                }

                Item item = _ready.Dequeue();
                if (item.Kind == ItemKind.Packet)
                {
                    packet = new byte[item.Length];
                    Buffer.BlockCopy(item.Buffer, 0, packet, 0, item.Length);
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                    address = item.Address;
                    channel = item.Channel;
                    return true;
                }

                Raise(item);
            }

            address = null;
            channel = null;
            packet = null;
            return false;
        }

        /// <summary>
        /// Shuts this network down. It runs early, by itself, when Play mode ends (in the Editor) or
        /// the app wants to quit, so it comes before the plugin releases the platform; the owner's
        /// Dispose later finds it done.
        /// <list type="bullet">
        /// <item>While EOS still runs, it takes this device's listings down, closes the connections
        /// this instance allowed, and removes every notification. Connections it only heard of are
        /// left alone (another instance on the same player, such as EosCheck, may own them).</item>
        /// <item>Once EOS is gone, it touches the SDK no more.</item>
        /// <item>Whatever still waits for the SDK then answers Unknown or false.</item>
        /// </list>
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UnityEngine.Application.wantsToQuit -= OnWantsToQuit;
            UnityEngine.Application.quitting -= Dispose;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            PlatformInterface platform = Platform;
            if (Alive(platform))
            {
                foreach (Link link in new List<Link>(_allowed))
                {
                    Close(link.Address, link.Channel);
                }

                foreach (Listing listing in AllListings())
                {
                    Withdraw(listing);
                }

                StopListeningToPeers(platform.GetP2PInterface());
                if (_memberStatus != Common.INVALID_NOTIFICATIONID)
                {
                    platform.GetLobbyInterface()?.RemoveNotifyLobbyMemberStatusReceived(_memberStatus);
                }

                if (_loginStatus != Common.INVALID_NOTIFICATIONID)
                {
                    platform.GetConnectInterface()?.RemoveNotifyLoginStatusChanged(_loginStatus);
                }
            }
            else
            {
                // EOS is gone already, and its lobbies, connections and notifications with it.
                foreach (Listing listing in AllListings())
                {
                    Forget(listing);
                }
            }

            _disposed = true;
            _closed.TrySetResult(true);
            _connectionRequests = Common.INVALID_NOTIFICATIONID;
            _connectionsOpened = Common.INVALID_NOTIFICATIONID;
            _connectionsClosed = Common.INVALID_NOTIFICATIONID;
            _memberStatus = Common.INVALID_NOTIFICATIONID;
            _loginStatus = Common.INVALID_NOTIFICATIONID;
            ReturnBuffers(_ready);
            ReturnBuffers(_drained);
            _ready.Clear();
            _drained.Clear();
            _pending.Clear();
            _links.Clear();
            _closing.Clear();
            _announcing.Clear();
            _allowed.Clear();
            ConnectionRequested = null;
            Connected = null;
            Disconnected = null;
            ListingLost = null;
        }

        /// <summary>Answers <paramref name="whenCanceled"/> as soon as <paramref name="ct"/> is cancelled; the work itself goes on.</summary>
        private static async Task<T> Abandonable<T>(Task<T> work, CancellationToken ct, T whenCanceled)
        {
            if (!ct.CanBeCanceled || work.IsCompleted)
            {
                return await work;
            }

            if (ct.IsCancellationRequested)
            {
                return whenCanceled;
            }

            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => canceled.TrySetResult(true)))
            {
                Task first = await Task.WhenAny(work, canceled.Task);
                return first == work ? await work : whenCanceled;
            }
        }

        /// <summary>
        /// Whether an SDK answer is the last one. OperationWillRetry means the same callback comes again.
        /// This is decided here, without asking the SDK, because callbacks may run while EOS shuts down.
        /// </summary>
        private static bool IsComplete(Result result) => result != Result.OperationWillRetry;

        /// <summary>
        /// The SDK's failures as the room logic knows them: no network is Offline, the rest Unknown.
        /// The network's state is the one last read on the main loop; this runs inside callbacks too,
        /// where the SDK must not be asked.
        /// </summary>
        private RoomError ErrorOf(Result result)
        {
            switch (result)
            {
                case Result.NoConnection:
                case Result.NetworkDisconnected:
                case Result.TimedOut:
                    return RoomError.Offline;
            }

            return _networkStatus != NetworkStatus.Online ? RoomError.Offline : RoomError.Unknown;
        }

        /// <summary>
        /// Waits for an SDK answer, or until this network shuts down: then <paramref name="whenClosed"/>
        /// comes back at once. The caller checks <see cref="Alive"/> before it touches the SDK again,
        /// so an answer that is late, or never comes, costs nothing.
        /// </summary>
        private async Task<T> Answer<T>(Task<T> answer, T whenClosed)
        {
            if (!answer.IsCompleted)
            {
                await Task.WhenAny(answer, _closed.Task);
            }

            return answer.IsCompleted && !answer.IsFaulted && !answer.IsCanceled ? answer.Result : whenClosed;
        }

        private bool OnWantsToQuit()
        {
            // Nothing in this game turns a quit down, so the app does go now: stop before EOS does.
            Dispose();
            return true;
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            // Play mode's end comes before Application.quitting, from which the plugin shuts EOS down.
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Dispose();
            }
        }
#endif

        /// <summary>
        /// The channel as an EOS socket name: 1 to 32 characters from A-Z, a-z, 0-9, '-', '_', '+',
        /// '=', '.' and space. Anything else is the caller's mistake.
        /// </summary>
        private static SocketId SocketOf(string channel)
        {
            if (string.IsNullOrEmpty(channel) || channel.Length >= P2PInterface.SOCKETID_SOCKETNAME_SIZE)
            {
                throw new ArgumentException("An EOS channel is 1 to 32 characters long.", nameof(channel));
            }

            foreach (char c in channel)
            {
                bool allowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                    || c == '-' || c == '_' || c == '+' || c == '=' || c == '.' || c == ' ';
                if (!allowed)
                {
                    throw new ArgumentException("An EOS channel holds only A-Z, a-z, 0-9, '-', '_', '+', '=', '.' and spaces.", nameof(channel));
                }
            }

            return new SocketId { SocketName = channel };
        }

        /// <summary>An address is required. It becomes a Product User ID only once the SDK is known to run.</summary>
        private static void CheckAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException("An address is required.", nameof(address));
            }
        }

        private static void ReturnBuffers(IEnumerable<Item> items)
        {
            foreach (Item item in items)
            {
                if (item.Kind == ItemKind.Packet)
                {
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                }
            }
        }

        /// <summary>
        /// The player's name for the Connect login, cut (at a character boundary) to the SDK's limit.
        /// The limit is read as UTF-8 bytes, so a name in any script fits.
        /// </summary>
        private string DisplayNameForLogin()
        {
            string name = null;
            try
            {
                name = _displayName?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            name = string.IsNullOrWhiteSpace(name) ? DefaultDisplayName : name.Trim();
            int limit = ConnectInterface.USERLOGININFO_DISPLAYNAME_MAX_LENGTH;
            int bytes = 0;
            int end = 0;
            while (end < name.Length)
            {
                int width = char.IsSurrogatePair(name, end) ? 2 : 1;
                char c = name[end];
                int size = width == 2 ? 4 : c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
                if (bytes + size > limit)
                {
                    break;
                }

                bytes += size;
                end += width;
            }

            return end > 0 ? name.Substring(0, end) : DefaultDisplayName;
        }

        private static string DeviceModel()
        {
            string model = SystemInfo.deviceModel;
            return string.IsNullOrWhiteSpace(model) || model == SystemInfo.unsupportedIdentifier ? "Unknown device" : model;
        }

        private bool IsSignedIn()
        {
            if (_localUser == null)
            {
                return false;
            }

            PlatformInterface platform = Platform;
            return Alive(platform) && platform.GetConnectInterface().GetLoginStatus(_localUser) == LoginStatus.LoggedIn;
        }

        private bool IsLocalUser(ProductUserId user) => user != null && _localAddress != null && user.ToString() == _localAddress;

        private bool TryP2P(out P2PInterface p2p)
        {
            p2p = !_disposed && _localUser != null ? Platform?.GetP2PInterface() : null;
            return p2p != null;
        }

        /// <summary>
        /// Logs in with this device's id. Only when that finds no device id is one made (asking for
        /// one that exists makes the SDK log an error), and only for a new player is one made.
        /// </summary>
        private async Task<bool> SignInCoreAsync()
        {
            try
            {
                PlatformInterface platform = Platform;
                if (!Alive(platform))
                {
                    // EOS is not running: no credentials in its config, it failed to start, or it shut down.
                    _signInError = RoomError.Unknown;
                    return false;
                }

                ConnectInterface connect = platform.GetConnectInterface();
                var shutDown = new LoginCallbackInfo { ResultCode = Result.Canceled };
                string name = DisplayNameForLogin();
                LoginCallbackInfo login = await Answer(LoginAsync(name), shutDown);
                if (!Alive(platform))
                {
                    return false;
                }

                bool newPlayer = login.ResultCode == Result.InvalidUser && login.ContinuanceToken != null;
                if (login.ResultCode != Result.Success && !newPlayer && ErrorOf(login.ResultCode) != RoomError.Offline)
                {
                    // No device id yet (a first start on this device): make it, then log in again.
                    Result first = login.ResultCode;
                    Result device = await Answer(CreateDeviceIdAsync(connect), Result.Canceled);
                    if (!Alive(platform))
                    {
                        return false;
                    }

                    if (device != Result.Success && device != Result.DuplicateNotAllowed)
                    {
                        return SignInFailed("the sign-in failed (" + first + ") and no device id could be made", device);
                    }

                    login = await Answer(LoginAsync(name), shutDown);
                    if (!Alive(platform))
                    {
                        return false;
                    }

                    newPlayer = login.ResultCode == Result.InvalidUser && login.ContinuanceToken != null;
                }

                if (newPlayer)
                {
                    CreateUserCallbackInfo created = await Answer(CreateUserAsync(login.ContinuanceToken), new CreateUserCallbackInfo { ResultCode = Result.Canceled });
                    if (!Alive(platform))
                    {
                        return false;
                    }

                    if (created.ResultCode != Result.Success)
                    {
                        return SignInFailed("the player could not be made", created.ResultCode);
                    }

                    // Only a successful login arms the plugin's refresh of it (it runs out after an hour).
                    login = await Answer(LoginAsync(name), shutDown);
                    if (!Alive(platform))
                    {
                        return false;
                    }
                }

                if (login.ResultCode != Result.Success || login.LocalUserId == null || !login.LocalUserId.IsValid())
                {
                    return SignInFailed("the sign-in failed", login.ResultCode);
                }

                _localUser = login.LocalUserId;
                _localAddress = _localUser.ToString();
                ListenToAccount();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _signInError = RoomError.Unknown;
                return false;
            }
        }

        private bool SignInFailed(string what, Result result)
        {
            _signInError = ErrorOf(result);
            Failed(what, result);
            return false;
        }

        /// <summary>Notes what the SDK refused (<see cref="LastFailure"/>) and warns of it, with the result code only.</summary>
        private void Failed(string what, Result result)
        {
            LastFailure = what + " (" + result + ")";
            Debug.LogWarning("EOS: " + LastFailure + ".");
        }

        /// <summary><see cref="Failed"/>, answered as the room logic knows it.</summary>
        private RoomError FailedAs(string what, Result result)
        {
            Failed(what, result);
            return ErrorOf(result);
        }

        private static Task<Result> CreateDeviceIdAsync(ConnectInterface connect)
        {
            var done = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new CreateDeviceIdOptions { DeviceModel = DeviceModel() };
            connect.CreateDeviceId(ref options, null, (ref CreateDeviceIdCallbackInfo info) =>
            {
                if (IsComplete(info.ResultCode))
                {
                    done.TrySetResult(info.ResultCode);
                }
            });
            return done.Task;
        }

        /// <summary>Logs in through the plugin, so its EOSManager knows the player and refreshes the login before it runs out.</summary>
        private static Task<LoginCallbackInfo> LoginAsync(string displayName)
        {
            var done = new TaskCompletionSource<LoginCallbackInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            EOSManager.Instance.StartConnectLoginWithDeviceToken(displayName, info =>
            {
                if (IsComplete(info.ResultCode))
                {
                    done.TrySetResult(info);
                }
            });
            return done.Task;
        }

        private static Task<CreateUserCallbackInfo> CreateUserAsync(ContinuanceToken token)
        {
            var done = new TaskCompletionSource<CreateUserCallbackInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            EOSManager.Instance.CreateConnectUserWithContinuanceToken(token, info =>
            {
                if (IsComplete(info.ResultCode))
                {
                    done.TrySetResult(info);
                }
            });
            return done.Task;
        }

        /// <summary>
        /// Starts listening for this player: the lobbies (a listing lost), the login (it ran out),
        /// and the connections. The first two are added once, the connections on every sign-in.
        /// </summary>
        private void ListenToAccount()
        {
            PlatformInterface platform = Platform;
            if (!Alive(platform))
            {
                return;
            }

            if (_memberStatus == Common.INVALID_NOTIFICATIONID)
            {
                var options = new AddNotifyLobbyMemberStatusReceivedOptions();
                _memberStatus = platform.GetLobbyInterface().AddNotifyLobbyMemberStatusReceived(ref options, null, OnMemberStatus);
            }

            if (_loginStatus == Common.INVALID_NOTIFICATIONID)
            {
                var options = new AddNotifyLoginStatusChangedOptions();
                _loginStatus = platform.GetConnectInterface().AddNotifyLoginStatusChanged(ref options, null, OnLoginStatusChanged);
            }

            _networkStatus = platform.GetNetworkStatus();
            ListenToPeers(platform.GetP2PInterface());
        }

        private void ListenToPeers(P2PInterface p2p)
        {
            if (p2p == null || _localUser == null)
            {
                return;
            }

            StopListeningToPeers(p2p);

            // Direct connections where NAT traversal finds a way, Epic's relays where it does not
            // (the SDK's default, said out loud).
            var relays = new SetRelayControlOptions { RelayControl = RelayControl.AllowRelays };
            Result relayResult = p2p.SetRelayControl(ref relays);
            if (relayResult != Result.Success)
            {
                Failed("relays could not be allowed", relayResult);
            }

            var requests = new AddNotifyPeerConnectionRequestOptions { LocalUserId = _localUser };
            _connectionRequests = p2p.AddNotifyPeerConnectionRequest(ref requests, null, OnConnectionRequest);
            var opened = new AddNotifyPeerConnectionEstablishedOptions { LocalUserId = _localUser };
            _connectionsOpened = p2p.AddNotifyPeerConnectionEstablished(ref opened, null, OnConnectionEstablished);
            var closed = new AddNotifyPeerConnectionClosedOptions { LocalUserId = _localUser };
            _connectionsClosed = p2p.AddNotifyPeerConnectionClosed(ref closed, null, OnConnectionClosed);
        }

        private void StopListeningToPeers(P2PInterface p2p)
        {
            if (p2p != null)
            {
                if (_connectionRequests != Common.INVALID_NOTIFICATIONID)
                {
                    p2p.RemoveNotifyPeerConnectionRequest(_connectionRequests);
                }

                if (_connectionsOpened != Common.INVALID_NOTIFICATIONID)
                {
                    p2p.RemoveNotifyPeerConnectionEstablished(_connectionsOpened);
                }

                if (_connectionsClosed != Common.INVALID_NOTIFICATIONID)
                {
                    p2p.RemoveNotifyPeerConnectionClosed(_connectionsClosed);
                }
            }

            _connectionRequests = Common.INVALID_NOTIFICATIONID;
            _connectionsOpened = Common.INVALID_NOTIFICATIONID;
            _connectionsClosed = Common.INVALID_NOTIFICATIONID;
        }

        /// <summary>
        /// The SDK forgets who listens for connection requests and openings when the network goes
        /// down. Once it is back, listen again (EOS P2P docs).
        /// </summary>
        private void FollowNetworkStatus()
        {
            PlatformInterface platform = Platform;
            if (!Alive(platform))
            {
                return;
            }

            NetworkStatus now = platform.GetNetworkStatus();
            if (now == NetworkStatus.Online && _networkStatus != NetworkStatus.Online)
            {
                ListenToPeers(platform.GetP2PInterface());
            }

            _networkStatus = now;
        }

        /// <summary>
        /// Puts what happened since the last time in order. Closes the SDK did not report in time
        /// count as reported. The packets waiting in the SDK are read, and each noted event is queued
        /// in turn, a Disconnected after the packets of its own connection. The other packets follow.
        /// </summary>
        private void Pump()
        {
            if (_disposed)
            {
                return;
            }

            ExpireCloses();
            FollowNetworkStatus();
            ReadPackets();
            while (_pending.Count > 0)
            {
                Item noted = _pending.Dequeue();
                if (noted.Kind == ItemKind.Disconnected)
                {
                    // What arrived on that connection before it dropped comes first.
                    int kept = 0;
                    for (int i = 0; i < _drained.Count; i++)
                    {
                        Item packet = _drained[i];
                        if (packet.Is(noted.Address, noted.Channel))
                        {
                            _ready.Enqueue(packet);
                        }
                        else
                        {
                            _drained[kept++] = packet;
                        }
                    }

                    _drained.RemoveRange(kept, _drained.Count - kept);
                }

                _ready.Enqueue(noted);
            }

            foreach (Item packet in _drained)
            {
                _ready.Enqueue(packet);
            }

            _drained.Clear();
        }

        private void ExpireCloses()
        {
            if (_closing.Count == 0)
            {
                return;
            }

            double now = Now;
            List<Link> expired = null;
            foreach (KeyValuePair<Link, double> closing in _closing)
            {
                if (now >= closing.Value)
                {
                    (expired ??= new List<Link>()).Add(closing.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            foreach (Link link in expired)
            {
                _closing.Remove(link);
                NoteDisconnected(link);
            }
        }

        /// <summary>Reads what the SDK received into <see cref="_drained"/>, in pooled buffers; TryReceive hands out copies.</summary>
        private void ReadPackets()
        {
            if (!TryP2P(out P2PInterface p2p))
            {
                return;
            }

            var options = new ReceivePacketOptions { LocalUserId = _localUser, MaxDataSizeBytes = (uint)MaxPacketSize, RequestedChannel = null };
            for (int i = 0; i < DrainLimit; i++)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(MaxPacketSize);
                ProductUserId peer = null;
                Result result = p2p.ReceivePacket(ref options, ref peer, ref _receiveSocket, out byte _, new ArraySegment<byte>(buffer, 0, MaxPacketSize), out uint written);
                string channel = result == Result.Success ? _receiveSocket.SocketName : null;
                if (result != Result.Success || peer == null || string.IsNullOrEmpty(channel))
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    if (result != Result.NotFound)
                    {
                        Failed("a packet could not be read", result);
                    }

                    break;
                }

                _drained.Add(Item.Packet(peer.ToString(), channel, buffer, (int)written));
            }
        }

        /// <summary>
        /// Drops what is queued from <paramref name="link"/>'s connection: its packets, and with
        /// <paramref name="andDrop"/> its noted Disconnected too.
        /// </summary>
        private void DropQueued(Link link, bool andDrop)
        {
            Drop(_ready, link, andDrop);
            Drop(_pending, link, andDrop);
        }

        private static void Drop(Queue<Item> queue, Link link, bool andDrop)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                Item item = queue.Dequeue();
                bool dropped = item.Is(link.Address, link.Channel)
                    && (item.Kind == ItemKind.Packet || (andDrop && item.Kind == ItemKind.Disconnected));
                if (!dropped)
                {
                    queue.Enqueue(item);
                }
                else if (item.Kind == ItemKind.Packet)
                {
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                }
            }
        }

        private void Note(ItemKind kind, Link link)
        {
            _pending.Enqueue(Item.Event(kind, link.Address, link.Channel));
        }

        /// <summary>Notes that the connection is gone; nothing is sent to it until that is told.</summary>
        private void NoteDisconnected(Link link)
        {
            _announcing.Add(link);
            Note(ItemKind.Disconnected, link);
        }

        /// <summary>Raises a noted event, outside the SDK; a handler that throws is logged and the next goes ahead.</summary>
        private void Raise(Item item)
        {
            Action<string, string> handler;
            switch (item.Kind)
            {
                case ItemKind.ConnectionRequested:
                    handler = ConnectionRequested;
                    break;
                case ItemKind.Connected:
                    handler = Connected;
                    break;
                case ItemKind.Disconnected:
                    if (!_announcing.Remove(new Link(item.Address, item.Channel)))
                    {
                        // Superseded by an Accept meanwhile.
                        return;
                    }

                    handler = Disconnected;
                    break;
                case ItemKind.ListingLost:
                    try
                    {
                        ListingLost?.Invoke(item.Code);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    return;
                default:
                    return;
            }

            try
            {
                handler?.Invoke(item.Address, item.Channel);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnConnectionRequest(ref OnIncomingConnectionRequestInfo info)
        {
            try
            {
                if (_disposed || !IsLocalUser(info.LocalUserId) || !TryLinkOf(info.RemoteUserId, info.SocketId, out Link link))
                {
                    return;
                }

                SettleUnreportedClose(link);
                if (!_links.ContainsKey(link))
                {
                    _links[link] = LinkState.Requested;
                }

                Note(ItemKind.ConnectionRequested, link);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnConnectionEstablished(ref OnPeerConnectionEstablishedInfo info)
        {
            try
            {
                if (_disposed || !IsLocalUser(info.LocalUserId) || !TryLinkOf(info.RemoteUserId, info.SocketId, out Link link))
                {
                    return;
                }

                SettleUnreportedClose(link);

                // A reconnection after an interruption, or a change of route (direct or relayed), is no news.
                bool wasOpen = _links.TryGetValue(link, out LinkState state) && state == LinkState.Open;
                _links[link] = LinkState.Open;
                if (!wasOpen)
                {
                    Note(ItemKind.Connected, link);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnConnectionClosed(ref OnRemoteConnectionClosedInfo info)
        {
            try
            {
                if (_disposed || !IsLocalUser(info.LocalUserId) || !TryLinkOf(info.RemoteUserId, info.SocketId, out Link link))
                {
                    return;
                }

                if (_closing.Remove(link))
                {
                    NoteDisconnected(link);
                    return;
                }

                // A close of this side's that was settled already (or superseded) concerns no
                // connection there is now.
                if (info.Reason == ConnectionClosedReason.ClosedByLocalUser)
                {
                    return;
                }

                _allowed.Remove(link);
                if (_links.Remove(link))
                {
                    NoteDisconnected(link);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// A connection this side closed is heard of again before the SDK reported the close: the old
        /// one is over, so its Disconnected comes first.
        /// </summary>
        private void SettleUnreportedClose(Link link)
        {
            if (_closing.Remove(link))
            {
                NoteDisconnected(link);
            }
        }

        private void OnMemberStatus(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            try
            {
                if (_disposed || !IsLocalUser(info.TargetUserId))
                {
                    return;
                }

                switch (info.CurrentStatus)
                {
                    case LobbyMemberStatus.Left:
                    case LobbyMemberStatus.Disconnected:
                    case LobbyMemberStatus.Kicked:
                    case LobbyMemberStatus.Closed:
                        break;
                    default:
                        return;
                }

                string lobbyId = info.LobbyId;
                if (lobbyId == null || !_listingsByLobby.TryGetValue(lobbyId, out Listing listing))
                {
                    return;
                }

                Forget(listing);
                _pending.Enqueue(Item.Lost(listing.Code));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnLoginStatusChanged(ref LoginStatusChangedCallbackInfo info)
        {
            try
            {
                if (_disposed || !IsLocalUser(info.LocalUserId) || info.CurrentStatus == LoginStatus.LoggedIn)
                {
                    return;
                }

                SignedOut();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// The login ran out (or was lost): the listings and the connections went with it. That is
        /// told like everything else, and the next <see cref="SignInAsync"/> signs in again. This runs
        /// inside a callback, so it asks nothing of the SDK. The connection notifications stay until
        /// the next sign-in or Dispose replaces them, and with no local player they are ignored.
        /// </summary>
        private void SignedOut()
        {
            _localUser = null;
            _localAddress = null;

            foreach (Link link in new List<Link>(_links.Keys))
            {
                NoteDisconnected(link);
            }

            foreach (Link link in new List<Link>(_closing.Keys))
            {
                NoteDisconnected(link);
            }

            _links.Clear();
            _closing.Clear();
            _allowed.Clear();
            foreach (Listing listing in AllListings())
            {
                if (listing.Live)
                {
                    _pending.Enqueue(Item.Lost(listing.Code));
                }

                Forget(listing);
            }
        }

        /// <summary>A copy of every listing of this device's, private rooms and Quick Match tables.</summary>
        private List<Listing> AllListings()
        {
            var all = new List<Listing>(_listings.Values);
            all.AddRange(_quickListings.Values);
            return all;
        }

        private static bool TryLinkOf(ProductUserId remote, SocketId? socket, out Link link)
        {
            string address = remote != null ? remote.ToString() : null;
            string channel = socket.HasValue ? socket.Value.SocketName : null;
            link = new Link(address, channel);
            return !string.IsNullOrEmpty(address) && !string.IsNullOrEmpty(channel);
        }

        /// <summary>
        /// The answer to a ListAsync caller. One that gave up is told Unknown and will not take the
        /// listing down, so it is withdrawn here: no lobby stays up for it.
        /// </summary>
        private async Task<RoomError> ListedAsync(Listing listing, CancellationToken ct)
        {
            RoomError answer = await Abandonable(listing.Making, ct, RoomError.Unknown);
            if (answer != RoomError.None && ct.IsCancellationRequested)
            {
                Withdraw(listing);
            }

            return answer;
        }

        /// <summary>
        /// Makes the listing's lobby in its bucket, then gives it its attributes in one update. A
        /// private room first checks that no other device lists its code. A Quick Match table skips
        /// that search: its code is drawn at random and never typed, and the search would cost
        /// seconds of the table's countdown.
        /// </summary>
        private async Task<RoomError> MakeListingAsync(Listing listing, CancellationToken ct)
        {
            try
            {
                if (!await SignInAsync(ct))
                {
                    Forget(listing);
                    return ct.IsCancellationRequested ? RoomError.Unknown : _signInError;
                }

                if (Abandoned(listing, ct))
                {
                    return RoomError.Unknown;
                }

                if (!listing.Quick)
                {
                    // A code another device lists already is taken: the caller draws another.
                    Search search = await SearchAsync(listing.Code);
                    if (Abandoned(listing, ct))
                    {
                        return RoomError.Unknown;
                    }

                    if (search.Error == RoomError.None && search.ListedByAnotherThan(_localAddress, listing.Generation))
                    {
                        Forget(listing);
                        return RoomError.AlreadyStarted;
                    }

                    if (search.Error != RoomError.None && search.Error != RoomError.NotFound)
                    {
                        Forget(listing);
                        return search.Error;
                    }
                }

                Made made = await Answer(CreateLobbyAsync(listing.Quick ? QuickBucket : Bucket), new Made(null, RoomError.Unknown));
                if (made.Error != RoomError.None)
                {
                    Forget(listing);
                    return made.Error;
                }

                listing.LobbyId = made.LobbyId;
                _listingsByLobby[made.LobbyId] = listing;
                if (Abandoned(listing, ct))
                {
                    Destroy(listing);
                    return RoomError.Unknown;
                }

                int free = listing.Free;
                RoomError tagged = await PutAttributesAsync(made.LobbyId, AttributesOf(listing, free));
                if (tagged != RoomError.None || Abandoned(listing, ct))
                {
                    Withdraw(listing);
                    return tagged != RoomError.None ? tagged : RoomError.Unknown;
                }

                listing.SentFree = free;
                listing.Live = true;

                // The free seats may have changed while the lobby was being made.
                PushFree(listing);
                return RoomError.None;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Withdraw(listing);
                return RoomError.Unknown;
            }
        }

        /// <summary>The attributes a listing's lobby carries: its code, and a table's free seats and opening time.</summary>
        private static AttributeData[] AttributesOf(Listing listing, int free)
        {
            if (!listing.Quick)
            {
                return listing.Generation > 0
                    ? new[] { Text(CodeKey, listing.Code), Number(GenerationKey, listing.Generation) }
                    : new[] { Text(CodeKey, listing.Code) };
            }

            return new[] { Text(CodeKey, listing.Code), Number(FreeKey, free), Number(OpenedKey, listing.OpenedAtUnixMs) };
        }

        private static AttributeData Text(string key, string value) => new AttributeData { Key = key, Value = value };

        private static AttributeData Number(string key, long value) => new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } };

        /// <summary>
        /// Sends the table's newest number of free seats, unless an update is on its way already: that
        /// one sends it next.
        /// </summary>
        private void PushFree(Listing listing)
        {
            if (listing.Quick && listing.Live && !listing.Withdrawn && !listing.Updating && listing.SentFree != listing.Free)
            {
                _ = UpdateFreeAsync(listing);
            }
        }

        /// <summary>
        /// Updates FREE until the directory holds the newest number, one update at a time. A failed
        /// update is tried again after a pause, a few times. After that the directory keeps the last
        /// number it took, and the room logic still refuses whoever comes to a full table.
        /// </summary>
        private async Task UpdateFreeAsync(Listing listing)
        {
            listing.Updating = true;
            int failures = 0;
            try
            {
                while (!listing.Withdrawn && !_disposed && listing.SentFree != listing.Free)
                {
                    int free = listing.Free;
                    RoomError result = await PutAttributesAsync(listing.LobbyId, Number(FreeKey, free));
                    if (result == RoomError.None)
                    {
                        listing.SentFree = free;
                        failures = 0;
                        continue;
                    }

                    if (++failures >= FreeUpdateAttempts)
                    {
                        break;
                    }

                    // The pause ends early when this network shuts down; the loop then stops.
                    await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(FreeRetrySeconds)), _closed.Task);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                listing.Updating = false;
            }
        }

        /// <summary>
        /// True when nobody wants <paramref name="listing"/> any more: it was taken down, the caller
        /// gave up, or this network was disposed. The listing is forgotten then.
        /// </summary>
        private bool Abandoned(Listing listing, CancellationToken ct)
        {
            if (!listing.Withdrawn && !ct.IsCancellationRequested && Alive(Platform))
            {
                return false;
            }

            Forget(listing);
            return true;
        }

        /// <summary>
        /// Takes the listing down on purpose: forgotten, and its lobby destroyed (now, or by its making
        /// once it exists). A table's age goes with it.
        /// </summary>
        private void Withdraw(Listing listing)
        {
            Forget(listing);
            Destroy(listing);
            if (listing.Quick)
            {
                _quickOpened.Remove(listing.Code);
            }
        }

        /// <summary>Forgets the listing (taken down, or lost with its lobby); its lobby is not touched.</summary>
        private void Forget(Listing listing)
        {
            listing.Withdrawn = true;
            Dictionary<string, Listing> byCode = listing.Quick ? _quickListings : _listings;
            if (byCode.TryGetValue(listing.Code, out Listing current) && current == listing)
            {
                byCode.Remove(listing.Code);
            }

            if (listing.LobbyId != null && _listingsByLobby.TryGetValue(listing.LobbyId, out current) && current == listing)
            {
                _listingsByLobby.Remove(listing.LobbyId);
            }
        }

        /// <summary>Destroys the listing's lobby, once; nothing while it has none yet.</summary>
        private void Destroy(Listing listing)
        {
            if (listing.LobbyId == null || listing.DestroyAsked)
            {
                return;
            }

            listing.DestroyAsked = true;
            PlatformInterface platform = Platform;
            if (!Alive(platform) || _localUser == null)
            {
                // Signed out or shut down: the lobby went with the login, or goes with EOS.
                return;
            }

            LobbyInterface lobbies = platform.GetLobbyInterface();
            var options = new DestroyLobbyOptions { LocalUserId = _localUser, LobbyId = listing.LobbyId };
            lobbies.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo info) =>
            {
                if (IsComplete(info.ResultCode) && info.ResultCode != Result.Success && info.ResultCode != Result.NotFound)
                {
                    Failed("a room listing could not be taken down", info.ResultCode);
                }
            });
        }

        private async Task<PeerLookup> LookUpCoreAsync(string roomCode, CancellationToken ct)
        {
            try
            {
                if (!await SignInAsync(ct))
                {
                    return new PeerLookup(null, ct.IsCancellationRequested ? RoomError.Unknown : _signInError);
                }

                Search search = await SearchAsync(roomCode);
                if (search.Error != RoomError.None)
                {
                    return new PeerLookup(null, search.Error);
                }

                // The room as the latest generation hosts it (ADR 030): an earlier host's lobby may still be up.
                Owner latest = search.Owners[0];
                foreach (Owner owner in search.Owners)
                {
                    if (owner.Generation > latest.Generation)
                    {
                        latest = owner;
                    }
                }

                return new PeerLookup(latest.Address, RoomError.None, latest.Generation);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new PeerLookup(null, RoomError.Unknown);
            }
        }

        /// <summary>The owners of the lobbies in this version's bucket whose CODE is <paramref name="roomCode"/>.</summary>
        private async Task<Search> SearchAsync(string roomCode)
        {
            PlatformInterface platform = Platform;
            ProductUserId user = _localUser;
            if (!Alive(platform) || user == null)
            {
                return new Search(RoomError.Unknown, null);
            }

            LobbyInterface lobbies = platform.GetLobbyInterface();
            var create = new CreateLobbySearchOptions { MaxResults = SearchLimit };
            Result made = lobbies.CreateLobbySearch(ref create, out LobbySearch search);
            if (made != Result.Success || search == null)
            {
                return new Search(FailedAs("a lookup could not be made", made), null);
            }

            try
            {
                Result set = SetEqual(search, LobbyInterface.SEARCH_BUCKET_ID, Bucket);
                if (set == Result.Success)
                {
                    set = SetEqual(search, CodeKey, roomCode);
                }

                if (set != Result.Success)
                {
                    return new Search(FailedAs("a lookup could not be set up", set), null);
                }

                Result found = await Answer(FindAsync(search, user), Result.Canceled);
                if (!Alive(platform))
                {
                    // EOS shut down meanwhile: the search goes with it, unread.
                    return new Search(RoomError.Unknown, null);
                }

                if (found == Result.NotFound)
                {
                    return new Search(RoomError.NotFound, null);
                }

                if (found != Result.Success)
                {
                    return new Search(FailedAs("a lookup failed", found), null);
                }

                List<Owner> owners = OwnersOf(search);
                return owners.Count > 0 ? new Search(RoomError.None, owners) : new Search(RoomError.NotFound, null);
            }
            finally
            {
                // Only into a platform that still runs (2 Oct 2026: a release after EOS shut down crashed the Editor).
                if (Alive(platform))
                {
                    search.Release();
                }
            }
        }

        /// <summary>
        /// The Quick Match tables with free seats, oldest first. The search keeps FREE != 0, which for
        /// seat counts is FREE >= 1 and leaves no doubt about which side the SDK compares. It also asks
        /// for OPENED nearest to 0, so that the ten tables that come back are the ten oldest. They are
        /// put in order here again, and anything without its attributes is skipped.
        /// </summary>
        private async Task<QuickTableSearch> FindQuickCoreAsync(CancellationToken ct)
        {
            try
            {
                if (!await SignInAsync(ct))
                {
                    return new QuickTableSearch(null, ct.IsCancellationRequested ? RoomError.Unknown : _signInError);
                }

                PlatformInterface platform = Platform;
                ProductUserId user = _localUser;
                if (!Alive(platform) || user == null)
                {
                    return new QuickTableSearch(null, RoomError.Unknown);
                }

                LobbyInterface lobbies = platform.GetLobbyInterface();
                var create = new CreateLobbySearchOptions { MaxResults = QuickSearchLimit };
                Result made = lobbies.CreateLobbySearch(ref create, out LobbySearch search);
                if (made != Result.Success || search == null)
                {
                    return new QuickTableSearch(null, FailedAs("a table search could not be made", made));
                }

                try
                {
                    Result set = SetEqual(search, LobbyInterface.SEARCH_BUCKET_ID, QuickBucket);
                    if (set == Result.Success)
                    {
                        set = SetParameter(search, FreeKey, new AttributeDataValue { AsInt64 = 0 }, ComparisonOp.Notequal);
                    }

                    if (set != Result.Success)
                    {
                        return new QuickTableSearch(null, FailedAs("a table search could not be set up", set));
                    }

                    Result ordered = SetParameter(search, OpenedKey, new AttributeDataValue { AsInt64 = 0 }, ComparisonOp.Distance);
                    if (ordered != Result.Success)
                    {
                        // The search still finds tables, only not necessarily the oldest ten.
                        Failed("a table search could not be ordered", ordered);
                    }

                    Result found = await Answer(FindAsync(search, user), Result.Canceled);
                    if (!Alive(platform))
                    {
                        // EOS shut down meanwhile: the search goes with it, unread.
                        return new QuickTableSearch(null, RoomError.Unknown);
                    }

                    if (found == Result.NotFound)
                    {
                        return new QuickTableSearch(Array.Empty<QuickTable>(), RoomError.None);
                    }

                    if (found != Result.Success)
                    {
                        return new QuickTableSearch(null, FailedAs("a table search failed", found));
                    }

                    List<QuickTable> tables = TablesOf(search);
                    tables.Sort(OldestFirst);
                    return new QuickTableSearch(tables, RoomError.None);
                }
                finally
                {
                    // Only into a platform that still runs: this very release, after EOS had shut down
                    // as Play mode ended, crashed the Editor three times on 2 Oct 2026.
                    if (Alive(platform))
                    {
                        search.Release();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new QuickTableSearch(null, RoomError.Unknown);
            }
        }

        /// <summary>The search's results as tables: each with an owner, a code, free seats and an opening time.</summary>
        private static List<QuickTable> TablesOf(LobbySearch search)
        {
            var tables = new List<QuickTable>();
            var countOptions = new LobbySearchGetSearchResultCountOptions();
            uint count = search.GetSearchResultCount(ref countOptions);
            for (uint i = 0; i < count; i++)
            {
                var index = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                if (search.CopySearchResultByIndex(ref index, out LobbyDetails details) != Result.Success || details == null)
                {
                    continue;
                }

                try
                {
                    var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
                    ProductUserId owner = details.GetLobbyOwner(ref ownerOptions);
                    string code = TextOf(details, CodeKey);
                    long? free = NumberOf(details, FreeKey);
                    long? opened = NumberOf(details, OpenedKey);
                    if (owner != null && owner.IsValid() && !string.IsNullOrEmpty(code) && free.HasValue && free.Value >= 1 && opened.HasValue)
                    {
                        tables.Add(new QuickTable(code, owner.ToString(), (int)Math.Min(free.Value, int.MaxValue), opened.Value));
                    }
                }
                finally
                {
                    details.Release();
                }
            }

            return tables;
        }

        private static string TextOf(LobbyDetails details, string key)
        {
            AttributeDataValue? value = ValueOf(details, key);
            return value.HasValue && value.Value.ValueType == AttributeType.String ? (string)value.Value.AsUtf8 : null;
        }

        private static long? NumberOf(LobbyDetails details, string key)
        {
            AttributeDataValue? value = ValueOf(details, key);
            return value.HasValue && value.Value.ValueType == AttributeType.Int64 ? value.Value.AsInt64 : null;
        }

        private static AttributeDataValue? ValueOf(LobbyDetails details, string key)
        {
            var options = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
            if (details.CopyAttributeByKey(ref options, out var attribute) != Result.Success || !attribute.HasValue || !attribute.Value.Data.HasValue)
            {
                return null;
            }

            return attribute.Value.Data.Value.Value;
        }

        /// <summary>Oldest first; two tables that opened in the same millisecond by their codes.</summary>
        private static int OldestFirst(QuickTable a, QuickTable b)
        {
            int byAge = a.OpenedAtUnixMs.CompareTo(b.OpenedAtUnixMs);
            return byAge != 0 ? byAge : string.CompareOrdinal(a.RoomCode, b.RoomCode);
        }

        private static Result SetEqual(LobbySearch search, Utf8String key, string value) =>
            SetParameter(search, key, value, ComparisonOp.Equal);

        private static Result SetParameter(LobbySearch search, Utf8String key, AttributeDataValue value, ComparisonOp comparison)
        {
            var options = new LobbySearchSetParameterOptions
            {
                Parameter = new AttributeData { Key = key, Value = value },
                ComparisonOp = comparison,
            };
            return search.SetParameter(ref options);
        }

        private static Task<Result> FindAsync(LobbySearch search, ProductUserId user)
        {
            var done = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new LobbySearchFindOptions { LocalUserId = user };
            search.Find(ref options, null, (ref LobbySearchFindCallbackInfo info) =>
            {
                if (IsComplete(info.ResultCode))
                {
                    done.TrySetResult(info.ResultCode);
                }
            });
            return done.Task;
        }

        private static List<Owner> OwnersOf(LobbySearch search)
        {
            var owners = new List<Owner>();
            var countOptions = new LobbySearchGetSearchResultCountOptions();
            uint count = search.GetSearchResultCount(ref countOptions);
            for (uint i = 0; i < count; i++)
            {
                var index = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                if (search.CopySearchResultByIndex(ref index, out LobbyDetails details) != Result.Success || details == null)
                {
                    continue;
                }

                try
                {
                    var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
                    ProductUserId owner = details.GetLobbyOwner(ref ownerOptions);
                    if (owner != null && owner.IsValid())
                    {
                        long? generation = NumberOf(details, GenerationKey);
                        owners.Add(new Owner(owner.ToString(), generation.HasValue && generation.Value > 0 && generation.Value <= int.MaxValue ? (int)generation.Value : 0));
                    }
                }
                finally
                {
                    details.Release();
                }
            }

            return owners;
        }

        private Task<Made> CreateLobbyAsync(string bucket)
        {
            PlatformInterface platform = Platform;
            if (!Alive(platform) || _localUser == null)
            {
                return Task.FromResult(new Made(null, RoomError.Unknown));
            }

            LobbyInterface lobbies = platform.GetLobbyInterface();
            var done = new TaskCompletionSource<Made>(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new CreateLobbyOptions
            {
                LocalUserId = _localUser,
                MaxLobbyMembers = ListingCapacity,
                PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                PresenceEnabled = false,
                AllowInvites = false,
                BucketId = bucket,
                DisableHostMigration = true,
                EnableRTCRoom = false,
                LocalRTCOptions = null,
                LobbyId = null,
                EnableJoinById = false,
                RejoinAfterKickRequiresInvite = false,
                AllowedPlatformIds = null,
                CrossplayOptOut = false,
            };
            lobbies.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
            {
                if (!IsComplete(info.ResultCode))
                {
                    return;
                }

                string lobbyId = info.LobbyId;
                done.TrySetResult(info.ResultCode == Result.Success && !string.IsNullOrEmpty(lobbyId)
                    ? new Made(lobbyId, RoomError.None)
                    : new Made(null, FailedAs("a room listing could not be made", info.ResultCode)));
            });
            return done.Task;
        }

        /// <summary>
        /// Sets public attributes on the lobby, which searches can match, all in one update. An
        /// attribute that is there already takes the new value.
        /// </summary>
        private async Task<RoomError> PutAttributesAsync(string lobbyId, params AttributeData[] attributes)
        {
            PlatformInterface platform = Platform;
            if (!Alive(platform) || _localUser == null || lobbyId == null)
            {
                return RoomError.Unknown;
            }

            LobbyInterface lobbies = platform.GetLobbyInterface();
            var modify = new UpdateLobbyModificationOptions { LocalUserId = _localUser, LobbyId = lobbyId };
            Result made = lobbies.UpdateLobbyModification(ref modify, out LobbyModification modification);
            if (made != Result.Success || modification == null)
            {
                return FailedAs("a room listing could not be changed", made);
            }

            try
            {
                foreach (AttributeData data in attributes)
                {
                    var attribute = new LobbyModificationAddAttributeOptions { Attribute = data, Visibility = LobbyAttributeVisibility.Public };
                    Result added = modification.AddAttribute(ref attribute);
                    if (added != Result.Success)
                    {
                        return FailedAs("an attribute could not be put on a room listing", added);
                    }
                }

                Result updated = await Answer(UpdateLobbyAsync(lobbies, modification), Result.Canceled);
                if (!Alive(platform))
                {
                    return RoomError.Unknown;
                }

                return updated == Result.Success ? RoomError.None : FailedAs("a room listing could not be updated", updated);
            }
            finally
            {
                // Only into a platform that still runs, as with a search.
                if (Alive(platform))
                {
                    modification.Release();
                }
            }
        }

        private static Task<Result> UpdateLobbyAsync(LobbyInterface lobbies, LobbyModification modification)
        {
            var done = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new UpdateLobbyOptions { LobbyModificationHandle = modification };
            lobbies.UpdateLobby(ref options, null, (ref UpdateLobbyCallbackInfo info) =>
            {
                if (IsComplete(info.ResultCode))
                {
                    done.TrySetResult(info.ResultCode);
                }
            });
            return done.Task;
        }

        /// <summary>One room code this device lists (a private room or a Quick Match table), from the moment it was asked for.</summary>
        private sealed class Listing
        {
            public Listing(string code, bool quick)
            {
                Code = code;
                Quick = quick;
            }

            public string Code { get; }

            /// <summary>True for a Quick Match table, false for a private room.</summary>
            public bool Quick { get; }

            /// <summary>A table's free seats as the room logic last said.</summary>
            public int Free { get; set; }

            /// <summary>A table's free seats as the directory last took them; -1 before it took any.</summary>
            public int SentFree { get; set; } = -1;

            /// <summary>True while an update of a table's free seats is on its way.</summary>
            public bool Updating { get; set; }

            /// <summary>When a table opened, by this device's clock (UTC ms since 1970).</summary>
            public long OpenedAtUnixMs { get; set; }

            /// <summary>The generation a private room is listed at (ADR 030); 0 as its creator opened it.</summary>
            public int Generation { get; set; }

            /// <summary>The making of the listing, for a second ListAsync of the same code while it runs.</summary>
            public Task<RoomError> Making { get; set; }

            /// <summary>The lobby once it exists; null while it is being made.</summary>
            public string LobbyId { get; set; }

            /// <summary>True once the lobby is up with its code.</summary>
            public bool Live { get; set; }

            /// <summary>True once it was taken down (Unlist, a sign-out, Dispose, a caller that gave up).</summary>
            public bool Withdrawn { get; set; }

            public bool DestroyAsked { get; set; }
        }

        /// <summary>A connection: a peer and a channel.</summary>
        private readonly struct Link : IEquatable<Link>
        {
            public Link(string address, string channel)
            {
                Address = address;
                Channel = channel;
            }

            public string Address { get; }

            public string Channel { get; }

            public bool Equals(Link other) =>
                string.Equals(Address, other.Address, StringComparison.Ordinal) && string.Equals(Channel, other.Channel, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is Link other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Address != null ? Address.GetHashCode() : 0) * 397) ^ (Channel != null ? Channel.GetHashCode() : 0);
                }
            }
        }

        /// <summary>A noted event, or a packet in a pooled buffer until TryReceive copies it out.</summary>
        private readonly struct Item
        {
            private Item(ItemKind kind, string address, string channel, string code, byte[] buffer, int length)
            {
                Kind = kind;
                Address = address;
                Channel = channel;
                Code = code;
                Buffer = buffer;
                Length = length;
            }

            public ItemKind Kind { get; }

            public string Address { get; }

            public string Channel { get; }

            /// <summary>The room code of a ListingLost.</summary>
            public string Code { get; }

            public byte[] Buffer { get; }

            public int Length { get; }

            public static Item Packet(string address, string channel, byte[] buffer, int length) =>
                new Item(ItemKind.Packet, address, channel, null, buffer, length);

            public static Item Event(ItemKind kind, string address, string channel) => new Item(kind, address, channel, null, null, 0);

            public static Item Lost(string roomCode) => new Item(ItemKind.ListingLost, null, null, roomCode, null, 0);

            public bool Is(string address, string channel) =>
                string.Equals(Address, address, StringComparison.Ordinal) && string.Equals(Channel, channel, StringComparison.Ordinal);
        }

        /// <summary>The answer of a search: the owners found (never empty when Error is None).</summary>
        private readonly struct Search
        {
            public Search(RoomError error, List<Owner> owners)
            {
                Error = error;
                Owners = owners;
            }

            public RoomError Error { get; }

            public List<Owner> Owners { get; }

            /// <summary>True when a device other than <paramref name="address"/> lists the code at <paramref name="generation"/> or a later one.</summary>
            public bool ListedByAnotherThan(string address, int generation)
            {
                foreach (Owner owner in Owners)
                {
                    if (!string.Equals(owner.Address, address, StringComparison.Ordinal) && owner.Generation >= generation)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>A lobby's owner, and the generation it lists its room at (ADR 030).</summary>
        private readonly struct Owner
        {
            public Owner(string address, int generation)
            {
                Address = address;
                Generation = generation;
            }

            public string Address { get; }

            public int Generation { get; }
        }

        /// <summary>The answer of a lobby's creation.</summary>
        private readonly struct Made
        {
            public Made(string lobbyId, RoomError error)
            {
                LobbyId = lobbyId;
                Error = error;
            }

            public string LobbyId { get; }

            public RoomError Error { get; }
        }
    }
}
