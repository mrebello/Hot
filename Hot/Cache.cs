namespace Hot;


/// <summary>
/// Faz cache em memória de uma tupla.
/// Uso:
/// var cache = new Cached<Guid, string>();
/// string item = Cached( key, (key) =>  { return cria_valor_para_chave(key); } );
/// </summary>
/// <typeparam name="TKey">Tipo da chave de acesso</typeparam>
/// <typeparam name="TItem">Tipo de item do cache</typeparam>

// Implementação "simples" apenas com ConcurrentDictionary
public class Cached<TKey, TItem> : ConcurrentDictionary<TKey, TItem> where TKey : notnull {
}

// Implementação mais completa seria fazendo diversos tratamentos para expiração de cache, ...
