using System;
using System.IO;

using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Network;
using Meesles.Avalon.Server;

// Force loading of shared sim code. See KlothoServerBootstrap
KlothoServerBootstrap.Initialize("Avalon", "Meesles");

int port = args.Length > 0 ? int.Parse(args[0]) : 7777;
var logLevel = args.Length > 1 ? Enum.Parse<KLogLevel>(args[1]) : KLogLevel.Information;
const int maxRooms = 1;   // single concurrent match

using var loggerFactory = KLoggerFactory.Create(builder => {
  builder.SetMinimumLevel(logLevel);
  builder.AddConsole();
  builder.AddRollingFile(options => {
    options.FilePrefix = "AvalonServer";
    options.RollingSizeKB = 1024 * 1024;
  });
});
var logger = loggerFactory.CreateLogger("AvalonServer");

// Server-authoritative config (simulationconfig.json / sessionconfig.json).
var simConfig = SimulationConfigLoader.Load(args, logger);
var sessionConfig = SessionConfigLoader.Load(args, logger);
int tickIntervalMs = simConfig.TickIntervalMs;
int maxPlayers = sessionConfig.MaxPlayers;

// Shared sim DataAssets (.bytes), copied from client/Sim/Data next to the executable under Data/.
var assetPath = Path.Combine(AppContext.BaseDirectory, "Data", "Assets.bytes");
var dataAssets = DataAssetReader.LoadMixedCollectionFromBytes(assetPath);
IDataAssetRegistryBuilder registryBuilder = new DataAssetRegistry();
registryBuilder.RegisterRange(dataAssets);

var layoutPath = Path.Combine(AppContext.BaseDirectory, "Data", "MapLayout.bytes");
if (File.Exists(layoutPath))
  registryBuilder.RegisterRange(DataAssetReader.LoadMixedCollectionFromBytes(layoutPath));

var sharedRegistry = registryBuilder.Build();

var transport = new LiteNetLibTransport(logger, connectionKey: "Meesles.Avalon");
if (!transport.Listen("0.0.0.0", port, maxRooms * maxPlayers)) {
  logger.KError($"[AvalonServer] Failed to bind port {port} — exiting.");
  Environment.Exit(1);
}

// RoomRouter consumes the RoomHandshakeMessage and routes peers to the room; RoomManager
// wires EcsSimulation / ServerNetworkService / KlothoEngine / CommandFactory per room internally.
var router = new RoomRouter(transport, logger);
var roomManagerConfig = new RoomManagerConfigBuilder((roomLogger) => new ServerSimCallbacks(roomLogger, maxPlayers))
    .WithRoomLimits(maxRooms, maxPlayers, maxSpectatorsPerRoom: 0)
    .WithSimulationConfig(simConfig)
    .WithSessionConfig(sessionConfig)
    .WithDerivedSimulation(sharedRegistry)
    .Build();
var roomManager = new RoomManager(transport, router, loggerFactory, roomManagerConfig);

logger.KInformation($"[AvalonServer] listening on port {port}, maxPlayers={maxPlayers}, tickInterval={tickIntervalMs}ms");

var loop = new ServerLoop(transport, roomManager, tickIntervalMs, logger);
loop.Run();

logger.KInformation($"[AvalonServer] Server stopped.");
