namespace Billing.Application.Exceptions;

public abstract class BillingApplicationException : Exception
{
    public string Code { get; }

    protected BillingApplicationException(string code, string message) : base(message)
    {
        Code = code;
    }

    protected BillingApplicationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

public sealed class ValidationException : BillingApplicationException
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(IEnumerable<string> errors)
        : base("VALIDATION_ERROR", "The request is invalid.")
    {
        Errors = errors.ToArray();
    }
}

public sealed class NotFoundException : BillingApplicationException
{
    public NotFoundException(string message) : base("NOT_FOUND", message)
    {
    }
}

public sealed class ConflictException : BillingApplicationException
{
    public ConflictException(string code, string message) : base(code, message)
    {
    }
}

public sealed class PersistenceException : BillingApplicationException
{
    public PersistenceException(string message, Exception innerException)
        : base("PERSISTENCE_ERROR", message, innerException)
    {
    }
}

public sealed class SunatRejectionException : BillingApplicationException
{
    public string? ResponseCode { get; }
    public string? Notes { get; }

    public SunatRejectionException(string message, string? responseCode = null, string? notes = null)
        : base("SUNAT_REJECTION", message)
    {
        ResponseCode = responseCode;
        Notes = notes;
    }
}

public sealed class SunatUnavailableException : BillingApplicationException
{
    public SunatUnavailableException(string message, Exception? innerException = null)
        : base("SUNAT_UNAVAILABLE", message, innerException!)
    {
    }
}

public sealed class TransientCommunicationException : BillingApplicationException
{
    public TransientCommunicationException(string message, Exception? innerException = null)
        : base("TRANSIENT_COMMUNICATION_ERROR", message, innerException!)
    {
    }
}

public sealed class InternalApplicationException : BillingApplicationException
{
    public InternalApplicationException(string message, Exception? innerException = null)
        : base("INTERNAL_ERROR", message, innerException!)
    {
    }
}
