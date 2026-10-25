using System.Diagnostics;
using Rts.Net.Transport;
using Rts.Server.Relay;

// The relay server (docs/implementation/M9_network.md, 4.14): rts-server [--port N] [--max-rooms N] [--rooms-per-address N].
// It runs until stopped (Ctrl+C, or docker stop) and writes what it does to the console.
var port = 27020;
var settings = new RelaySettings();
for (var i = 0; i < args.Length; i++)
{
    var value = i + 1 < args.Length && int.TryParse(args[i + 1], out var number) ? number : (int?)null;
    switch (args[i])
    {
        case "--port" when value is { } p:
            port = p;
            i++;
            break;
        case "--max-rooms" when value is { } rooms:
            settings = settings with { MaxRooms = rooms };
            i++;
            break;
        case "--rooms-per-address" when value is { } perAddress:
            settings = settings with { MaxRoomsPerAddress = perAddress };
            i++;
            break;
        default:
            Console.Error.WriteLine($"rts-server: unknown argument '{args[i]}'. Usage: rts-server [--port N] [--max-rooms N] [--rooms-per-address N]");
            return 2;
    }
}

var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();

using var transport = new LiteNetTransport();
transport.Listen(port);
void Log(string line) => Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {line}");
var server = new RelayServer(transport, settings, transport.AddressOf, log: Log);
Log($"relay on UDP port {transport.LocalPort}, protocol {Rts.Net.Protocol.NetProtocol.Version}, up to {settings.MaxRooms} rooms");

var clock = Stopwatch.StartNew();
var last = clock.ElapsedMilliseconds;
while (!stop.IsCancellationRequested)
{
    var now = clock.ElapsedMilliseconds;
    server.Update(now - last);
    last = now;
    Thread.Sleep(1);
}

Log("relay stopped");
return 0;
