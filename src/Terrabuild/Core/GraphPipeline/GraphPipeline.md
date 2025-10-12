
# GraphPipeline

The GraphPipeline coordinates the build process by sequencing several specialized components. Each component transforms the build graph, resolves dependencies, and determines execution strategy for efficient and correct builds.

```mermaid
graph TD
	A[Node.fs] --> B[Action.fs]
	B --> C[Cascade.fs]
	C --> D[Cluster.fs]
```

## Nodes.fs

Builds the initial dependency graph from the workspace configuration.  
- Validates targets and projects.
- Recursively creates nodes for each project and target, resolving all dependencies.
- Discovers operations for each node using extension scripts.
- Computes hashes for caching and clustering.
- Produces the raw graph structure with nodes and root nodes.

# Action.fs

Determines the build actions for each node and cluster, using build status if available.  
- Evaluates which nodes need to be built, restored, or ignored.
- Applies rules based on cache status, previous build results, and configuration.
- Prepares the build request for execution.

# Cascade.fs

Implements the cascading scheme for build requests and rebuilds.  
- Applies cascade attributes to propagate rebuilds through dependencies.
- Ensures that changes in one node or cluster trigger appropriate downstream builds.

# Cluster.fs

Groups related nodes into clusters for batch execution.  
- Identifies batchable nodes based on configuration and script attributes.
- Creates cluster nodes, ensuring operations are discovered via extension scripts.
- Sets up batch contexts and cluster dependencies.

