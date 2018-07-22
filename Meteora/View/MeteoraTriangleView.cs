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
			new Vertex(new float[] { -0.5f, -0.5f }, new float[] { 1.0f, 1.0f , 1.0f }),
			new Vertex(new float[] {  0.5f, -0.5f }, new float[] { 255/255f, 0.0f , 100/255f }),
			new Vertex(new float[] {  0.5f,  0.5f }, new float[] { 1.0f, 1.0f , 1.0f }),
			new Vertex(new float[] { -0.5f, 0.5f }, new float[] { 255/255f, 0.0f , 100/255f }),
		};

		private readonly int[] indices = new[]
		{
			0, 1, 2, 2, 3, 0
		};

		protected DescriptorSetLayout descriptorSetLayout;
		protected Vulkan.Buffer vertexBuffer;
		protected DeviceMemory vertexBufferMemory;
		protected Vulkan.Buffer indexBuffer;
		protected DeviceMemory indexBufferMemory;
		protected Vulkan.Buffer[] uniformBuffers;
		protected DeviceMemory[] unifromBuffersMemory;

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

		public override void Draw(uint curImage)
		{
			var start = DateTime.Now;

			var deltaTime = (DateTime.Now - start).TotalSeconds;
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
				commandBuffers[i].CmdBindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

				commandBuffers[i].CmdDrawIndexed((uint)indices.Length, 1, 0, 0, 0);

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

		protected override void CreateBuffers()
		{
			CreateVertexBuffer();
			CreateIndexBuffer();
			CreateUniformBuffer();
		}

		protected void CreateVertexBuffer()
		{
			var vtx = vertices.SelectMany(v => v.Data).ToArray();
			var bufferSize = vertices.Length * Vertex.SIZE;


			var (stagingBuffer, stagingBufferMemory) = CreateBuffer(vtx, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);

			(vertexBuffer, vertexBufferMemory) = CreateBuffer(bufferSize, BufferUsageFlags.TransferDst | BufferUsageFlags.VertexBuffer, MemoryPropertyFlags.DeviceLocal);

			CopyBuffer(stagingBuffer, vertexBuffer, bufferSize);

			device.DestroyBuffer(stagingBuffer);
			device.FreeMemory(stagingBufferMemory);
		}

		protected void CreateIndexBuffer()
		{
			var size = sizeof(int) * indices.Length;

			var (stagingBuffer, stagingBufferMemory) = CreateBuffer(indices, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);

			(indexBuffer, indexBufferMemory) = CreateBuffer(size, BufferUsageFlags.TransferDst | BufferUsageFlags.IndexBuffer, MemoryPropertyFlags.DeviceLocal);

			CopyBuffer(stagingBuffer, indexBuffer, size);

			device.DestroyBuffer(stagingBuffer);
			device.FreeMemory(stagingBufferMemory);
		}

		protected void CreateUniformBuffer()
		{
			var size = sizeof(float) * 4 * 4 * 3;

			uniformBuffers = new Vulkan.Buffer[images.Length];
			unifromBuffersMemory = new DeviceMemory[images.Length];

			for (int i = 0; i < images.Length; i++)
			{
				(uniformBuffers[i], unifromBuffersMemory[i]) = CreateBuffer(size, BufferUsageFlags.UniformBuffer, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);
			}
		}

		protected override void CreateDescriptorSetLayout()
		{
			var uboLayoutBinding = new DescriptorSetLayoutBinding
			{
				Binding = 0,
				DescriptorType = DescriptorType.UniformBuffer,
				DescriptorCount = 1,
				StageFlags = ShaderStageFlags.Vertex
			};
			var layoutInfo = new DescriptorSetLayoutCreateInfo
			{
				BindingCount = 1,
				Bindings = new[] { uboLayoutBinding }
			};
			descriptorSetLayout = device.CreateDescriptorSetLayout(layoutInfo);
		}

		protected override PipelineLayoutCreateInfo GetPipelineLayoutInfo() => new PipelineLayoutCreateInfo
		{
			SetLayoutCount = 1,
			SetLayouts = new[] { descriptorSetLayout }
		};

		public override void CleanupBuffers()
		{
			base.CleanupBuffers();
			device.DestroyDescriptorSetLayout(descriptorSetLayout);
			for (int i = 0; i < uniformBuffers.Length; i++)
			{
				device.DestroyBuffer(uniformBuffers[i]);
				device.FreeMemory(unifromBuffersMemory[i]);
			}
			device.DestroyBuffer(vertexBuffer);
			device.DestroyBuffer(indexBuffer);
			device.FreeMemory(vertexBufferMemory);
			device.FreeMemory(indexBufferMemory);
		}
	}
}
