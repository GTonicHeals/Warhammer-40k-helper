---
status: accepted
date: 2026-11-10
---

# Orders of Battle live in the SQLite campaign database, not browser localStorage

The Campaign Map was moved to SQLite in the repo (`T'au/campaign.db`), but Orders of Battle and Crusade Cards stayed in browser localStorage, so a Crusade force only existed in the one browser that created it and could not be linked to a recorded Battle. We decided to move Orders of Battle and Crusade Cards into the same SQLite database as the Campaign Map, with a one-time import of any localStorage data on first load, because a Crusade Campaign needs one store that holds the map, the forces and the battles together, and the post-battle sequence must update Crusade Cards from a Battle record. The cost is losing the "works without a server write" property of localStorage, which we no longer need since the app already runs as a server.

## Consequences

- `CrusadeManagerService` becomes an EF Core repository like `CampaignManagerService`; the `crusadeStorage.js` interop goes away after the import path has run.
- An Order of Battle belongs to a Crusade Campaign and to a player, so a Battle can reference the Orders of Battle that fought it.
