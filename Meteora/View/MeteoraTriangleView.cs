using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meteora.Data;
using Vulkan;

namespace Meteora.View
{
	public class MeteoraTriangleView : MeteoraViewBase
	{
		public override void DrawFrame()
		{
			base.DrawFrame();
		}

		public override void Initialize(InstanceCreateData data)
		{
			base.Initialize(data);
		}

		protected override PipelineShaderStageCreateInfo[] CreateShaderStages()
		{
			var fragModule = CreateShaderModule(File.ReadAllBytes(@"Shaders/Fragment/frag.spv"));
			var vertModule = CreateShaderModule(File.ReadAllBytes(@"Shaders/Vertex/vert.spv"));

			var vertShaderStageInfo = new PipelineShaderStageCreateInfo
			{
				Stage = ShaderStageFlags.Vertex,
				Module = vertModule,
				Name = "main"
			};

			var fragShaderStageInfo = new PipelineShaderStageCreateInfo
			{
				Stage = ShaderStageFlags.Fragment,
				Module = fragModule,
				Name = "main"
			};

			var shaderStages = new PipelineShaderStageCreateInfo[]
			{
				vertShaderStageInfo,
				fragShaderStageInfo
			};

			return shaderStages;
		}

		protected override void InitCommandBuffer()
		{
			for (int i = 0; i < bufferSize; i++)
			{
				var beginInfo = new CommandBufferBeginInfo
				{
					Flags = CommandBufferUsageFlags.SimultaneousUse
				};
				commandBuffers[i].Begin(beginInfo);

				var clearColor = new ClearValue
				{
					Color = new ClearColorValue(new uint[] { 255, 0, 100, 255 })
				};
				var renderPassInfo = new RenderPassBeginInfo
				{
					RenderPass = renderPass,
					Framebuffer = framebuffers[i],
					RenderArea = new Rect2D
					{
						Offset = new Offset2D
						{
							X = 0,
							Y = 0
						},
						Extent = extent
					},
					ClearValueCount = 1,
					ClearValues = new ClearValue[] { clearColor }
				};
				commandBuffers[i].CmdBeginRenderPass(renderPassInfo, SubpassContents.Inline);
				commandBuffers[i].CmdBindPipeline(PipelineBindPoint.Graphics, graphicsPipeline);
				commandBuffers[i].CmdDraw(3, 1, 0, 0);
				commandBuffers[i].CmdEndRenderPass();
				commandBuffers[i].End();
			}
		}
	}
}
