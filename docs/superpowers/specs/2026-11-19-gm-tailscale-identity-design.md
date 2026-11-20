# GM identity via Tailscale — design

Date: 2026-11-19 · Size: small change

## Intent

The app is hosted on the tailnet (`tailscale serve` :8443 → `http://localhost:7200`) and the players
(their own Tailscale accounts) can open the Campaign Editor, which holds every GM secret (GM Notes,
note peek, GM Table Mode). Lock the editor to the GM without ever typing a password: the GM is
recognised by the Tailscale account the request comes from.

## Goals

- GM's devices (signed in to Tailscale as the GM) open the app as GM, with no prompt.
- Everyone else is a guest: the Campaign Editor link is hidden and `/campaign-editor` shows a short
  "GM only" page instead of the editor.
- Sidebar shows a small "GM" tag when you are GM.

## Non-goals / anti-goals

- No password, no sign-in screen, no sign-out (user chose Tailscale only over password or both).
- Crusade, battle and other pages stay open to players; only the Campaign Editor is locked.
- The GM's login (an email) must not be committed to the repo.

## Approaches considered

- **A. Tailscale identity** — read the `Tailscale-User-Login` header that Serve adds to proxied
  tailnet traffic (absent for tagged devices and Funnel). Zero typing; identifies the device, not the
  person. **Chosen.**
- **B. Guest/GM sign-in with password 5544, remembered ~1 year by cookie.** Works from any device but
  you type it once per device.
- **C. Both** — Tailscale auto-GM with the password as a fallback. More to build; declined.

## Design

- ASP.NET Core authentication with a small custom handler: if `Tailscale-User-Login` matches one of
  the configured GM logins, the user gets a GM role claim; otherwise they are a guest.
- Setting `Gm:TailscaleLogins` (list, supports a co-GM later). Empty in the committed
  `appsettings.json`; the real value is set as an environment variable on the `WarhammerHelper`
  NSSM service (survives republish). The repo documents how to set it.
- Development environment (`dotnet run`): everyone is GM, since Tailscale isn't in front.
- Production with no header (e.g. opening `localhost:7200` on the host): guest.
- `CampaignEditor` requires the GM role; the NavMenu link and GM tag use the same check. Blazor
  Server reads the user when the circuit starts, so the interactive editor is covered.
- Accepted limit: header spoofing requires bypassing Tailscale, i.e. local access to the host, since
  the app only listens on localhost.

## Deployment

Republish the hosted copy (stop service, `dotnet publish`, keep its campaign.db, start service) and
set the environment variable on the service once (admin step for the user).

## Left to the builder

Exact handler/claims wiring, how the "GM only" page looks, test approach.
