﻿using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record CreateRenderPipelineInfo(CreateRenderPipelineInfo.ShaderStageInfo[] ShaderStages,
									   CreateRenderPipelineInfo.VertexInputInfo VertexInput,
									   Rect3D Viewport,
									   Rect3D[]? Scissors,
									   PolygonMode PolygonMode,
									   string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "renderPipeline";

	public record ShaderStageInfo(ShaderStage Stage, IdOrRef Shader, string EntryPoint);

	public record VertexInputInfo(VertexInputInfo.VertexBufferBinding[] VertexBufferBindings, VertexInputInfo.VertexAttribute[] VertexAttributes)
	{
		public record VertexBufferBinding(int Binding, int Stride, InputRate InputRate);
		public record VertexAttribute(int Location, int Binding, ShaderType Type, int Offset);
	}
}