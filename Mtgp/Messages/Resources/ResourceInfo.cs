using System.Text.Json.Serialization;

namespace Mtgp.Messages.Resources;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "resourceType")]
[JsonDerivedType(typeof(CreateShaderInfo), CreateShaderInfo.ResourceType)]
[JsonDerivedType(typeof(CreateBufferInfo), CreateBufferInfo.ResourceType)]
[JsonDerivedType(typeof(CreateBufferViewInfo), CreateBufferViewInfo.ResourceType)]
[JsonDerivedType(typeof(CreateImageInfo), CreateImageInfo.ResourceType)]
[JsonDerivedType(typeof(CreateRenderPipelineInfo), CreateRenderPipelineInfo.ResourceType)]
[JsonDerivedType(typeof(CreateComputePipelineInfo), CreateComputePipelineInfo.ResourceType)]
[JsonDerivedType(typeof(CreateActionListInfo), CreateActionListInfo.ResourceType)]
[JsonDerivedType(typeof(CreatePipeInfo), CreatePipeInfo.ResourceType)]
[JsonDerivedType(typeof(CreateStringSplitPipelineInfo), CreateStringSplitPipelineInfo.ResourceType)]
public record ResourceInfo(string? Reference = null);

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
