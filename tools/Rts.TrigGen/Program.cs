using System;
using System.IO;
using Rts.TrigGen;

// Writes src/Rts.Simulation/Math/TrigTables.cs. Run from anywhere inside the repository:
//   dotnet run --project tools/Rts.TrigGen
var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !File.Exists(Path.Combine(root.FullName, "RtsGame.sln")))
{
    root = root.Parent;
}

if (root is null)
{
    Console.Error.WriteLine("RtsGame.sln not found above the current folder.");
    return 1;
}

var target = Path.Combine(root.FullName, "src", "Rts.Simulation", "Math", "TrigTables.cs");
File.WriteAllText(target, TrigTableGenerator.Render());
Console.WriteLine($"Wrote {target}");
return 0;
