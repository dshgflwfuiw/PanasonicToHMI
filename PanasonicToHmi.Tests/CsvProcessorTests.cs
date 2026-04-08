using Xunit;
using PanasonicToHmi.Core;

namespace PanasonicToHmi.Tests;

public class CsvProcessorTests
{
    [Fact]
    public void Process_WithValidData_ReturnsSuccess()
    {
        var csv = TestData.GetSampleCsv();
        var processor = new CsvProcessor(BrandTemplate.Weinview);
        
        var result = processor.Process(csv, out var errors, null);
        
        Assert.NotNull(result);
        Assert.Empty(errors);
        Assert.Contains("LB", result); // Weinview uses LB for X
        Assert.Contains("LW", result); // Weinview uses LW for D
    }

    [Fact]
    public void Process_WithInvalidData_CollectsErrors()
    {
        var csv = TestData.GetInvalidCsv();
        var processor = new CsvProcessor(BrandTemplate.Weinview);
        
        var result = processor.Process(csv, out var errors, null);
        
        Assert.NotNull(result);
        Assert.NotEmpty(errors);
        Assert.Equal(2, errors.Count); // Two invalid rows
    }

    [Theory]
    [InlineData(BrandTemplate.Weinview, "X0", "LB0")]
    [InlineData(BrandTemplate.Siemens, "X0", "I0.0")]
    [InlineData(BrandTemplate.Mitsubishi, "X0", "X0")]
    [InlineData(BrandTemplate.Omron, "X0", "0.00")]
    [InlineData(BrandTemplate.Delta, "X0", "X0")]
    public void Process_DifferentBrands_ProducesCorrectAddress(BrandTemplate brand, string inputAddr, string expectedPrefix)
    {
        var csv = $"Variable,Address,Comment\nTestVar,{inputAddr},Test";
        var processor = new CsvProcessor(brand);
        
        var result = processor.Process(csv, out var errors, null);
        
        Assert.Empty(errors);
        Assert.Contains(expectedPrefix, result);
    }

    [Fact]
    public void Process_WithProgressCallback_InvokesCallback()
    {
        var csv = TestData.GetSampleCsv();
        var processor = new CsvProcessor(BrandTemplate.Weinview);
        
        int callbackCount = 0;
        void ProgressCallback(int percent, string status) => callbackCount++;
        
        var result = processor.Process(csv, out var errors, ProgressCallback);
        
        Assert.True(callbackCount > 0, "Progress callback should be invoked");
    }
}
