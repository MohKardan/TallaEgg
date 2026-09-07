using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using TallaEgg.Core.Swagger;

namespace TallaEgg.AllServices.Tests;

// Guards TallaEgg.Core.Swagger.SwaggerSchemaConventions, which every API service applies to its
// generated document (issue #237). Swashbuckle closes every object schema it generates and no
// service enforces that, so the published document has to be reopened before it is served. The
// generator itself is not exercised here — none of the three services can boot in this
// environment without a live SQL Server (issue #68) — so these work on the document model
// Swashbuckle hands the filter.
public class SwaggerSchemaConventionsTests
{
    [Fact]
    public void AllowUndeclaredMembers_ClosedObjectSchema_StopsDeclaringTheKeyword()
    {
        var document = DocumentWith("OrderDto", new OpenApiSchema
        {
            Type = "object",
            AdditionalPropertiesAllowed = false
        });

        document.AllowUndeclaredMembers();

        // True with no AdditionalProperties is what makes the serializer omit the keyword, and an
        // absent additionalProperties means "allowed" in OpenAPI 3 — matching the server.
        Assert.True(document.Components.Schemas["OrderDto"].AdditionalPropertiesAllowed);
        Assert.Null(document.Components.Schemas["OrderDto"].AdditionalProperties);
    }

    [Fact]
    public void AllowUndeclaredMembers_DictionarySchema_LeavesItsValueTypeAlone()
    {
        // A schema naming a type for its extra members is describing a dictionary. That is a real
        // statement about the payload, not the blanket flag, so it must survive untouched.
        var valueType = new OpenApiSchema { Type = "string" };
        var document = DocumentWith("StringMap", new OpenApiSchema
        {
            Type = "object",
            AdditionalProperties = valueType
        });

        document.AllowUndeclaredMembers();

        Assert.Same(valueType, document.Components.Schemas["StringMap"].AdditionalProperties);
    }

    [Fact]
    public void AllowUndeclaredMembers_ExtensibleFrameworkSchema_IsNotRewritten()
    {
        // HttpValidationProblemDetails arrives already open, carrying an empty value schema. It is
        // the framework's own and the one schema in the platform that was never closed.
        var open = new OpenApiSchema { Type = "object", AdditionalProperties = new OpenApiSchema() };
        var document = DocumentWith("HttpValidationProblemDetails", open);

        document.AllowUndeclaredMembers();

        Assert.True(document.Components.Schemas["HttpValidationProblemDetails"].AdditionalPropertiesAllowed);
        Assert.NotNull(document.Components.Schemas["HttpValidationProblemDetails"].AdditionalProperties);
    }

    [Fact]
    public void AllowUndeclaredMembers_DocumentWithoutComponents_DoesNotThrow()
    {
        // The filter runs inside the swagger middleware, so anything it throws is a 500 on the
        // schema endpoint rather than a startup failure.
        var document = new OpenApiDocument { Components = null };

        document.AllowUndeclaredMembers();
    }

    private static OpenApiDocument DocumentWith(string name, OpenApiSchema schema) => new()
    {
        Components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, OpenApiSchema> { [name] = schema }
        }
    };
}
