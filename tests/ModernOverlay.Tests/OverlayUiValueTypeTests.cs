using ModernOverlay.UI;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiValueTypeTests
{
    [TestMethod]
    public void ThicknessConstructorsSetExpectedEdges()
    {
        Assert.AreEqual(new Thickness(4f, 4f, 4f, 4f), new Thickness(4f));
        Assert.AreEqual(new Thickness(3f, 5f, 3f, 5f), new Thickness(3f, 5f));

        Thickness thickness = new(1f, 2f, 3f, 4f);

        Assert.AreEqual(4f, thickness.Horizontal);
        Assert.AreEqual(6f, thickness.Vertical);
        Assert.AreEqual(Thickness.Zero, new Thickness(0f));
    }

    [TestMethod]
    public void UiSizeDefaultsUseZeroSize()
    {
        Assert.AreEqual(new UiSize(0f, 0f), UiSize.Zero);
        Assert.AreEqual(default, UiSize.Zero);
    }

    [TestMethod]
    public void UiConstraintsValidateInvalidRanges()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new UiConstraints(-1f, 0f, 10f, 10f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new UiConstraints(0f, float.NaN, 10f, 10f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new UiConstraints(0f, 0f, -1f, 10f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new UiConstraints(0f, 0f, float.NegativeInfinity, 10f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new UiConstraints(20f, 0f, 10f, 10f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new UiConstraints(0f, 20f, 10f, 10f));
    }

    [TestMethod]
    public void UiConstraintsClampSizesToRange()
    {
        UiConstraints constraints = new(10f, 20f, 100f, 200f);

        Assert.AreEqual(new SizeF(10f, 20f), constraints.Constrain(new SizeF(1f, 2f)));
        Assert.AreEqual(new SizeF(50f, 60f), constraints.Constrain(new SizeF(50f, 60f)));
        Assert.AreEqual(new SizeF(100f, 200f), constraints.Constrain(new SizeF(500f, 600f)));
        Assert.AreEqual(new UiConstraints(25f, 20f, 100f, 200f), constraints.WithMinWidth(25f));
        Assert.AreEqual(new UiConstraints(10f, 30f, 100f, 200f), constraints.WithMinHeight(30f));
        Assert.AreEqual(new UiConstraints(10f, 20f, 80f, 200f), constraints.WithMaxWidth(80f));
        Assert.AreEqual(new UiConstraints(10f, 20f, 100f, 120f), constraints.WithMaxHeight(120f));
    }

    [TestMethod]
    public void UiConstraintsUnboundedAllowPositiveInfinityMaximums()
    {
        Assert.AreEqual(new UiConstraints(0f, 0f, float.PositiveInfinity, float.PositiveInfinity), UiConstraints.Unbounded);
        Assert.AreEqual(new SizeF(500f, 600f), UiConstraints.Unbounded.Constrain(new SizeF(500f, 600f)));
    }

    [TestMethod]
    public void GridLengthFactoriesSetUnitTypes()
    {
        Assert.AreEqual(new GridLength(1f, GridUnitType.Auto), GridLength.Auto);
        Assert.AreEqual(new GridLength(24f, GridUnitType.Pixel), GridLength.Pixel(24f));
        Assert.AreEqual(new GridLength(2f, GridUnitType.Star), GridLength.Star(2f));
        Assert.AreEqual(GridUnitType.Star, GridLength.Star().UnitType);
    }

    [TestMethod]
    public void GridDefinitionStoresMutableLength()
    {
        GridDefinition definition = new(GridLength.Auto)
        {
            Length = GridLength.Pixel(42f),
        };

        Assert.AreEqual(GridLength.Pixel(42f), definition.Length);
    }
}
