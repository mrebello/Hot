using static System.Net.Mime.MediaTypeNames;

namespace Hot.Extensions {
    /// <summary>
    /// Extensões para o tipo Enum
    /// </summary>
    public static class EnumExtensions {
        /// <summary>
        /// Valida se o valor do enum está dentro dos itens do enum
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool IsEnumValueDefined(this Enum e, object v) => e.GetType().IsEnumDefined(v);


        /// <summary>
        /// Valida se o valor atual do enum está dentro dos itens do enum
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool IsEnumValueDefined(this Enum e) => e.GetType().IsEnumDefined(e);
    }
}
