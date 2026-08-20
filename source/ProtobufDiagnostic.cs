using System;
using System.IO;
using System.Linq;
using System.Reflection;

public static class ProtobufDiagnostic
{
    public static void Run()
    {
        Console.WriteLine("AppBase: " + AppDomain.CurrentDomain.BaseDirectory);
        Console.WriteLine("RelativeSearchPath: " + AppDomain.CurrentDomain.RelativeSearchPath);
        Console.WriteLine("Loaded assemblies:");
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName))
        {
            Console.WriteLine($"{a.GetName().FullName} - Location=\"{GetLocationSafe(a)}\"");
        }

        var probeName = "protobuf-net";
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
                      .FirstOrDefault(a => string.Equals(a.GetName().Name, probeName, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"Loaded probe '{probeName}': {(loaded != null ? loaded.GetName().FullName + " @ " + GetLocationSafe(loaded) : "Not loaded")}");

        Console.WriteLine("Searching filesystem for protobuf-net*.dll under AppBase:");
        foreach (var f in Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory, "protobuf-net*.dll", SearchOption.AllDirectories).Take(50))
        {
            try
            {
                var an = AssemblyName.GetAssemblyName(f);
                Console.WriteLine($"{f} -> {an.FullName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{f} -> could not read assembly: {ex.Message}");
            }
        }
    }

    static string GetLocationSafe(Assembly a)
    {
        try { return a.Location; } catch { return "(no location)"; }
    }
}