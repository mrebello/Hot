using static System.Net.Mime.MediaTypeNames;

namespace Hot.Extensions {
    /// <summary>
    /// Extensões para os tipos básicos do C#: object, string, int, ...
    /// </summary>
    public static class AssemblyExtensions {
        public static Stream? GetAsmStream(this Assembly asm_resource,string sub_name) => asm_resource.GetManifestResourceStream(asm_resource.GetName().Name + "." + sub_name);
    }
}
