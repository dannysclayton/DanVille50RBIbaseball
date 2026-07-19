#nullable enable annotations

namespace StandaloneBaseball.Tests;

public sealed class PresentationAudioCoordinatorTests : IDisposable
{
    public PresentationAudioCoordinatorTests()
    {
        PresentationAudioCoordinator.ResetForTests();
    }

    [Fact]
    public void StartingNewPresentationLoop_ReplacesPriorOwner()
    {
        using var launch = new LaunchSoundPlayer();
        using var menu = new LaunchSoundPlayer();
        int launchStarts = 0;
        int menuStarts = 0;

        PresentationAudioCoordinator.StartExclusiveLoop(launch, () => launchStarts++);
        Assert.True(PresentationAudioCoordinator.IsActiveOwnerForTests(launch));

        PresentationAudioCoordinator.StartExclusiveLoop(menu, () => menuStarts++);

        Assert.Equal(1, launchStarts);
        Assert.Equal(1, menuStarts);
        Assert.False(PresentationAudioCoordinator.IsActiveOwnerForTests(launch));
        Assert.True(PresentationAudioCoordinator.IsActiveOwnerForTests(menu));
    }

    [Fact]
    public void StoppingOldOwner_DoesNotStopCurrentOwner()
    {
        using var loading = new LaunchSoundPlayer();
        using var menu = new LaunchSoundPlayer();

        PresentationAudioCoordinator.StartExclusiveLoop(loading, () => { });
        PresentationAudioCoordinator.StartExclusiveLoop(menu, () => { });
        PresentationAudioCoordinator.Stop(loading);

        Assert.True(PresentationAudioCoordinator.IsActiveOwnerForTests(menu));
        PresentationAudioCoordinator.Stop(menu);
        Assert.False(PresentationAudioCoordinator.IsActiveOwnerForTests(menu));
    }

    public void Dispose()
    {
        PresentationAudioCoordinator.ResetForTests();
    }
}
