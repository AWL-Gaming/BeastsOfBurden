using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 4) throw new ArgumentException("Expected plugin, native game directory, BepInEx core directory, report");
var pluginPath = Path.GetFullPath(args[0]);
var directories = new[] { Path.GetFullPath(args[1]), Path.GetFullPath(args[2]) };
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in directories)
    {
        var file = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(file)) return context.LoadFromAssemblyPath(file);
    }
    return null;
};
var cases = new List<object>();
Exception? failure = null;
try
{
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
    var prefix = assembly.GetType("BeastsOfBurden.BeastsOfBurden", true)!
        .GetNestedType("Vagon_Update_Patch", BindingFlags.NonPublic)!
        .GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
    var parameters = prefix.GetParameters();
    var viewIndex = Array.FindIndex(parameters, p => p.Name == "___m_nview");
    var requestIndex = Array.FindIndex(parameters, p => p.Name == "___m_useRequester");
    if (viewIndex < 0 || requestIndex < 0) throw new InvalidOperationException("Native ownership injection is absent");
    var cart = RuntimeHelpers.GetUninitializedObject(parameters[0].ParameterType);
    var request = RuntimeHelpers.GetUninitializedObject(parameters[requestIndex].ParameterType.GetElementType()!);
    var viewType = parameters[viewIndex].ParameterType;
    void Run(string name, object? view)
    {
        var values = new object?[parameters.Length];
        values[0] = cart;
        values[requestIndex] = request;
        values[viewIndex] = view;
        prefix.Invoke(null, values);
        if (!ReferenceEquals(values[0], cart) || !ReferenceEquals(values[requestIndex], request))
            throw new InvalidOperationException(name + " changed the cart or pending ownership request");
        cases.Add(new { name, passed = true });
    }
    Run("Missing native network view preserves the cart and pending request", null);
    var view = RuntimeHelpers.GetUninitializedObject(viewType);
    Run("Invalid native network view preserves the cart and pending request", view);
    var zdoField = viewType.GetField("m_zdo", BindingFlags.Instance | BindingFlags.NonPublic)!;
    var zdo = RuntimeHelpers.GetUninitializedObject(zdoField.FieldType);
    zdoField.SetValue(view, zdo);
    if (!(bool)viewType.GetMethod("IsValid")!.Invoke(view, null)!) throw new InvalidOperationException("Native fixture must be valid");
    if ((bool)viewType.GetMethod("IsOwner")!.Invoke(view, null)!) throw new InvalidOperationException("Native fixture must be a non-owner");
    cases.Add(new { name = "Native fixture is valid and does not own the cart", passed = true });
    Run("Native non-owner preserves the cart and pending ownership request", view);
}
catch (Exception exception) { failure = exception; }
var proof = new
{
    checkedAtUtc = DateTime.UtcNow,
    pluginPath,
    pluginSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pluginPath))),
    nativeGameSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directories[0], "assembly_valheim.dll")))),
    cases,
    success = failure == null,
    failure = failure?.ToString(),
    scope = "Execute the actual compiled ownership prefix against actual native managed cart, requester, view and ZDO types. No Harmony patch activation, Awake, Unity native game objects, live process, world state or physics acceptance.",
    runtimeDeployments = 0
};
File.WriteAllText(args[3], JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Native ownership regression: {cases.Count} cases; success={proof.success}");
if (failure != null) Console.WriteLine(failure.ToString());
return failure == null ? 0 : 1;
