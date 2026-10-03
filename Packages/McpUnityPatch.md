# MCP Unity metadata patch

The local `com.gamelovers.mcp-unity-1.5.0-meta-fix.tgz` contains the pinned upstream
revision `382a43a30f4dab4c7f02d770b4ec7d084813bbe4`, plus the missing
`Editor/Tests/McpUnityServerBatchModeTests.cs.meta` (GUID `4ee96574083041ea911935241aa705cf`).
The upstream license and source are included. No package behavior or dependencies changed.

Unity imports Git/tarball packages as immutable. Adding a metadata file only in Library
would be lost on cache refresh, so the project manifest uses this committed archive.
Generated Node `build` and `node_modules` directories are excluded; the package's normal
Node installation/build supplies them. The project MCP client follows Unity's resolved
package path rather than accidentally selecting a stale cache entry.

To update, compare a new upstream revision, ensure every imported asset has its metadata,
and replace the archive/manifest together. The metadata-only patch can be dropped when
upstream includes it. Do not edit the immutable package cache as the lasting fix.
