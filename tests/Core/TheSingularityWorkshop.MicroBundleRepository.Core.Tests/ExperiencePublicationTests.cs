using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class ExperiencePublicationTests
{
    [Fact]
    public void Publication_exposes_the_addressed_experience_id()
    {
        var address = new ExperienceArtifactAddress(
            3010,
            "1.2.0",
            new string('a', 64));

        var publication = new ExperiencePublication(address);

        Assert.Equal(3010UL, publication.ExperienceId);
        Assert.Equal(address, publication.Address);
    }
}
