using System.Text.Json.Serialization;

namespace Mtgp.Messages.Resources;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "resourceType")]
[JsonDerivedType(typeof(CreateShaderInfo), CreateShaderInfo.ResourceType)]
public class ResourceInfo(string? reference = null)
{
    public string? Reference { get; init; } = reference;
}

public record ResourceCreateResult(int ResourceId, ResourceCreateResultType Result)
{
    public static ResourceCreateResult InvalidRequest => new(0, ResourceCreateResultType.InvalidRequest);
    public static ResourceCreateResult InvalidReference => new(0, ResourceCreateResultType.InvalidReference);
    public static ResourceCreateResult InternalError => new(0, ResourceCreateResultType.InternalError);
    public static ResourceCreateResult FailedReference => new(0, ResourceCreateResultType.FailedReference);
};

public enum ResourceCreateResultType
{
    Success,
    InvalidRequest,
    InvalidReference,
    InternalError,
    FailedReference
}

public interface ICreateResourceInfo
{
    static abstract string ResourceType { get; }
}
