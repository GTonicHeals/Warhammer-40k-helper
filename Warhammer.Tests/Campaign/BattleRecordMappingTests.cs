using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Models.Campaign;
using Warhammer.Tests.TestInfrastructure;

namespace Warhammer.Tests.Campaign;

public class BattleRecordMappingTests : SqliteTestBase
{
    private CampaignManagerService Svc() => new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    [Fact]
    public async Task Mustered_battle_round_trips_and_unit_result_mutation_persists()
    {
        var campaign = await Svc().LoadCampaignAsync();
        campaign.BattleHistory.Add(new BattleRecord
        {
            Status = BattleStatus.Mustered, PointsLimit = 2000, Player1OrderId = "o1",
            UnitResults = { new BattleUnitResult { CardId = "c1", UnitName = "U", Points = 90 } }
        });
        await Svc().SaveCampaignAsync(campaign);

        var loaded = await Svc().LoadCampaignAsync();
        var battle = loaded.BattleHistory.Single();
        battle.Status.Should().Be(BattleStatus.Mustered);
        battle.PointsLimit.Should().Be(2000);
        battle.Player1Id.Should().Be("p1");
        battle.UnitResults.Single().CardId.Should().Be("c1");

        battle.UnitResults[0].Destroyed = true;
        await Svc().SaveCampaignAsync(loaded);

        (await Svc().LoadCampaignAsync()).BattleHistory.Single().UnitResults.Single().Destroyed.Should().BeTrue();
    }
}
