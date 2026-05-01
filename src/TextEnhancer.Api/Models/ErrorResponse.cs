namespace TextEnhancer.Api.Models;

public record ErrorResponse(string Code, string Message, string? TraceId = null);
