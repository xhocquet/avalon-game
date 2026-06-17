# Project Structure

- `client/`: Godot 4.6 Mono/.NET game client.
- `server/`: C# game server.
- `client/` and `server/` both use Klotho for deterministic networking.
- `gdd/`: generated HTML game design documentation from another repo.
- `klotho-docs/`: copied Klotho source docs for local reference only; treat as read-only.

# Agent Routing

- If working in `client/`, follow [`client/AGENTS.md`](client/AGENTS.md) for Godot/editor/runtime context.
- If working in `server/`, follow [`server/AGENTS.md`](server/AGENTS.md) for backend/build/runtime context.
- Treat `client/Sim/` as shared deterministic simulation code compiled by both client and server. Keep client/server behavior aligned when editing it.
