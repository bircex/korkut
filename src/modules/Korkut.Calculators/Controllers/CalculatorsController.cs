using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Korkut.Contract.Calculators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Korkut.Calculators.Controllers;

/// <summary>
/// The 27 legal/financial calculators as REST endpoints, one per calculator (mirrors the agent's
/// calc_* tools). No specific permission required — same as the tools (RequiredPermission =&gt; null),
/// just an authenticated office member.
/// </summary>
[ApiController]
[Route("api/calc")]
[Authorize]
public sealed class CalculatorsController(ICalculatorService calc) : ControllerBase
{
    /// <summary>
    /// Field constraints (required + [Range] minimum/maximum) for every calculator's request body,
    /// keyed by the same route slug used above. Reflects the [FromBody] request records directly —
    /// this IS the canonical source the FE reads at runtime, so the frontend's field metadata can
    /// never drift from what this controller actually accepts (KDM roadmap Faz 3).
    /// </summary>
    [HttpGet("schema")]
    public IActionResult Schema()
    {
        var nullability = new NullabilityInfoContext();
        var schema = new Dictionary<string, Dictionary<string, object>>();

        foreach (var method in typeof(CalculatorsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var route = method.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault();
            var bodyParam = method.GetParameters().FirstOrDefault(p => p.GetCustomAttribute<FromBodyAttribute>() is not null);
            if (route?.Template is null || bodyParam is null) continue;

            var fields = new Dictionary<string, object>();
            foreach (var prop in bodyParam.ParameterType.GetProperties())
            {
                var range = prop.GetCustomAttribute<RangeAttribute>();
                var isOptional = Nullable.GetUnderlyingType(prop.PropertyType) is not null
                    || (!prop.PropertyType.IsValueType && nullability.Create(prop).WriteState == NullabilityState.Nullable);
                fields[JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name)] = new
                {
                    required = !isOptional,
                    minimum = range?.Minimum,
                    maximum = range?.Maximum,
                };
            }
            schema[route.Template] = fields;
        }

        return Ok(schema);
    }

    [HttpPost("nafaka")]
    public IActionResult Alimony([FromBody] AlimonyRequest request) => Ok(calc.CalcAlimony(request));

    [HttpPost("yillik-izin-ucreti")]
    public IActionResult AnnualLeavePay([FromBody] AnnualLeavePayRequest request) => Ok(calc.CalcAnnualLeavePay(request));

    [HttpPost("vekalet-ucreti")]
    public IActionResult AttorneyFee([FromBody] AttorneyFeeRequest request) => Ok(calc.CalcAttorneyFee(request));

    [HttpPost("bedensel-tazminat")]
    public IActionResult BodilyInjuryCompensation([FromBody] BodilyInjuryCompensationRequest request) => Ok(calc.CalcBodilyInjuryCompensation(request));

    [HttpPost("harc")]
    public IActionResult CourtFee([FromBody] CourtFeeRequest request) => Ok(calc.CalcCourtFee(request));

    [HttpPost("sure")]
    public IActionResult Deadline([FromBody] DeadlineRequest request) => Ok(calc.CalcDeadline(request));

    [HttpGet("doviz-kurlari")]
    public async Task<IActionResult> ExchangeRates(
        [FromQuery(Name = "para_birimleri")] string[]? paraBirimleri, [FromQuery] DateOnly? tarih, CancellationToken ct)
        => Ok(await calc.GetExchangeRatesAsync(new ExchangeRatesRequest(paraBirimleri, tarih), ct));

    [HttpPost("smm")]
    public IActionResult FreelanceReceipt([FromBody] FreelanceReceiptRequest request) => Ok(calc.CalcFreelanceReceipt(request));

    [HttpPost("brut-net-maas")]
    public IActionResult GrossNetSalary([FromBody] GrossNetSalaryRequest request) => Ok(calc.CalcGrossNetSalary(request));

    [HttpPost("miras-pay")]
    public IActionResult InheritanceShare([FromBody] InheritanceShareRequest request) => Ok(calc.CalcInheritanceShare(request));

    [HttpPost("veraset-intikal")]
    public IActionResult InheritanceTax([FromBody] InheritanceTaxRequest request) => Ok(calc.CalcInheritanceTax(request));

    [HttpPost("faiz")]
    public IActionResult Interest([FromBody] InterestRequest request) => Ok(calc.CalcInterest(request));

    [HttpPost("adli-para-cezasi")]
    public IActionResult JudicialFine([FromBody] JudicialFineRequest request) => Ok(calc.CalcJudicialFine(request));

    [HttpPost("gecikme-zammi")]
    public IActionResult LatePaymentSurcharge([FromBody] LatePaymentSurchargeRequest request) => Ok(calc.CalcLatePaymentSurcharge(request));

    [HttpPost("destekten-yoksunluk")]
    public IActionResult LossOfSupport([FromBody] LossOfSupportRequest request) => Ok(calc.CalcLossOfSupport(request));

    [HttpPost("arabuluculuk-ucreti")]
    public IActionResult MediationFee([FromBody] MediationFeeRequest request) => Ok(calc.CalcMediationFee(request));

    [HttpPost("ihbar-tazminati")]
    public IActionResult NoticePay([FromBody] NoticePayRequest request) => Ok(calc.CalcNoticePay(request));

    [HttpPost("fazla-mesai")]
    public IActionResult OvertimePay([FromBody] OvertimePayRequest request) => Ok(calc.CalcOvertimePay(request));

    [HttpPost("ise-iade-tazminati")]
    public IActionResult ReinstatementPay([FromBody] ReinstatementPayRequest request) => Ok(calc.CalcReinstatementPay(request));

    [HttpPost("kira-artis")]
    public IActionResult RentIncrease([FromBody] RentIncreaseRequest request) => Ok(calc.CalcRentIncrease(request));

    [HttpPost("infaz")]
    public IActionResult SentenceExecution([FromBody] SentenceExecutionRequest request) => Ok(calc.CalcSentenceExecution(request));

    [HttpPost("kidem-tazminati")]
    public IActionResult Severance([FromBody] SeveranceRequest request) => Ok(calc.CalcSeverance(request));

    [HttpPost("damga-vergisi")]
    public IActionResult StampDuty([FromBody] StampDutyRequest request) => Ok(calc.CalcStampDuty(request));

    [HttpPost("zamanasimi")]
    public IActionResult StatuteOfLimitations([FromBody] StatuteOfLimitationsRequest request) => Ok(calc.CalcStatuteOfLimitations(request));

    [HttpPost("tapu-harci")]
    public IActionResult TitleDeedFee([FromBody] TitleDeedFeeRequest request) => Ok(calc.CalcTitleDeedFee(request));

    [HttpPost("arac-deger-kaybi")]
    public IActionResult VehicleValueLoss([FromBody] VehicleValueLossRequest request) => Ok(calc.CalcVehicleValueLoss(request));

    [HttpPost("is-kazasi-tazminati")]
    public IActionResult WorkAccidentCompensation([FromBody] WorkAccidentCompensationRequest request) => Ok(calc.CalcWorkAccidentCompensation(request));

    [HttpPost("askerlik-borclanmasi")]
    public IActionResult MilitaryServiceBorrowing([FromBody] MilitaryServiceBorrowingRequest request) => Ok(calc.CalcMilitaryServiceBorrowing(request));

    [HttpPost("yurtdisi-borclanma")]
    public IActionResult OverseasBorrowing([FromBody] OverseasBorrowingRequest request) => Ok(calc.CalcOverseasBorrowing(request));

    [HttpPost("kira-stopaji")]
    public IActionResult RentWithholding([FromBody] RentWithholdingRequest request) => Ok(calc.CalcRentWithholding(request));

    [HttpPost("emeklilik-ikramiyesi")]
    public IActionResult RetirementBonus([FromBody] RetirementBonusRequest request) => Ok(calc.CalcRetirementBonus(request));

    [HttpPost("trafik-cezasi")]
    public IActionResult TrafficFine([FromBody] TrafficFineRequest request) => Ok(calc.CalcTrafficFine(request));

    [HttpPost("issizlik-odenegi")]
    public IActionResult UnemploymentBenefit([FromBody] UnemploymentBenefitRequest request) => Ok(calc.CalcUnemploymentBenefit(request));
}
