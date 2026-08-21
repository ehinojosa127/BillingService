using Billing.Application.Commands;
using Billing.Application.DTOs;
using Billing.WebApi.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class DocumentsController(IMediator mediator) : ControllerBase
{
    [HttpPost("invoices")]
    [ProducesResponseType(typeof(DocumentResultDto), StatusCodes.Status201Created)]
    public Task<ActionResult<DocumentResultDto>> IssueInvoice([FromBody] IssueDocumentRequest request) =>
        Issue("01", request);

    [HttpPost("receipts")]
    [ProducesResponseType(typeof(DocumentResultDto), StatusCodes.Status201Created)]
    public Task<ActionResult<DocumentResultDto>> IssueReceipt([FromBody] IssueDocumentRequest request) =>
        Issue("03", request);

    [HttpPost("credit-notes")]
    [ProducesResponseType(typeof(DocumentResultDto), StatusCodes.Status201Created)]
    public Task<ActionResult<DocumentResultDto>> IssueCreditNote([FromBody] IssueDocumentRequest request) =>
        Issue("07", request);

    [HttpPost("debit-notes")]
    [ProducesResponseType(typeof(DocumentResultDto), StatusCodes.Status201Created)]
    public Task<ActionResult<DocumentResultDto>> IssueDebitNote([FromBody] IssueDocumentRequest request) =>
        Issue("08", request);

    [HttpPost("shipping-guides")]
    [ProducesResponseType(typeof(DocumentResultDto), StatusCodes.Status201Created)]
    public Task<ActionResult<DocumentResultDto>> IssueShippingGuide([FromBody] IssueDocumentRequest request) =>
        Issue("09", request);

    [HttpGet("documents")]
    public async Task<ActionResult<PagedResultDto<DocumentListItemDto>>> GetDocuments(
        [FromQuery] string? documentType,
        [FromQuery] string? series,
        [FromQuery] string? status,
        [FromQuery] string? sunatStatus,
        [FromQuery] string? externalReference,
        [FromQuery] string? externalId,
        [FromQuery] string? externalSystem,
        [FromQuery] string? search,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        return Ok(await mediator.Send(new Application.Queries.GetDocumentsQuery(
            documentType,
            series,
            status,
            sunatStatus,
            externalReference,
            externalId,
            externalSystem,
            search,
            dateFrom,
            dateTo,
            minAmount,
            maxAmount,
            skip,
            take)));
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<DocumentResultDto>> GetDocument(Guid id) =>
        Ok(await mediator.Send(new Application.Queries.GetDocumentQuery(id)));

    [HttpGet("documents/{id:guid}/status")]
    public async Task<ActionResult<DocumentStatusDto>> GetStatus(Guid id) =>
        Ok(await mediator.Send(new Application.Queries.GetDocumentStatusQuery(id)));

    [HttpGet("documents/{id:guid}/xml")]
    public Task<IActionResult> GetXml(Guid id) => FileAsync(id, "xml");

    [HttpGet("documents/{id:guid}/pdf")]
    public Task<IActionResult> GetPdf(Guid id, [FromQuery] string? template) => FileAsync(id, "pdf", template);

    [HttpPost("documents/{id:guid}/pdf/regenerate")]
    public async Task<ActionResult<RegeneratePdfResultDto>> RegeneratePdf(Guid id, [FromBody] RegeneratePdfRequest? request)
    {
        var result = await mediator.Send(new RegenerateDocumentPdfCommand(
            id,
            request?.TemplateType ?? request?.Template ?? Request.Query["template"].FirstOrDefault()));
        return Ok(result);
    }

    [HttpGet("documents/{id:guid}/cdr")]
    public Task<IActionResult> GetCdr(Guid id) => FileAsync(id, "cdr");

    [HttpPost("documents/{id:guid}/consult")]
    public async Task<ActionResult<DocumentResultDto>> Consult(Guid id)
    {
        var result = await mediator.Send(new ConsultSunatStatusCommand(id, HttpContext.Items[Billing.Shared.BillingHeaders.CorrelationId]?.ToString(), User.Identity?.Name));
        return Ok(result);
    }

    [HttpPost("documents/{id:guid}/retry")]
    public async Task<ActionResult<DocumentResultDto>> Retry(Guid id)
    {
        var result = await mediator.Send(new RetrySubmissionCommand(id, HttpContext.Items[Billing.Shared.BillingHeaders.CorrelationId]?.ToString(), User.Identity?.Name));
        return Ok(result);
    }

    [HttpPost("documents/{id:guid}/cancel")]
    public async Task<ActionResult<DocumentResultDto>> Cancel(Guid id, [FromBody] CancelRequest? request)
    {
        var result = await mediator.Send(new CancelDocumentCommand(id, request?.Reason, HttpContext.Items[Billing.Shared.BillingHeaders.CorrelationId]?.ToString(), User.Identity?.Name));
        return Ok(result);
    }

    private async Task<ActionResult<DocumentResultDto>> Issue(string documentType, IssueDocumentRequest request)
    {
        var result = await mediator.Send(request.ToCommand(documentType, HttpContext));
        return Created($"/api/v1/documents/{result.Id}", result);
    }

    private async Task<IActionResult> FileAsync(Guid id, string kind, string? template = null)
    {
        var file = await mediator.Send(new Application.Queries.GetDocumentFileQuery(id, kind, template));
        return File(file.Content, file.ContentType, file.FileName);
    }
}

public sealed record CancelRequest(string? Reason);

public sealed record RegeneratePdfRequest(string? Template, string? TemplateType);

[ApiController]
[Authorize]
[Route("api/v1/issuer")]
public sealed class IssuerController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IssuerDto>> Get() =>
        Ok(await mediator.Send(new Application.Queries.GetIssuerQuery()));

    [HttpPut]
    public async Task<ActionResult<IssuerDto>> Upsert([FromBody] UpsertIssuerRequest request)
    {
        var result = await mediator.Send(new UpsertIssuerCommand
        {
            Ruc = request.Ruc,
            LegalName = request.LegalName,
            TradeName = request.TradeName,
            AddressLine = request.AddressLine,
            Ubigeo = request.Ubigeo,
            Department = request.Department,
            Province = request.Province,
            District = request.District,
            CountryCode = request.CountryCode,
            Urbanization = request.Urbanization,
            EstablishmentCode = request.EstablishmentCode,
            Email = request.Email,
            Phone = request.Phone
        });
        return Ok(result);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/capabilities")]
public sealed class CapabilitiesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IssuerCapabilitiesDto>> Get() =>
        Ok(await mediator.Send(new Application.Queries.GetCapabilitiesQuery()));
}

[ApiController]
[Authorize]
[Route("api/v1/series")]
public sealed class SeriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeriesDto>>> Get() =>
        Ok(await mediator.Send(new Application.Queries.GetSeriesQuery()));

    [HttpPost]
    public async Task<ActionResult<SeriesDto>> Create([FromBody] CreateSeriesRequest request)
    {
        var result = await mediator.Send(new CreateSeriesCommand(request.DocumentType, request.Series));
        return Created($"/api/v1/series/{result.Id}", result);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/pdf")]
public sealed class PdfRenderController(IMediator mediator) : ControllerBase
{
    [HttpPost("render")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Render([FromBody] RenderPdfRequest request)
    {
        var pdf = await mediator.Send(request.ToCommand());
        return File(pdf, "application/pdf", $"{request.FullNumber}.pdf");
    }
}

public sealed record RenderPdfItemRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total);

public sealed record RenderPdfRequest
{
    public string? PdfTemplate { get; init; }
    public bool ShowQr { get; init; }
    public bool ShowTaxBreakdown { get; init; } = true;
    public required string TypeLabel { get; init; }
    public required string Series { get; init; }
    public required int Number { get; init; }
    public required string FullNumber { get; init; }
    public required string IssueDate { get; init; }
    public string? ExternalReference { get; init; }
    public required string RecipientName { get; init; }
    public string RecipientIdentityType { get; init; } = "1";
    public required string RecipientIdentityNumber { get; init; }
    public string? RecipientAddress { get; init; }
    public required IReadOnlyList<RenderPdfItemRequest> Items { get; init; }
    public required decimal PayableAmount { get; init; }
    public string? Observation { get; init; }
    public string? FooterText { get; init; }

    public RenderPdfCommand ToCommand() => new()
    {
        PdfTemplate = PdfTemplate,
        ShowQr = ShowQr,
        ShowTaxBreakdown = ShowTaxBreakdown,
        TypeLabel = TypeLabel,
        Series = Series,
        Number = Number,
        FullNumber = FullNumber,
        IssueDate = IssueDate,
        ExternalReference = ExternalReference,
        RecipientName = RecipientName,
        RecipientIdentityType = RecipientIdentityType,
        RecipientIdentityNumber = RecipientIdentityNumber,
        RecipientAddress = RecipientAddress,
        Items = Items.Select(item => new RenderPdfItemDto(
            item.Description,
            item.Quantity,
            item.UnitPrice,
            item.Total)).ToArray(),
        PayableAmount = PayableAmount,
        Observation = Observation,
        FooterText = FooterText
    };
}

[ApiController]
[Authorize]
[Route("api/v1/pdf-templates")]
public sealed class PdfTemplatesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PdfTemplateDto>>> Get() =>
        Ok(await mediator.Send(new Application.Queries.GetPdfTemplatesQuery()));

    [HttpPut("{code}")]
    public async Task<ActionResult<PdfTemplateDto>> Upsert(string code, [FromBody] UpsertPdfTemplateRequest request)
    {
        var result = await mediator.Send(new UpsertPdfTemplateCommand
        {
            Code = code,
            Name = request.Name,
            TradeName = request.TradeName,
            PrimaryColor = request.PrimaryColor,
            FooterText = request.FooterText,
            CommercialText = request.CommercialText,
            SetAsDefault = request.SetAsDefault
        });
        return Ok(result);
    }

    [HttpPost("{code}/default")]
    public async Task<ActionResult<PdfTemplateDto>> SetDefault(string code) =>
        Ok(await mediator.Send(new SetDefaultPdfTemplateCommand(code)));

    [HttpPost("{code}/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<PdfTemplateDto>> UploadLogo(string code, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var result = await mediator.Send(new UploadPdfTemplateLogoCommand(
            code,
            file.FileName,
            file.ContentType,
            memory.ToArray()));
        return Ok(result);
    }
}

public sealed record UpsertPdfTemplateRequest(
    string Name,
    string? TradeName,
    string? PrimaryColor,
    string? FooterText,
    string? CommercialText,
    bool SetAsDefault);
