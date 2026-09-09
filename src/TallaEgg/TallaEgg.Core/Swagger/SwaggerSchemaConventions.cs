using Microsoft.OpenApi.Models;

namespace TallaEgg.Core.Swagger;

/// <summary>
/// Makes a generated OpenAPI document describe what these services actually do.
/// </summary>
public static class SwaggerSchemaConventions
{
    /// <summary>
    /// Removes the blanket <c>additionalProperties: false</c> Swashbuckle stamps on every object
    /// schema it generates.
    /// </summary>
    /// <remarks>
    /// No service enforces that keyword and none is meant to (issue #237). <c>System.Text.Json</c>
    /// ignores unknown members and nothing sets <c>JsonUnmappedMemberHandling.Disallow</c>, so a
    /// request body carrying undeclared fields is accepted — deliberately: that tolerance is what
    /// lets an un-updated bot keep talking to a post-#235 API. A client generated from the closed
    /// schema, or one running the body through a validator, would reject a request the server
    /// would have taken, and the failure lands before the request is ever sent.
    ///
    /// Responses are opened too, and for a reason of their own rather than as collateral: adding a
    /// field to a response is a MINOR change under <c>STANDARDS.md</c> §11, so a closed response
    /// schema promises something the platform does not — it would turn every such addition into a
    /// break for any client that validates what it receives.
    ///
    /// It is cleared on the finished document rather than at generation time. The hook Swashbuckle
    /// offers for the latter is an <c>ISchemaFilter</c>, since <c>SchemaGeneratorOptions</c>
    /// carries no switch for the flag — and when this was written that hook was out of reach: an
    /// <c>ISchemaFilter</c> is a Swashbuckle type, and Users.Api pinned Swashbuckle 7.0.0 while
    /// Orders and Wallet were on 9.0.3, so a 9.0.3 reference in this assembly reached Users.Api as
    /// a package downgrade (NU1605) that <c>TreatWarningsAsErrors</c> turned into a build failure.
    ///
    /// Issue #243 put all three on 9.0.3, so that reference builds now and the filter is
    /// available. It has deliberately not been taken, because for every schema that is a component
    /// the two approaches produce the same document, and this one is already written and tested.
    /// What it cannot reach is a schema Swashbuckle inlines into an operation instead of
    /// referencing from <c>Components</c> — the loop below only ever sees components. Every object
    /// schema is a component today, so that gap is hypothetical; the day an endpoint binds an
    /// anonymous body is the day moving to an <c>ISchemaFilter</c> earns its change.
    ///
    /// A schema that names a type for its extra members is describing a dictionary, which is a
    /// real statement about the payload; only the blanket flag is cleared.
    /// </remarks>
    public static void AllowUndeclaredMembers(this OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var schemas = document.Components?.Schemas;
        if (schemas is null) return;

        foreach (var schema in schemas.Values)
        {
            if (schema.AdditionalProperties is null)
            {
                schema.AdditionalPropertiesAllowed = true;
            }
        }
    }
}
