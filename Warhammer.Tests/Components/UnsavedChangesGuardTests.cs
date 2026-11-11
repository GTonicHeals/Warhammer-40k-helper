using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Warhammer.Components.UI;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

/// <summary>
/// Tests for UnsavedChangesGuard — the NavigationLock + leave-confirmation modal
/// used by CampaignEditor. Verifies that the modal stays hidden until an internal
/// navigation is attempted with IsDirty=true, that each footer button maps to the
/// correct outcome (Stay = prevent + close, Leave = allow + close, Save & Leave =
/// fire callback + allow), and that the optional Save & Leave button only appears
/// when its callback is bound.
/// </summary>
public class UnsavedChangesGuardTests : TestContext
{
    // ═════════════════════════════════════════════════════════════════════════
    // Modal visibility: hidden until navigation is attempted with IsDirty=true
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Modal_NotRendered_WhenNotDirty()
    {
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, false));
        cut.FindAll(".modal").Should().BeEmpty();
    }

    [Fact]
    public void Modal_NotRendered_WhenDirty_ButNoNavigationAttempted()
    {
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));
        cut.FindAll(".modal").Should().BeEmpty();
    }

    [Fact]
    public void InternalNavigation_WhenNotDirty_ProceedsImmediately_NoModal()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, false));

        nav.NavigateTo("/elsewhere");

        cut.FindAll(".modal").Should().BeEmpty();
        nav.Uri.Should().EndWith("/elsewhere");
    }

    [Fact]
    public void InternalNavigation_WhenDirty_OpensModal()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");

        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
    }

    [Fact]
    public void InternalNavigation_WhenDirty_DoesNotChangeUrl_WhileAwaitingDecision()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var originalUri = nav.Uri;
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");

        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        nav.Uri.Should().Be(originalUri);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Footer buttons: outcomes
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ClickingStay_HidesModal_AndPreventsNavigation()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var originalUri = nav.Uri;
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        await cut.Find("button.btn-outline-secondary").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().BeEmpty());
        nav.Uri.Should().Be(originalUri);
    }

    [Fact]
    public async Task ClickingLeaveWithoutSaving_HidesModal_AndAllowsNavigation()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        await cut.Find("button.btn-danger").ClickAsync(new MouseEventArgs());

        WaitForUri(nav, "/elsewhere");
    }

    [Fact]
    public async Task ClickingSaveAndLeave_InvokesCallback_ThenAllowsNavigation()
    {
        var saveInvoked = false;
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p
            .Add(x => x.IsDirty,        true)
            .Add(x => x.OnSaveAndLeave, EventCallback.Factory.Create(this, () => { saveInvoked = true; })));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        await cut.Find("button.btn-warning").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => saveInvoked.Should().BeTrue());
        WaitForUri(nav, "/elsewhere");
    }

    [Fact]
    public async Task ClickingBackdrop_BehavesLikeStay()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var originalUri = nav.Uri;
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        await cut.Find(".modal-backdrop").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().BeEmpty());
        nav.Uri.Should().Be(originalUri);
    }

    [Fact]
    public async Task ClickingCloseButton_BehavesLikeStay()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var originalUri = nav.Uri;
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        await cut.Find("button.btn-close").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().BeEmpty());
        nav.Uri.Should().Be(originalUri);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Conditional Save & Leave button
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SaveAndLeaveButton_NotRendered_WhenCallbackUnbound()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        cut.FindAll("button.btn-warning").Should().BeEmpty();
    }

    [Fact]
    public void SaveAndLeaveButton_Rendered_WhenCallbackBound()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p
            .Add(x => x.IsDirty,        true)
            .Add(x => x.OnSaveAndLeave, EventCallback.Factory.Create(this, () => { })));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        cut.FindAll("button.btn-warning").Should().HaveCount(1);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Title & Message customization
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultTitleAndMessage_AreUsed_WhenNotSpecified()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        cut.Find(".modal-title").TextContent.Should().Contain("Unsaved Changes");
        cut.Find(".modal-body").TextContent.Should().Contain("unsaved changes");
    }

    [Fact]
    public void CustomTitleAndMessage_RenderInsteadOfDefaults()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.Title,   "Confirm Exit")
            .Add(x => x.Message, "Your changes will be lost. Continue?"));

        nav.NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        cut.Find(".modal-title").TextContent.Should().Contain("Confirm Exit");
        cut.Find(".modal-body").TextContent.Should().Contain("Your changes will be lost. Continue?");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Re-entrancy: a second navigation after a Stay still opens a fresh modal
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModalCanBeReopened_AfterStay()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<UnsavedChangesGuard>(p => p.Add(x => x.IsDirty, true));

        // First attempt → Stay
        nav.NavigateTo("/first-attempt");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        await cut.Find("button.btn-outline-secondary").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().BeEmpty());

        // Second attempt → modal reopens
        nav.NavigateTo("/second-attempt");
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
    }

    // The guard renders before the fake NavigationManager updates Uri, and WaitForAssertion only
    // re-checks on renders, so poll the Uri on a timer instead.
    private static void WaitForUri(NavigationManager nav, string suffix)
    {
        SpinWait.SpinUntil(() => nav.Uri.EndsWith(suffix, StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        nav.Uri.Should().EndWith(suffix);
    }
}
