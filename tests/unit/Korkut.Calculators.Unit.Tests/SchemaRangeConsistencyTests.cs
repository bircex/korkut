using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Korkut.Contract.Calculators;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

/// <summary>
/// The LLM-facing JSON schema hand-written in each Calc/*Tool.cs carries its own "minimum"/
/// "maximum" text; the [Range] attribute on the matching Contract request record is the
/// canonical source of that same constraint (KDM roadmap Faz 3, mirrored by the FE-facing
/// GET /api/calc/schema endpoint). This locks the two together so editing one without the
/// other fails a test instead of silently drifting.
/// </summary>
public class SchemaRangeConsistencyTests
{
    private static void AssertRangeMatches(IAgentTool tool, Type requestType, string schemaKey, string propertyName)
    {
        var prop = requestType.GetProperty(propertyName) ?? throw new InvalidOperationException($"{requestType.Name}.{propertyName} not found");
        var range = prop.GetCustomAttribute<RangeAttribute>() ?? throw new InvalidOperationException($"{requestType.Name}.{propertyName} has no [Range]");

        var field = tool.Spec.Parameters.GetProperty("properties").GetProperty(schemaKey);
        Assert.Equal(Convert.ToDecimal(range.Minimum), field.GetProperty("minimum").GetDecimal());
        Assert.Equal(Convert.ToDecimal(range.Maximum), field.GetProperty("maximum").GetDecimal());
    }

    [Fact]
    public void JudicialFine_matches_contract_range()
    {
        var tool = new JudicialFineTool(TestCalc.Service);
        AssertRangeMatches(tool, typeof(JudicialFineRequest), "gun_sayisi", nameof(JudicialFineRequest.GunSayisi));
        AssertRangeMatches(tool, typeof(JudicialFineRequest), "gunluk_miktar", nameof(JudicialFineRequest.GunlukMiktar));
    }

    [Fact]
    public void BodilyInjuryCompensation_matches_contract_range()
    {
        var tool = new BodilyInjuryCompensationTool(TestCalc.Service);
        AssertRangeMatches(tool, typeof(BodilyInjuryCompensationRequest), "yas", nameof(BodilyInjuryCompensationRequest.Yas));
        AssertRangeMatches(tool, typeof(BodilyInjuryCompensationRequest), "maluliyet_orani", nameof(BodilyInjuryCompensationRequest.MaluliyetOrani));
        AssertRangeMatches(tool, typeof(BodilyInjuryCompensationRequest), "kusur_orani", nameof(BodilyInjuryCompensationRequest.KusurOrani));
    }

    [Fact]
    public void GrossNetSalary_matches_contract_range()
    {
        var tool = new GrossNetSalaryTool(TestCalc.Service);
        AssertRangeMatches(tool, typeof(GrossNetSalaryRequest), "ay", nameof(GrossNetSalaryRequest.Ay));
    }

    [Fact]
    public void LossOfSupport_matches_contract_range()
    {
        var tool = new LossOfSupportTool(TestCalc.Service);
        AssertRangeMatches(tool, typeof(LossOfSupportRequest), "yas", nameof(LossOfSupportRequest.Yas));
        AssertRangeMatches(tool, typeof(LossOfSupportRequest), "kusur_orani", nameof(LossOfSupportRequest.KusurOrani));
    }

    [Fact]
    public void WorkAccidentCompensation_matches_contract_range()
    {
        var tool = new WorkAccidentCompensationTool(TestCalc.Service);
        AssertRangeMatches(tool, typeof(WorkAccidentCompensationRequest), "yas", nameof(WorkAccidentCompensationRequest.Yas));
        AssertRangeMatches(
            tool, typeof(WorkAccidentCompensationRequest), "surekli_is_goremezlik_orani", nameof(WorkAccidentCompensationRequest.SurekliIsGoremezlikOrani));
        AssertRangeMatches(tool, typeof(WorkAccidentCompensationRequest), "isveren_kusur_orani", nameof(WorkAccidentCompensationRequest.IsverenKusurOrani));
    }
}
