using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Meteora.Data;
using Vulkan;

namespace Meteora.View
{
	public class MeteoraTriangleView : MeteoraViewBase
	{
		private Vertex[] vertices = new Vertex[]
		{
			new Vertex(new float[] {  0.0f, -0.5f }, new float[] { 255/255f, 0.0f , 100/255f }),
			new Vertex(new float[] {  0.5f,  0.5f }, new float[] { 0.0f, 1.0f , 1.0f }),
			new Vertex(new float[] { -0.5f,  0.5f }, new float[] { 1.0f, 1.0f , 1.0f }),
		};

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

		protected override void InitCommandBuffers()
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
					Color = new ClearColorValue(new float[] { 25/255f, 0.0f, 10/255f, 1.0f } )
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

				commandBuffers[i].CmdBindVertexBuffer(0, vertexBuffer, 0);

				commandBuffers[i].CmdDraw((uint)vertices.Length, 1, 0, 0);
				commandBuffers[i].CmdEndRenderPass();
				commandBuffers[i].End();
			}
		}

		protected override PipelineVertexInputStateCreateInfo GetVertexInputInfo()
		{
			var bindingDesc = Vertex.GetBindingDescription();
			var attrDesc = Vertex.GetAttributeDescriptions();

			var vertexInputInfo = new PipelineVertexInputStateCreateInfo
			{
				VertexAttributeDescriptionCount = (uint)bindingDesc.Length,
				VertexBindingDescriptionCount = (uint)attrDesc.Length,
				VertexBindingDescriptions = bindingDesc,
				VertexAttributeDescriptions = attrDesc
			};

			return vertexInputInfo;
		}

		protected override void CreateVertexBuffer() => (vertexBuffer, vertexBufferMemory) = CreateBuffer(vertices.SelectMany(v => v.Data).ToArray());
	}
}
