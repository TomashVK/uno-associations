using System.Collections.Generic;

public class ConnectionGraph
{
    private readonly Dictionary<string, HashSet<string>> graph = new Dictionary<string, HashSet<string>>();

    public void Build(LevelConnection[] connections)
    {
        graph.Clear();
        if (connections == null) return;

        foreach (LevelConnection c in connections)
        {
            AddEdge(c.card1, c.card2);
            AddEdge(c.card2, c.card1);
        }
    }

    public bool CanPlay(string activeId, string targetId)
    {
        if (string.IsNullOrEmpty(activeId) || string.IsNullOrEmpty(targetId)) return false;
        return graph.TryGetValue(activeId, out var edges) && edges.Contains(targetId);
    }

    private void AddEdge(string from, string to)
    {
        if (!graph.TryGetValue(from, out var edges))
        {
            edges = new HashSet<string>();
            graph[from] = edges;
        }
        edges.Add(to);
    }
}
