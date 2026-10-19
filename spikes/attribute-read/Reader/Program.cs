using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Spike A: read registration attributes from a net462 DLL with MetadataLoadContext only.
// Usage: Reader <plugin-dll> <net462-reference-dir> [--without-xrm-sdk] [--only-dll-folder] [--only-mscorlib]
// No Assembly.Load* / LoadFrom is ever called; the plugin code is never executed.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: Reader <plugin-dll> <net462-reference-dir> [--without-xrm-sdk] [--only-dll-folder] [--only-mscorlib]");
            return 2;
        }

        string dll = Path.GetFullPath(args[0]);
        string refDir = Path.GetFullPath(args[1]);
        bool withoutXrm = args.Contains("--without-xrm-sdk");
        bool onlyDllFolder = args.Contains("--only-dll-folder");

        var paths = new List<string>();
        foreach (string f in Directory.GetFiles(Path.GetDirectoryName(dll)!, "*.dll"))
        {
            if (withoutXrm && Path.GetFileName(f).Equals("Microsoft.Xrm.Sdk.dll", StringComparison.OrdinalIgnoreCase)) continue;
            paths.Add(f);
        }
        // --only-mscorlib: from the reference folder, give the resolver nothing but mscorlib.dll.
        if (args.Contains("--only-mscorlib")) paths.Add(Path.Combine(refDir, "mscorlib.dll"));
        else if (!onlyDllFolder) paths.AddRange(Directory.GetFiles(refDir, "*.dll"));

        Console.WriteLine("Resolver inputs: " + paths.Count + " files (withoutXrm=" + withoutXrm + ", onlyDllFolder=" + onlyDllFolder + ")");
        var resolver = new PathAssemblyResolver(paths);
        using var mlc = new MetadataLoadContext(resolver, "mscorlib");
        try
        {
            Assembly asm = mlc.LoadFromAssemblyPath(dll);
            Console.WriteLine("Loaded: " + asm.FullName);
            foreach (Type t in asm.GetTypes().OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                Console.WriteLine();
                Console.WriteLine("TYPE " + t.FullName + " (IsPublic=" + t.IsPublic + ", IsNotPublic=" + t.IsNotPublic + ", IsNestedAssembly=" + t.IsNestedAssembly + ")");
                foreach (CustomAttributeData cad in t.GetCustomAttributesData())
                    Dump(cad);
                // Probe: resolving interfaces touches Microsoft.Xrm.Sdk (IPlugin); attributes above do not.
                try
                {
                    Console.WriteLine("  INTERFACES: " + string.Join(", ", t.GetInterfaces().Select(i => i.FullName)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  INTERFACES threw " + ex.GetType().FullName + ": " + ex.Message);
                }
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    if (f.IsLiteral)
                        Console.WriteLine("  CONST " + f.Name + " = " + Describe(f.GetRawConstantValue()));
            }

            Type? info = asm.GetType("Dvx.Plugin.Sdk.DvxSdkInfo");
            Console.WriteLine();
            Console.WriteLine("DvxSdkInfo.Version = " + Describe(info?.GetField("Version")?.GetRawConstantValue()));
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPTION " + ex.GetType().FullName + ": " + ex.Message);
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Assembly.Load* calls in this program: none; MetadataLoadContext only.");
        return 0;
    }

    private static void Dump(CustomAttributeData cad)
    {
        Console.WriteLine("  ATTR " + cad.AttributeType.FullName + " (IsNotPublic=" + cad.AttributeType.IsNotPublic + ")");
        ParameterInfo[] ps = cad.Constructor.GetParameters();
        for (int i = 0; i < cad.ConstructorArguments.Count; i++)
            Console.WriteLine("    ctor[" + i + "] " + ps[i].Name + ": " + Typed(cad.ConstructorArguments[i]));
        foreach (CustomAttributeNamedArgument na in cad.NamedArguments)
            Console.WriteLine("    named " + na.MemberName + " (IsField=" + na.IsField + "): " + Typed(na.TypedValue));
    }

    private static string Typed(CustomAttributeTypedArgument a)
    {
        string head = "ArgumentType=" + a.ArgumentType.FullName + " IsEnum=" + a.ArgumentType.IsEnum + " IsArray=" + a.ArgumentType.IsArray;
        object? v = a.Value;
        if (v == null) return head + " Value=null";
        string vt = "Value.GetType()=" + v.GetType().Name;
        if (v is IEnumerable<CustomAttributeTypedArgument> items)
        {
            var list = items.ToList();
            return head + " " + vt + " Count=" + list.Count + " [" + string.Join(", ", list.Select(x => "\"" + x.Value + "\" (" + x.ArgumentType.Name + "/" + x.Value?.GetType().Name + ")")) + "]";
        }
        if (a.ArgumentType.IsEnum)
            return head + " " + vt + " Value=" + v + " enumMember=" + EnumName(a);
        return head + " " + vt + " Value=" + Describe(v);
    }

    // Maps the raw underlying value back to the enum member name using the metadata-only enum type.
    private static string EnumName(CustomAttributeTypedArgument a)
    {
        foreach (FieldInfo f in a.ArgumentType.GetFields(BindingFlags.Public | BindingFlags.Static))
            if (Equals(f.GetRawConstantValue(), a.Value)) return f.Name;
        return "?";
    }

    private static string Describe(object? v) => v == null ? "null" : "\"" + v + "\" (" + v.GetType().Name + ")";
}
