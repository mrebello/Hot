using static System.Net.Mime.MediaTypeNames;

namespace Hot.Extensions {
    /// <summary>
    /// Extensões para os tipos básicos do C#: object, string, int, ...
    /// </summary>
    public static class AssemblyExtensions {
        /// <summary>
        /// Procura pelo arquivo de recurso cujo nome termina com <i>sub_name</i>
        /// </summary>
        /// <param name="asm_resource"></param>
        /// <param name="sub_name"></param>
        /// <returns></returns>
        public static Stream? GetAsmStream(this Assembly asm_resource,string sub_name) {
            // return asm_resource.GetManifestResourceStream(asm_resource.GetName().Name + "." + sub_name);
            string name = asm_resource.GetManifestResourceNames().Single(p => p.EndsWith(sub_name));
            return asm_resource.GetManifestResourceStream(name);
        }
    }
}
