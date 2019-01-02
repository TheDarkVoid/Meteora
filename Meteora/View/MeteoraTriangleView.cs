using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;
using Meteora.Data;
using Vulkan;

namespace Meteora.View
{
	public class MeteoraTriangleView : MeteoraViewBase
	{
		private static readonly Vertex[] vertices = new Vertex[]
		{
			new Vertex(new float[] { -0.5f, -0.5f, 0f }, new float[] { 1.0f, 1.0f , 1.0f }),			//0 BL
			new Vertex(new float[] {  0.5f, -0.5f, 0f }, new float[] { 255/255f, 0.0f , 100/255f }),	//1 BR
			new Vertex(new float[] {  0.5f,  0.5f, 0f }, new float[] { 1.0f, 1.0f , 1.0f }),			//2 TR
			new Vertex(new float[] { -0.5f,  0.5f, 0f }, new float[] { 255/255f, 0.0f , 100/255f }),	//3 TL

			new Vertex(new float[] { -0.5f, -0.5f, 1f }, new float[] { 1.0f, 1.0f , 1.0f }),			//4 BL
			new Vertex(new float[] {  0.5f, -0.5f, 1f }, new float[] { 255/255f, 0.0f , 100/255f }),	//5 BR
			new Vertex(new float[] {  0.5f,  0.5f, 1f }, new float[] { 1.0f, 1.0f , 1.0f }),			//6 TR
			new Vertex(new float[] { -0.5f,  0.5f, 1f }, new float[] { 255/255f, 0.0f , 100/255f }),	//7 TL
		};

		private static readonly int[] indices = new[]
		{
			//Front
			7, 3, 2, 2, 6, 7,
			//Back
			1, 0, 4, 4, 5, 1,
			//Left
			4, 0, 3, 3, 7, 4,
			//Right
			2, 1, 5, 5, 6, 2,
			//Top
			6, 5, 4, 4, 7, 6,
			//Bottom
			0, 1, 2, 2, 3, 0,
		};

		private Mesh mesh = new Mesh(vertices, indices);
		//private Mesh mesh = Mesh.LoadObj(@"Models/cube.obj");
		//private Mesh mesh = Mesh.LoadObj(@"Models/sphere.obj");
		//private Mesh mesh = Mesh.LoadObj(@"Models/monkey.obj");

		protected DescriptorSetLayout descriptorSetLayout;
		protected DescriptorPool descriptorPool;
		protected DescriptorSet[] descriptorSets;
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
			var deltaTime = (float)(DateTime.Now - start).TotalSeconds;
			UpdateUniformBuffer(curImage, deltaTime);
		}

		float angle = 0;
		public void UpdateUniformBuffer(uint currentImage, float deltaTime)
		{
			if (angle > 360)
				angle -= 360;
			angle += 0.01f;
			var ubo = new UniformBufferObject
			{
				model = mat4.Identity * mat4.Rotate(glm.Radians(angle), vec3.UnitZ),
				view = mat4.LookAt(new vec3(1, 1, 2), vec3.Zero, vec3.UnitZ),
				proj = mat4.Perspective(glm.Radians(90f), extent.Width / (float)extent.Height, .1f, 10f)
			};
			ubo.proj[1, 1] *= -1;
			var values = ubo.Values;
			var dst = device.MapMemory(unifromBuffersMemory[currentImage], 0, values.Length);
			Marshal.Copy(values, 0, dst, values.Length);
			device.UnmapMemory(unifromBuffersMemory[currentImage]);
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

				commandBuffers[i].CmdBindDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, 0, descriptorSets[i], null);

				commandBuffers[i].CmdDrawIndexed((uint)mesh.indices.Length, 1, 0, 0, 0);

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
			var vtx = mesh.vertices.SelectMany(v => v.Data).ToArray();
			var bufferSize = mesh.vertices.Length * Vertex.SIZE;


			var (stagingBuffer, stagingBufferMemory) = CreateBuffer(vtx, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);

			(vertexBuffer, vertexBufferMemory) = CreateBuffer(bufferSize, BufferUsageFlags.TransferDst | BufferUsageFlags.VertexBuffer, MemoryPropertyFlags.DeviceLocal);

			CopyBuffer(stagingBuffer, vertexBuffer, bufferSize);

			device.DestroyBuffer(stagingBuffer);
			device.FreeMemory(stagingBufferMemory);
		}

		protected void CreateIndexBuffer()
		{
			var size = sizeof(int) * mesh.indices.Length;

			var (stagingBuffer, stagingBufferMemory) = CreateBuffer(mesh.indices, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);

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

		protected override void CreateDescriptorPool()
		{
			DescriptorPoolSize poolSize = new DescriptorPoolSize
			{
				DescriptorCount = (uint)images.Length
			};
			DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo
			{
				PoolSizeCount = 1,
				PoolSizes = new DescriptorPoolSize[] { poolSize },
				MaxSets = (uint)images.Length
			};
			descriptorPool = device.CreateDescriptorPool(poolInfo);
		}

		protected override void CreateDescriptorSets()
		{
			var layouts = new DescriptorSetLayout[images.Length];
			for (int i = 0; i < layouts.Length; i++)
				layouts[i] = descriptorSetLayout;
			var allocInfo = new DescriptorSetAllocateInfo
			{
				DescriptorPool = descriptorPool,
				DescriptorSetCount = (uint)images.Length,
				SetLayouts = layouts
			};

			descriptorSets = device.AllocateDescriptorSets(allocInfo);
			for (int a = 0; a < descriptorSets.Length; a++)
			{
				var bufferInfo = new DescriptorBufferInfo
				{
					Buffer = uniformBuffers[a],
					Offset = 0,
					Range = (sizeof(float) * 16) * 2
				};
				var descriptorWrite = new WriteDescriptorSet
				{
					DstSet = descriptorSets[a],
					DstBinding = 0,
					DstArrayElement = 0,
					DescriptorType = DescriptorType.UniformBuffer,
					DescriptorCount = 1,
					BufferInfo = new DescriptorBufferInfo[] { bufferInfo }
				};
				device.UpdateDescriptorSet(descriptorWrite, null);
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
			device.DestroyDescriptorPool(descriptorPool);
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
