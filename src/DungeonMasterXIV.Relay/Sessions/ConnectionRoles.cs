namespace DungeonMasterXIV.Relay.Sessions;

internal sealed class ConnectionRoles
{
    private readonly Dictionary<string, HashSet<string>> _codesByConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _hostedByConnection = new(StringComparer.Ordinal);

    public bool Hosts(string connectionId) => _hostedByConnection.ContainsKey(connectionId);

    public bool IsIn(string connectionId, string code) =>
        _codesByConnection.TryGetValue(connectionId, out var codes) && codes.Contains(code);

    public void AddHost(string connectionId, string code)
    {
        _hostedByConnection[connectionId] = code;
        Add(connectionId, code);
    }

    public void Add(string connectionId, string code)
    {
        if (!_codesByConnection.TryGetValue(connectionId, out var codes))
        {
            codes = new HashSet<string>(StringComparer.Ordinal);
            _codesByConnection[connectionId] = codes;
        }

        codes.Add(code);
    }

    public void Remove(string connectionId, string code)
    {
        if (!_codesByConnection.TryGetValue(connectionId, out var codes))
        {
            return;
        }

        codes.Remove(code);
        if (codes.Count == 0)
        {
            Forget(connectionId);
        }
    }

    public IReadOnlyCollection<string>? Forget(string connectionId)
    {
        _hostedByConnection.Remove(connectionId);
        return _codesByConnection.Remove(connectionId, out var codes) ? codes : null;
    }
}
