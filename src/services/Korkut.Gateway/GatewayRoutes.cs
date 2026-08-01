using Yarp.ReverseProxy.Configuration;

namespace Korkut.Gateway;

/// <summary>
/// The gateway's in-memory YARP route + cluster map, built from env-configured downstream URLs.
/// Path-based routing: the agent surface (assistant sessions/chat/streaming + the drafting
/// sub-actions on a document) goes to <c>Korkut.Agent</c>; everything else under <c>/api</c> goes
/// to <c>Korkut.Api</c>. Knowledge and OCR are internal — never routed from here. The agent routes
/// carry the <see cref="AuthenticatedPolicy"/> so an anonymous caller is rejected at the edge;
/// the operational API routes pass through and the API enforces its own per-endpoint permissions
/// (defense in depth — downstream still validates the token + applies RLS).
/// </summary>
public static class GatewayRoutes
{
    public const string AuthenticatedPolicy = "authenticated";

    private const string AgentCluster = "agent";
    
    private const string ApiCluster = "api";

    public static IReadOnlyList<RouteConfig> Routes()
    {
        RouteConfig Agent(string id, string path, string? methods = null) => new()
        {
            RouteId = id,
            ClusterId = AgentCluster,
            AuthorizationPolicy = AuthenticatedPolicy,
            Order = 1,
            Match = new RouteMatch { Path = path, Methods = methods is null ? null : [methods] },
        };

        return
        [
            Agent("agent-sessions", "/api/sessions/{**catch-all}"),
            Agent("agent-legal", "/api/legal/{**catch-all}"),
            new()
            {
                // Platform-admin console API (auth + data) — served by the operational API host.
                RouteId = "admin-api",
                ClusterId = ApiCluster,
                Order = 1,
                Match = new RouteMatch { Path = "/admin/api/{**catch-all}" },
            },
            Agent("agent-doc-list", "/api/draft-documents", "GET"),
            Agent("agent-doc-draft", "/api/documents/{docId}/draft", "POST"),
            Agent("agent-doc-accept", "/api/documents/{docId}/accept", "POST"),
            Agent("agent-doc-revert", "/api/documents/{docId}/revert", "POST"),
            Agent("agent-doc-export", "/api/documents/{docId}/export", "GET"),
            Agent("agent-doc-save", "/api/documents/{docId}", "PUT"),
            new()
            {
                // Everything else under /api → the operational API (which enforces its own auth).
                RouteId = "api",
                ClusterId = ApiCluster,
                Order = 100,
                Match = new RouteMatch { Path = "/api/{**catch-all}" },
            },
        ];
    }

    public static IReadOnlyList<ClusterConfig> Clusters(IConfiguration config)
    {
        var apiUrl = config["KORKUT_API_URL"]
            ?? throw new InvalidOperationException("KORKUT_API_URL is required for the Gateway (the operational API upstream).");
        var agentUrl = config["KORKUT_AGENT_URL"]
            ?? throw new InvalidOperationException("KORKUT_AGENT_URL is required for the Gateway (the agent host upstream).");

        return
        [
            Cluster(ApiCluster, apiUrl),
            Cluster(AgentCluster, agentUrl),
        ];
    }

    private static ClusterConfig Cluster(string id, string address) => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["primary"] = new() { Address = address },
        },
    };
}
