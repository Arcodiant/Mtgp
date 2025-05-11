using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record ShaderStageInfo(ShaderStage Stage, IdOrRef Shader, string EntryPoint);
