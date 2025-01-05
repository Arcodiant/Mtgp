using Mtgp.Messages;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Mtgp.Comms;

internal class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
{
	public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
	{
		JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

		if (jsonTypeInfo.Type == typeof(MtgpRequest))
		{
			jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
			{
				TypeDiscriminatorPropertyName = "command",
				IgnoreUnrecognizedTypeDiscriminators = false,
				UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
				DerivedTypes =
					{
						new JsonDerivedType(typeof(SendRequest), "core.shader.send"),
						new JsonDerivedType(typeof(SetDefaultPipeRequest), "core.shader.setDefaultPipeline"),
						new JsonDerivedType(typeof(GetDataRequest), "core.data.getData"),
						new JsonDerivedType(typeof(SetDataRequest), "core.data.setData"),
						new JsonDerivedType(typeof(AddBindVertexBuffersRequest), "core.shader.addBindVertexBuffers"),
						new JsonDerivedType(typeof(AddClearBufferActionRequest), "core.shader.addClearBufferAction"),
						new JsonDerivedType(typeof(AddCopyBufferToImageActionRequest), "core.shader.addCopyBufferToImageAction"),
						new JsonDerivedType(typeof(AddDrawActionRequest), "core.shader.addDrawAction"),
						new JsonDerivedType(typeof(AddIndirectDrawActionRequest), "core.shader.addIndirectDrawAction"),
						new JsonDerivedType(typeof(AddPresentActionRequest), "core.shader.addPresentAction"),
						new JsonDerivedType(typeof(AddRunPipelineActionRequest), "core.shader.addRunPipelineAction"),
						new JsonDerivedType(typeof(CreateResourceRequest), "core.shader.createResource"),
						new JsonDerivedType(typeof(GetPresentImageRequest), "core.shader.getPresentImage"),
						new JsonDerivedType(typeof(OpenUrlRequest), "core.web.openUrl"),
						new JsonDerivedType(typeof(ResetActionListRequest), "core.shader.resetActionList"),
						new JsonDerivedType(typeof(SetActionTriggerRequest), "core.shader.setActionTrigger"),
						new JsonDerivedType(typeof(SetBufferDataRequest), "core.shader.setBufferData"),
						new JsonDerivedType(typeof(SetTimerTriggerRequest), "core.shader.setTimerTrigger"),
						new JsonDerivedType(typeof(AddTriggerPipeActionRequest), "core.shader.triggerPipeAction"),
						new JsonDerivedType(typeof(ClearStringSplitPipelineRequest), "core.shader.clearStringSplitPipeline"),
					}
			};
		}

		return jsonTypeInfo;
	}
}
