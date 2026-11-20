# GM Identity via Tailscale Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Campaign Editor opens only for the GM, recognised by the `Tailscale-User-Login` header that `tailscale serve` adds; everyone else is a guest.

**Architecture:** A custom ASP.NET Core authentication handler turns the header into a principal with role `GM` when the login is in the configured list (or always, in Development). Blazor gets it through cascading authentication state; `CampaignEditor` checks the role and renders a "GM only" notice for guests; `NavMenu` hides the link and shows a GM tag via `AuthorizeView`.

**Tech Stack:** .NET 10 Blazor Web App (global InteractiveServer), ASP.NET Core authentication, xUnit + bUnit 1.40 + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-19-gm-tailscale-identity-design.md`

## Global Constraints

- No password, no sign-in screen, no sign-out.
- Only the Campaign Editor is locked; every other page stays open to guests.
- Header name: `Tailscale-User-Login`. Role name: `GM`. Config section: `Gm:TailscaleLogins` (string list).
- The GM's real login is never committed; committed `appsettings.json` holds an empty list.
- Development environment: everyone is GM. Production with no/unknown header: guest.

## Review Focus

- Login differs only in case or surrounding whitespace from config (`Nicodaf@Gmail.com `) → GM. Pinned in Task 1.
- `Gm` section missing from config entirely → no crash, nobody is GM in Production. Pinned in Task 1.
- Header present but empty → guest. Pinned in Task 1.
- Guest opening `/campaign-editor` directly → notice only: campaign never loaded, no `UnsavedChangesGuard`, no editor controls. Pinned in Task 2.
- No cascading auth state at all (component rendered outside the auth pipeline) → treated as guest, not a crash. Pinned in Task 2.

---

### Task 1: Tailscale authentication handler and wiring

**Files:**
- Create: `T'au/Services/Auth/GmIdentity.cs` (constants + `GmOptions` + pure `IsGm`)
- Create: `T'au/Services/Auth/TailscaleAuthHandler.cs`
- Modify: `T'au/Program.cs` (register auth; `UseAuthentication()` + `UseAuthorization()` after `UseHttpsRedirection()`, before `UseAntiforgery()`)
- Modify: `T'au/appsettings.json` (add `"Gm": { "TailscaleLogins": [] }`)
- Test: `Warhammer.Tests/Services/TailscaleAuthHandlerTests.cs`

**Interfaces:**
- Produces:
  - `namespace Warhammer.Services.Auth`
  - `public static class GmIdentity { public const string Scheme = "Tailscale"; public const string Role = "GM"; public const string HeaderName = "Tailscale-User-Login"; public static bool IsGm(string? login, IEnumerable<string>? gmLogins, bool isDevelopment); }`
  - `public sealed class GmOptions { public const string Section = "Gm"; public List<string> TailscaleLogins { get; set; } = new(); }`
  - `public sealed class TailscaleAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>` — ctor also takes `IOptionsMonitor<GmOptions>` and `IHostEnvironment`.

- [ ] **Step 1: Write the failing tests**

`IsGm` theory:
- `("me@x.com", ["me@x.com"], false)` → true
- `(" ME@X.com ", ["me@x.com"], false)` → true (case/whitespace)
- `("player@y.com", ["me@x.com"], false)` → false
- `(null, ["me@x.com"], false)` → false
- `("", ["me@x.com"], false)` → false
- `("me@x.com", null, false)` → false; `("me@x.com", [], false)` → false
- `(null, [], true)` → true (Development)

Handler tests (build a `DefaultHttpContext`, `handler.InitializeAsync(new AuthenticationScheme(GmIdentity.Scheme, null, typeof(TailscaleAuthHandler)), ctx)`, then `AuthenticateAsync()`; options via `Mock<IOptionsMonitor<...>>` or a tiny fake, environment via `Mock<IHostEnvironment>` with `EnvironmentName`):
- `Production_matching_header_yields_GM_principal`: `Succeeded` true, `Principal.IsInRole("GM")` true, `Principal.Identity!.Name == "me@x.com"`.
- `Production_unknown_login_is_guest`: `result.None` true.
- `Production_without_header_is_guest`: `result.None` true.
- `Development_without_header_is_GM`: `Succeeded` true and in role `GM`.

- [ ] **Step 2: Run tests, verify they fail**

Run: `dotnet test Warhammer.Tests --filter FullyQualifiedName~TailscaleAuthHandlerTests`
Expected: build failure, `GmIdentity` / `TailscaleAuthHandler` not found.

- [ ] **Step 3: Implement**

`IsGm`: Development → true; otherwise trimmed, case-insensitive (`StringComparer.OrdinalIgnoreCase`) membership against trimmed config entries; null/blank login → false.
Handler `HandleAuthenticateAsync`: read first value of `GmIdentity.HeaderName`; if `IsGm(...)` return `Success` with a `ClaimsPrincipal` (identity authenticationType = `GmIdentity.Scheme`, claims `Name` = login or `"dev-gm"` when none, `Role` = `GM`); else `AuthenticateResult.NoResult()`.
`Program.cs`:
```csharp
builder.Services.Configure<GmOptions>(builder.Configuration.GetSection(GmOptions.Section));
builder.Services.AddAuthentication(GmIdentity.Scheme)
    .AddScheme<AuthenticationSchemeOptions, TailscaleAuthHandler>(GmIdentity.Scheme, null);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test Warhammer.Tests --filter FullyQualifiedName~TailscaleAuthHandlerTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Services/Auth" "T'au/Program.cs" "T'au/appsettings.json" Warhammer.Tests/Services/TailscaleAuthHandlerTests.cs
git commit -m "feat: recognise the GM from the Tailscale login header"
```

### Task 2: Campaign Editor locked to GM

**Files:**
- Create: `T'au/Components/UI/GmOnlyNotice.razor` (short card: "GM only", one line saying the Campaign Editor is for the Game Master, link back to War Room)
- Modify: `T'au/Components/_Imports.razor` (add `@using Microsoft.AspNetCore.Components.Authorization` and `@using Warhammer.Services.Auth`)
- Modify: `T'au/Components/Pages/Campaign/CampaignEditor.razor`
- Modify: `Warhammer.Tests/Components/CampaignEditorTableModeTests.cs`, `CampaignEditorHiddenTerritoryTests.cs`, `CampaignEditorRequisitionsTests.cs` (constructor: `_ctx.AddTestAuthorization().SetAuthorized("gm").SetRoles(GmIdentity.Role);`)
- Test: `Warhammer.Tests/Components/CampaignEditorGmGateTests.cs`

**Interfaces:**
- Consumes: `GmIdentity.Role` from Task 1.
- Produces: `GmOnlyNotice` component (no parameters), root element `id="gm-only-notice"`.

- [ ] **Step 1: Write the failing tests**

- `Guest_sees_the_notice_and_no_editor`: `AddTestAuthorization()` (not authorized); render `CampaignEditor`; `cut.WaitForElement("#gm-only-notice")`; `FindAll("#ce-mode-play")` empty; `FindAll(".ce-form-panel")` empty; no `UnsavedChangesGuard` rendered (`cut.FindComponents<UnsavedChangesGuard>()` empty).
- `Signed_in_non_GM_sees_the_notice`: `SetAuthorized("player")` without role → notice.
- `GM_sees_the_editor`: `SetAuthorized("gm").SetRoles("GM")` → `WaitForElement("#ce-mode-play")`, no `#gm-only-notice`.
- `No_auth_state_is_treated_as_guest`: no `AddTestAuthorization` call → renders notice without throwing.

- [ ] **Step 2: Run tests, verify they fail**

Run: `dotnet test Warhammer.Tests --filter FullyQualifiedName~CampaignEditorGmGateTests`
Expected: FAIL, `#gm-only-notice` not found.

- [ ] **Step 3: Implement the gate**

In `CampaignEditor.razor`: `[CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }`; `private bool _isGm;` set in `OnInitializedAsync` (`AuthState is null` → false; else `(await AuthState).User.IsInRole(GmIdentity.Role)`). Wrap the whole markup (including `UnsavedChangesGuard` and `PageTitle` content) in `@if (!_isGm) { <PageTitle>GM only</PageTitle><GmOnlyNotice /> } else { ...existing... }`. `OnAfterRenderAsync` returns immediately when `!_isGm`, so the campaign is never loaded and no JS starts for guests.

- [ ] **Step 4: Run the whole suite**

Run: `dotnet test Warhammer.Tests`
Expected: all pass, including the three existing editor test classes now set up as GM.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Components" Warhammer.Tests/Components
git commit -m "feat: Campaign Editor shows a GM-only notice to guests"
```

### Task 3: Menu, GM tag, docs, live check

**Files:**
- Modify: `T'au/Components/Layout/NavMenu.razor` (wrap the Campaign Editor `nav-item` at lines 89-94 in `<AuthorizeView Roles="@GmIdentity.Role"><Authorized>…</Authorized></AuthorizeView>`; add a small `<span class="badge bg-warning text-dark" id="nav-gm-tag">GM</span>` inside the same kind of `AuthorizeView` next to the campaign section heading)
- Modify: `README.md` (section "GM access": how the header works, Development = GM, and the one-time service setup below)

No bUnit test: `NavMenu` pulls in five services and JS storage; it is checked live instead.

- [ ] **Step 1: Implement the menu changes**

- [ ] **Step 2: Write the README section**

Must contain the service setup, with a placeholder instead of the real login:
```powershell
# Admin PowerShell. AppEnvironmentExtra REPLACES the list, so read it first and re-set every entry.
C:\Tools\nssm-2.24\win64\nssm.exe get WarhammerHelper AppEnvironmentExtra
C:\Tools\nssm-2.24\win64\nssm.exe set WarhammerHelper AppEnvironmentExtra <existing entries...> Gm__TailscaleLogins__0=<your-tailscale-login>
C:\Tools\nssm-2.24\win64\nssm.exe restart WarhammerHelper
```

- [ ] **Step 3: Live check in Development**

Run: `dotnet run --project "T'au" --launch-profile https`, open `/` and `/campaign-editor`.
Expected: GM tag shown, editor link shown, editor works (Development = GM).

- [ ] **Step 4: Live check as guest**

Run with `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://localhost:5199` (empty config list), open `/campaign-editor`.
Expected: no GM tag, no editor link, `/campaign-editor` shows the GM-only notice.
Then restart that run with `Gm__TailscaleLogins__0=test@example.com` and `curl -s -H "Tailscale-User-Login: test@example.com" http://localhost:5199/` → HTML contains `nav-gm-tag`; the same curl without the header → it does not.

- [ ] **Step 5: Run full tests and commit**

Run: `dotnet test Warhammer.Tests` → all pass.
```bash
git add "T'au/Components/Layout/NavMenu.razor" README.md
git commit -m "feat: hide the Campaign Editor link from guests and tag the GM"
```

### After the plan (user, admin)

Republish the hosted copy (stop service, `dotnet publish "T'au" -c Release -o C:\Apps\Warhammer-Helper-Published` keeping its `campaign.db`, start service) and set `Gm__TailscaleLogins__0` on the service once, as in the README. Then open the app from a phone on Tailscale: GM tag visible; from a player's device: no editor.
