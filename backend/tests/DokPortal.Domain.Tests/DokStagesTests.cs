using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using Xunit;

namespace DokPortal.Domain.Tests;

public class DokStagesTests
{
    [Fact]
    public void Baptism_HasFiveStagesEndingWithGraduate()
    {
        Assert.Equal(
            new[] { DokStage.Prekatechumenate, DokStage.Catechumenate, DokStage.Election, DokStage.Neophyte, DokStage.Graduate },
            DokStages.For(DokPath.BaptismCandidate));
    }

    [Fact]
    public void Confirmation_HasEvangelizationAndGraduate()
    {
        Assert.Equal(new[] { DokStage.Evangelization, DokStage.Graduate }, DokStages.For(DokPath.Confirmation));
    }

    [Theory]
    [InlineData(DokPath.Communion)]
    [InlineData(DokPath.Conversion)]
    [InlineData(DokPath.ReturnToUnity)]
    public void EucharistConversionAndReturnToUnity_ShareTheSameStages(DokPath path)
    {
        Assert.Equal(new[] { DokStage.Evangelization, DokStage.CloserFormation, DokStage.Graduate }, DokStages.For(path));
    }

    [Fact]
    public void EveryPathEndsWithGraduate_AndStartsWithItsFirstStage()
    {
        foreach (var path in Enum.GetValues<DokPath>())
        {
            Assert.Equal(DokStage.Graduate, DokStages.For(path).Last());
            Assert.Equal(DokStages.For(path)[0], DokStages.First(path));
        }
        Assert.Equal(DokStage.Prekatechumenate, DokStages.First(DokPath.BaptismCandidate));
        Assert.Equal(DokStage.Evangelization, DokStages.First(DokPath.Confirmation));
    }

    [Theory]
    [InlineData(DokPath.Confirmation, DokStage.Evangelization, true)]
    [InlineData(DokPath.Confirmation, DokStage.CloserFormation, false)]
    [InlineData(DokPath.Confirmation, DokStage.Election, false)]
    [InlineData(DokPath.BaptismCandidate, DokStage.Evangelization, false)]
    [InlineData(DokPath.BaptismCandidate, DokStage.Neophyte, true)]
    [InlineData(DokPath.Communion, DokStage.CloserFormation, true)]
    [InlineData(DokPath.ReturnToUnity, DokStage.Graduate, true)]
    public void IsValid_AcceptsOnlyTheStagesOfThePath(DokPath path, DokStage stage, bool expected)
    {
        Assert.Equal(expected, DokStages.IsValid(path, stage));
    }

    [Fact]
    public void All_ListsEveryStageExactlyOnce_AndEachHasAPolishLabel()
    {
        Assert.Equal(Enum.GetValues<DokStage>().OrderBy(s => (int)s), DokStages.All.OrderBy(s => (int)s));
        Assert.All(DokStages.All, stage => Assert.NotEqual(stage.ToString(), DokStages.Label(stage)));
        Assert.Equal("Formacja bliższa", DokStages.Label(DokStage.CloserFormation));
    }

    [Fact]
    public void PathLabels_UseEucharystiaInsteadOfStolPanski()
    {
        Assert.Equal("Eucharystia", DokStages.Label(DokPath.Communion));
        Assert.Equal("Bierzmowanie", DokStages.Label(DokPath.Confirmation));
        Assert.Equal("Kandydaci do Chrztu", DokStages.Label(DokPath.BaptismCandidate));
    }

    [Fact]
    public void TheStoredNumbers_StayStable()
    {
        Assert.Equal(3, (int)DokStage.Graduate);
        Assert.Equal(4, (int)DokStage.Evangelization);
        Assert.Equal(6, (int)DokStage.Prekatechumenate);
    }
}
