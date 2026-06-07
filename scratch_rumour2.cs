using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
            string name = new AssemblyName(args.Name).Name;
            string path = @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\" + name + ".dll";
            if (System.IO.File.Exists(path)) return Assembly.LoadFrom(path);
            return null;
        };

        try {
            var assembly = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\RumourHasIt\bin\Win64_Shipping_Client\RumourHasIt.dll");
            Console.WriteLine("--- TYPES ---");
            foreach (var type in assembly.GetTypes().Where(t => t.IsPublic)) {
                Console.WriteLine(type.FullName);
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                    Console.WriteLine("  - " + method.Name);
                }
            }
        } catch (ReflectionTypeLoadException ex) {
            foreach (var loaderEx in ex.LoaderExceptions) {
                Console.WriteLine("Loader Exception: " + loaderEx.Message);
            }
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
        }
    }
}
